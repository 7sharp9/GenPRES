namespace Informedica.GenForm.Lib


open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges


/// <summary>
/// Round-trip CHECK for the reverse map <c>DoseRule -&gt; DoseRuleData[]</c>.
/// </summary>
/// <remarks>
/// The reverse map itself (<c>DoseRule.toData</c>) lives in <c>DoseRule.fs</c>; this
/// module only exercises it and reports rows that fail FORWARD (one-directional)
/// containment.
///
/// <c>runPass</c> reverses every forward-rebuilt rule, <c>distinct</c>s the result,
/// then verifies FORWARD only that every surviving input row is CONTAINED in some
/// generated row with the same categorical identity. The generated row may carry
/// MORE quantitative information (one-directional). Categorical identity is matched
/// by exact string; quantitative values are compared as canonical tokens
/// (<c>ValueUnit.toToken</c> / <c>Limit.toToken</c>) so base-unit normalization
/// (1000 mg == 1 g) holds and incl|/excl| keep &gt;5 and &gt;=5 distinct.
///
/// PASS 1 runs on the source data; PASS 2 feeds PASS 1's generated output back in
/// (a fixpoint check). PASS 2 reaches ~100% containment because the generated set
/// is already forward-canonicalized. It is NOT an exact set-equality fixpoint: rows
/// sharing the same categorical identity but differing only in dose VALUES are
/// folded by the forward merge (<c>setDataHashIds</c> keys on identity, EXCLUDING
/// dose values), so PASS 2's re-forward collapses them into one wider row. They stay
/// CONTAINED. This is a forward-side merge-granularity property, not a reverse
/// defect — <c>toData</c> reconstructs GPK/HPK narrowing exactly.
/// </remarks>
module Analyze =

    /// Result of a single round-trip pass: the generated (reverse-of-forward,
    /// deduped) dataset plus the FORWARD containment statistics.
    type PassResult =
        {
            Label: string
            InputCount: int
            SurvivingCount: int
            ForwardCount: int
            GeneratedCount: int
            // input rows not contained in any same-identity generated row
            Missing: DoseRuleData[]
            // missing because no generated row shares the categorical identity
            NoIdMatch: DoseRuleData[]
            // missing because identity matched but quantitative values were not contained
            QuantMiss: DoseRuleData[]
            // reverse-of-forward, deduped (feeds the next pass)
            Generated: DoseRuleData[]
            // generated rows indexed by categorical identity (for reasonLines)
            GenById: Map<string, DoseRuleData[]>
            Pct: float
        }

    /// Direct fixpoint delta between two generated sets: is
    /// <c>toData(forward(gen1)) == gen1</c> exactly? <c>Collapsed</c> are the rows
    /// PASS 1 produced but PASS 2 subsumed (same-identity dose-value folds).
    type FixpointDelta =
        {
            Gen1Count: int
            Gen2Count: int
            InP1NotP2: int
            InP2NotP1: int
            Collapsed: DoseRuleData[]
        }


    let brStr (br: BigRational option) =
        br |> Option.map _.ToString() |> Option.defaultValue ""

    let genKey (g: GenericData) =
        [
            g.Name
            g.Form
            g.Brand
            g.GPKs |> Array.sort |> String.concat ","
            g.HPKs |> Array.sort |> String.concat ","
        ]
        |> String.concat "~"

    let patKey (p: PatientCategoryData) =
        // Location recovered. When IsAdult, ages are masked (forward sets IsAdult).
        let minAge, maxAge = if p.IsAdult then "", "" else brStr p.MinAge, brStr p.MaxAge

        [
            p.Location
            p.Dep
            string p.IsAdult
            $"%A{p.Gender}"
            minAge
            maxAge
            brStr p.MinWeight
            brStr p.MaxWeight
            brStr p.MinBSA
            brStr p.MaxBSA
            brStr p.MinGestAge
            brStr p.MaxGestAge
            brStr p.MinPMAge
            brStr p.MaxPMAge
        ]
        |> String.concat "~"


    /// Populated dose-limit leaves of a row, built by the library's OWN parser
    /// (getDoseLimits builds the mg/kg/day composites and drops values whose unit is
    /// absent, exactly as the forward path does). One row -> one DoseLimit.
    let doseLeaves (d: DoseRuleData) : Map<string, string> =
        let limTok (lim: Limit option) =
            // Limit.toToken keeps the inclusive/exclusive distinction (incl|/excl|)
            // on top of the canonical ValueUnit.toToken, so >5 and >=5 don't collide.
            lim |> Option.map Limit.toToken

        DoseRule.getDoseLimits [| d |]
        |> Array.collect (fun dl ->
            [|
                "qty.min", limTok dl.Quantity.Min
                "qty.max", limTok dl.Quantity.Max
                "qtyAdj.min", limTok dl.QuantityAdjust.Min
                "qtyAdj.max", limTok dl.QuantityAdjust.Max
                "perTime.min", limTok dl.PerTime.Min
                "perTime.max", limTok dl.PerTime.Max
                "perTimeAdj.min", limTok dl.PerTimeAdjust.Min
                "perTimeAdj.max", limTok dl.PerTimeAdjust.Max
                "rate.min", limTok dl.Rate.Min
                "rate.max", limTok dl.Rate.Max
                "rateAdj.min", limTok dl.RateAdjust.Min
                "rateAdj.max", limTok dl.RateAdjust.Max
            |]
        )
        |> Array.choose (fun (k, v) -> v |> Option.map (fun t -> k, t))
        |> Map.ofArray

    /// Populated schedule time leaves (administration / interval / duration) as base tokens.
    let schedLeaves (d: DoseRuleData) : Map<string, string> =
        let timeTok (b: BigRational option) (us: string) =
            match b, (us |> Utils.Units.timeUnit) with
            | Some v, Some u -> ValueUnit.singleWithUnit u v |> ValueUnit.toToken |> Some
            | Some v, None -> $"%s{string v} %s{us |> String.trim}" |> Some
            | None, _ -> None

        let s = d.ScheduleData

        [
            "admin.min", timeTok s.MinTime s.TimeUnit
            "admin.max", timeTok s.MaxTime s.TimeUnit
            "int.min", timeTok s.MinInt s.IntUnit
            "int.max", timeTok s.MaxInt s.IntUnit
            "dur.min", timeTok s.MinDur s.DurUnit
            "dur.max", timeTok s.MaxDur s.DurUnit
        ]
        |> List.choose (fun (k, v) -> v |> Option.map (fun t -> k, t))
        |> Map.ofList

    /// Frequency set, one canonical token per value. Mirrors the forward path,
    /// which builds DoseRule.Frequencies with `Utils.Units.freqUnit` (times/<time>)
    /// and `ValueUnit.withUnit`, then normalizes via `ValueUnit.toToken` so equivalent
    /// time-units collapse. Per-value tokens keep the subset semantics used by
    /// `containedIn`.
    let freqSet (d: DoseRuleData) : Set<string> =
        let s = d.ScheduleData

        match s.FreqUnit |> Utils.Units.freqUnit with
        | Some u ->
            s.Freqs
            |> Array.map (fun f -> [| f |] |> ValueUnit.withUnit u |> ValueUnit.toToken)
            |> Set.ofArray
        | None ->
            s.Freqs
            |> Array.map (fun f -> $"%s{string f}/%s{s.FreqUnit |> String.trim}")
            |> Set.ofArray

    /// Categorical identity of a row (exact). Excludes generated Id/GrpId/SortNo and
    /// the quantitative values (compared separately, semantically).
    let idKey (d: DoseRuleData) =
        [
            d.Source
            d.SourceText
            d.Indication
            d.Route
            d.PatientText
            d.ScheduleText
            genKey d.Generic
            patKey d.Patient
            d.ScheduleData.DoseType |> String.toLower
            d.ScheduleData.DoseText
            d.ScheduleData.DoseLimitData.Component
            d.ScheduleData.DoseLimitData.Substance
            string d.ScheduleData.DoseLimitData.CmpBased
        ]
        |> String.concat "||"

    /// Full row key for dedup (identity + all quantitative leaves, base-normalized).
    let rowKey (d: DoseRuleData) =
        let m2s (m: Map<string, string>) =
            m |> Map.toList |> List.map (fun (k, v) -> k + "=" + v) |> String.concat ";"

        [
            idKey d
            doseLeaves d |> m2s
            schedLeaves d |> m2s
            freqSet d |> Set.toList |> String.concat ","
        ]
        |> String.concat "##"


    /// original row is CONTAINED in a generated row: every quantitative value present
    /// in the original equals the generated's; the generated may carry MORE values.
    /// (identity is matched separately, before calling this.)
    let containedIn (gen: DoseRuleData) (orig: DoseRuleData) =
        /// every entry in the smaller map equals the corresponding entry in the larger one
        let subMap (om: Map<string, string>) (gm: Map<string, string>) =
            om
            |> Map.forall (fun k v ->
                match gm.TryFind k with
                | Some gv -> gv = v
                | None -> false
            )

        subMap (doseLeaves orig) (doseLeaves gen)
        && subMap (schedLeaves orig) (schedLeaves gen)
        && Set.isSubset (freqSet orig) (freqSet gen)


    let allLimitsEmpty (d: DoseRuleData) =
        let l = d.ScheduleData.DoseLimitData

        [
            l.MinQty
            l.MaxQty
            l.MinQtyAdj
            l.MaxQtyAdj
            l.MinPerTime
            l.MaxPerTime
            l.MinPerTimeAdj
            l.MaxPerTimeAdj
            l.MinRate
            l.MaxRate
            l.MinRateAdj
            l.MaxRateAdj
        ]
        |> List.forall Option.isNone

    /// surviving rows of any dataset = valid rows (forward keeps them), minus
    /// empty-limit no-substance rows (forward keeps them but they reverse to nothing).
    let surviving (data: DoseRuleData[]) =
        data
        |> Array.filter (fun d -> DoseRuleData.validateData d |> List.isEmpty)
        |> Array.filter (fun d ->
            not (
                d.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace
                && allLimitsEmpty d
            )
        )

    let trunc n (s: string) =
        if s.Length > n then s[.. n - 1] + "…" else s

    let rowLine (d: DoseRuleData) =
        let l = d.ScheduleData.DoseLimitData
        let s = List.init d.Source.Length (fun _ -> " ") |> String.concat ""

        sprintf
            "%s | %s | dt=%s cmp=%s subst=%s freqs=[%s]\n  %s | %s"
            d.Source
            d.Route
            d.ScheduleData.DoseType
            l.Component
            l.Substance
            (d.ScheduleData.Freqs |> Array.map string |> String.concat ",")
            s
            (trunc 80 d.ScheduleText)

    /// The reason line(s) why a single input row is missing, against a pass's
    /// <c>GenById</c>: picks the generated sibling that matches the most leaves and
    /// shows what it lacks.
    let reasonLines (genById: Map<string, DoseRuleData[]>) (orig: DoseRuleData) : string list =
        match genById.TryFind(idKey orig) with
        | None -> [ "no identity match" ]
        | Some gens ->
            // pick the generated sibling that matches the most leaves, show what it lacks
            let od, os, ofq = doseLeaves orig, schedLeaves orig, freqSet orig

            let best =
                gens
                |> Array.maxBy (fun g ->
                    let gd, gs, gf = doseLeaves g, schedLeaves g, freqSet g

                    (od |> Map.filter (fun k v -> gd.TryFind k = Some v) |> Map.count)
                    + (os |> Map.filter (fun k v -> gs.TryFind k = Some v) |> Map.count)
                    + (Set.intersect ofq gf |> Set.count)
                )

            let gd, gs, gf = doseLeaves best, schedLeaves best, freqSet best

            [
                for KeyValue(k, v) in od do
                    if gd.TryFind k <> Some v then
                        sprintf "%-14s orig=%-18s gen=%s" k v (gd.TryFind k |> Option.defaultValue "·")
                for KeyValue(k, v) in os do
                    if gs.TryFind k <> Some v then
                        sprintf "%-14s orig=%-18s gen=%s" k v (gs.TryFind k |> Option.defaultValue "·")
                let fmiss = Set.difference ofq gf

                if not fmiss.IsEmpty then
                    sprintf "freqs missing  orig=%s gen=%s" (String.concat "," ofq) (String.concat "," gf)
            ]


    /// One round-trip + FORWARD containment check over <paramref name="input"/>.
    /// Pure: returns the <c>PassResult</c> (stats + generated set); does not print.
    /// <paramref name="forward"/> rebuilds DoseRuleData[] -> DoseRule[] (passed in so
    /// this module stays free of any live provider).
    let runPass (forward: DoseRuleData[] -> DoseRule[]) (label: string) (input: DoseRuleData[]) : PassResult =
        let survivingInput = surviving input
        let fwd = input |> forward
        let generated = fwd |> Array.collect DoseRule.toData |> Array.distinctBy rowKey
        let genById = generated |> Array.groupBy idKey |> Map.ofArray

        // an input row is MISSING when no generated row with the same identity contains it
        let missing =
            survivingInput
            |> Array.filter (fun orig ->
                match genById.TryFind(idKey orig) with
                | Some gens -> gens |> Array.exists (fun g -> containedIn g orig) |> not
                | None -> true
            )

        let noIdMatch, quantMiss =
            missing |> Array.partition (fun o -> genById.ContainsKey(idKey o) |> not)

        let pct =
            100.0 * float (survivingInput.Length - missing.Length)
            / float (max 1 survivingInput.Length)

        {
            Label = label
            InputCount = input.Length
            SurvivingCount = survivingInput.Length
            ForwardCount = fwd.Length
            GeneratedCount = generated.Length
            Missing = missing
            NoIdMatch = noIdMatch
            QuantMiss = quantMiss
            Generated = generated
            GenById = genById
            Pct = pct
        }

    /// Direct fixpoint delta: rows in <paramref name="gen1"/> not in
    /// <paramref name="gen2"/> (and vice versa), keyed by <c>rowKey</c>.
    let fixpointDelta (gen1: DoseRuleData[]) (gen2: DoseRuleData[]) : FixpointDelta =
        let g1Keys = gen1 |> Array.map rowKey |> Set.ofArray
        let g2Keys = gen2 |> Array.map rowKey |> Set.ofArray

        {
            Gen1Count = gen1.Length
            Gen2Count = gen2.Length
            InP1NotP2 = Set.difference g1Keys g2Keys |> Set.count
            InP2NotP1 = Set.difference g2Keys g1Keys |> Set.count
            Collapsed = gen1 |> Array.filter (fun d -> g2Keys.Contains(rowKey d) |> not)
        }


/// <summary>
/// Builds the canonicalized, G-Standaard-checked dose-rule export and writes it to
/// a TSV file. Wraps the live forward rebuild + reverse map around a resource
/// provider; the round-trip CHECK itself lives in <see cref="T:Informedica.GenForm.Lib.Analyze"/>.
/// </summary>
module Export =

    /// PURE rebuild: DoseRuleData[] -> DoseRule[] via the loader (warnings dropped).
    /// Kept free of the (expensive) G-Standaard check so the round-trip passes stay
    /// fast — the check is irrelevant to containment and is run once, on export.
    let forward (provider: Resources.IResourceProvider) (data: DoseRuleData[]) : DoseRule[] =
        DoseRuleLoader.fromData (provider.GetRouteMappings()) (provider.GetFormRoutes()) (provider.GetProducts()) data
        |> fst

    /// Attach the no-patient G-Standaard check to each rule, in parallel.
    /// `Check` is a RuleCheck { FreqCheck; DoseCheck }: the graded signals (severity
    /// <> Within) split by kind — FrequencyMismatch -> FreqCheck, the dose-limit
    /// severities (over norm/absolute, under norm, unit mismatch, no monitoring) ->
    /// DoseCheck. None when there is nothing to report (limits agree with G-Standaard).
    ///
    /// G-Standaard dose-rule check WITHOUT a specific patient: the patient only scopes
    /// the G-Standaard query; the actual narrowing is done by the rule's OWN category,
    /// so an empty base patient (`Patient.patient`) returns the full G-Standaard dose
    /// set and the check is scoped purely by the rule itself.
    let withChecks (provider: Resources.IResourceProvider) (drs: DoseRule[]) : DoseRule[] =
        let gStand = provider.GetGStandProvider()

        // annotate: `Check` is a field on both DoseRule and DoseRuleData, so the
        // record-update target must be pinned to DoseRule.
        let check (dr: DoseRule) : DoseRule =
            let signals =
                dr |> Check.checkDoseRuleWithProvider gStand Patient.patient |> _.signals

            // Messages for the severities matching `keep`, deduped and joined; None when
            // empty. dataToCsv collapses tabs/newlines, so " | " keeps multiple messages
            // legible in a single TSV cell.
            let collect keep =
                signals
                |> Array.choose (fun (sev, s) -> if keep sev then Some s else None)
                |> Array.filter String.notEmpty
                |> Array.distinct
                |> function
                    | [||] -> None
                    | xs -> xs |> String.concat " | " |> Some

            { dr with
                Check =
                    {
                        FreqCheck = collect (fun sev -> sev = Check.FrequencyMismatch)
                        DoseCheck = collect (fun sev -> sev <> Check.Within && sev <> Check.FrequencyMismatch)
                    }
            }

        // No warm-up needed: Utils.Lib.Memoization.memoize is thread-safe
        // (ConcurrentDictionary + Lazy), so the shared caches the check path touches
        // (ZIndex rule cache, ZForm "Units"/"Frequencies" sheets) are each loaded at
        // most once even under this parallel map.
        drs |> Array.Parallel.map check

    /// Build the final export dataset from a (fixpoint) generated set: forward-rebuild,
    /// attach the G-Standaard check, reverse to data, dedup, then restamp RowId/RuleId/
    /// GrpId and number SortNo within each group exactly as the loader does. Single
    /// check pass — PASS 2 is a fixpoint, so re-forwarding the input is cheap.
    let exportData (provider: Resources.IResourceProvider) (generated: DoseRuleData[]) : DoseRuleData[] =
        generated
        |> forward provider
        |> withChecks provider
        |> Array.collect DoseRule.toData
        |> Array.distinctBy Analyze.rowKey
        |> Array.map DoseRuleData.setDataHashIds
        |> Array.groupBy _.GrpId
        |> Array.collect (snd >> Array.mapi (fun i dd -> { dd with SortNo = i }))

    /// Write the export dataset as TSV to <paramref name="fileName"/> and return the
    /// resolved path (parent directory found upward from the current directory).
    let writeExport (fileName: string) (data: DoseRuleData[]) : string =
        data
        |> DoseRuleData.dataToCsv
        |> String.concat "\n"
        |> File.writeTextToFile fileName

        let dir =
            fileName
            |> File.findParent Environment.CurrentDirectory
            |> Option.defaultValue "."

        $"{dir}/{fileName}"

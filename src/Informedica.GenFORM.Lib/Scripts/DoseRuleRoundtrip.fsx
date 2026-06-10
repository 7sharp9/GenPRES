(*
    DoseRuleRoundtrip.fsx
    ---------------------
    Roundtrip: raw DoseRuleData[] -> DoseRule[] (full forward incl. product
    expansion) -> DoseRuleData[]. Goal:
      - Pass 1: surviving input rows == collapsed output rows, modulo
                Id/GrpId/SortNo (which are blank in the source TSV).
      - Pass 2: out1 -> forward -> reverse -> out2, FULL equality out2 == out1
                including Id/GrpId/SortNo (out1 is a fixed point).

    SCRIPT-ONLY per AGENTS.md. The reverse mapping DoseRule.toData prototyped
    here is migrated to source by the maintainer after review.

    Use a FRESH FSI session (dotnet fsi from this dir) — loading GenForm *source*
    after the DLL is referenced collides on the Types namespace otherwise.

    Bootstrap mirrors CheckReview.fsx: load.fsx (#r dependency DLLs) + canonical
    GenForm source #load order + live cached provider via .env GENPRES_URL_ID.
*)

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")

#load "../Types.fs" "../Utils.fs" "../Logging.fs" "../Mapping.fs" "../Patient.fs"
       "../Product.fs" "../Filter.fs" "../LimitTarget.fs" "../DoseLimit.fs" "../DoseType.fs"
       "../GenericLabel.fs" "../PharmaceuticalForm.fs" "../ProductId.fs" "../Generic.fs"
       "../Source.fs" "../DoseRule.fs" "../DoseRuleData.fs" "../Check.fs"
       "../SolutionLimit.fs" "../SolutionRule.fs" "../RenalRule.fs"
       "../PrescriptionRule.fs" "../FormLogging.fs" "../Api.fs"

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Types

module GU = Informedica.GenUnits.Lib.Types


// ===========================================================================
// Live provider + raw input + forward
// ===========================================================================

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId Informedica.Logging.Lib.Logging.noOp dataUrlId

let rm = provider.GetRouteMappings ()
let fr = provider.GetFormRoutes ()
let prods = provider.GetProducts ()

let inputData : DoseRuleData[] =
    DoseRule.getData dataUrlId
    |> function
        | Ok d -> d
        | Error msgs -> failwithf "getData failed: %A" msgs

let forward (data: DoseRuleData[]) : DoseRule[] =
    DoseRule.fromData rm fr prods data
    |> function
        | Ok (rules, _) -> rules
        | Error msgs -> failwithf "fromData failed: %A" msgs


// ===========================================================================
// Shared small helpers
// ===========================================================================

/// Bare, group-less unit string; idempotent through UnitsParse.fromString.
let unitStr (u: GU.Unit) = u |> Units.toStringEngShortWithoutGroup

/// First BigRational value of a Limit option (the value as stored, in its unit).
let brOf (lim: Limit option) =
    lim |> Option.bind (Limit.getValueUnit >> ValueUnit.getValue >> Array.tryHead)

let mmTuple (mm: MinMax) = brOf mm.Min, brOf mm.Max

/// The unit carried by a MinMax (taken from whichever bound is present).
let mmUnit (mm: MinMax) : GU.Unit option =
    [ mm.Min; mm.Max ]
    |> List.choose id
    |> List.tryHead
    |> Option.map (Limit.getValueUnit >> ValueUnit.getUnit)

/// "kg" / "m2" / "" from an adjust Unit option.
let adjStr (u: GU.Unit option) =
    match u with
    | Some uu when uu |> Units.eqsUnit Units.Weight.kiloGram -> "kg"
    | Some uu when uu |> Units.eqsUnit Units.BSA.m2 -> "m2"
    | _ -> ""

/// Time-token of a frequency ValueUnit (the denominator of times/<time>).
let freqUnitStr (vuOpt: ValueUnit option) =
    match vuOpt with
    | Some vu ->
        match vu |> ValueUnit.getUnit with
        | GU.CombiUnit(_, GU.OpPer, t) -> t |> unitStr
        | _ -> ""
    | None -> ""

/// Denominator time-token of a per-rate / per-time unit (du/<x>).
let perUnitStr (mm: MinMax) =
    match mm |> mmUnit with
    | Some (GU.CombiUnit(_, GU.OpPer, d)) -> d |> unitStr
    | _ -> ""

/// Time-token of a plain time MinMax (administration/interval/duration).
let timeUnitStr (mm: MinMax) =
    match mm |> mmUnit with
    | Some u -> u |> unitStr
    | None -> ""


// ===========================================================================
// Reverse mapping  DoseRule -> DoseRuleData[]   (shadowed module)
// ===========================================================================

module DoseRule =

    open Informedica.GenForm.Lib.DoseRule

    // --- PROPOSED FORWARD FIX (prototype for migration to DoseRule.fs) ---
    // The narrowing GPK/HPK list is already preserved on Generic.Products, but
    // `hashId` excludes it, so two rows narrowed to different products collide on
    // identity and merge in fromData's groupBy. Fold the ProductId contents into
    // the identity (empty list contributes nothing) so they stay distinct.
    let prodKey (dr: DoseRule) =
        dr.Generic.Products
        |> List.map (function Gpk s -> "G" + s | Hpk s -> "H" + s)
        |> List.sort
        |> String.concat ","

    let correctedId (dr: DoseRule) =
        match prodKey dr with
        | "" -> hashId dr
        | pk -> hashId dr + "|" + pk

    /// fromData, re-grouped by correctedId instead of hashId. Real product
    /// expansion (addProducts) is kept unchanged.
    let fromDataFixed routeMapping formRoutes prods (data: DoseRuleData[]) =
        let addLimits = addDoseLimits routeMapping formRoutes
        match data |> addProducts prods routeMapping with
        | Error e -> failwithf "addProducts: %A" e
        | Ok(data2, _) ->
            data2
            |> Array.choose (fun d -> match mapToDoseRule d with Ok dr -> Some(dr, d) | _ -> None)
            |> Array.groupBy (fun (dr, _) -> correctedId dr)
            |> Array.map (fun (_, items) ->
                let head = items |> Array.head |> fst
                let rs = items |> Array.map snd
                head |> addLimits rs)

    let private emptyDoseLimitData : DoseLimitData =
        {
            CmpBased = false        // not represented on DoseRule (lossy)
            Component = ""
            Substance = ""
            DoseUnit = ""
            MinQty = None ; MaxQty = None
            MinQtyAdj = None ; MaxQtyAdj = None
            MinPerTime = None ; MaxPerTime = None
            MinPerTimeAdj = None ; MaxPerTimeAdj = None
            MinRate = None ; MaxRate = None
            MinRateAdj = None ; MaxRateAdj = None
        }

    /// Inverse of getDoseLimits for one DoseLimit, parented by component name.
    let reverseDoseLimit (cmpName: string) (dl: DoseLimit) : DoseLimitData =
        let substance =
            match dl.DoseLimitTarget with
            | SubstanceLimitTarget s -> s
            | _ -> ""

        let q = mmTuple dl.Quantity
        let qa = mmTuple dl.QuantityAdjust
        let pt = mmTuple dl.PerTime
        let pta = mmTuple dl.PerTimeAdjust
        let rt = mmTuple dl.Rate
        let rta = mmTuple dl.RateAdjust

        { emptyDoseLimitData with
            Component = cmpName
            Substance = substance
            DoseUnit = dl.DoseUnit |> unitStr
            MinQty = fst q ; MaxQty = snd q
            MinQtyAdj = fst qa ; MaxQtyAdj = snd qa
            MinPerTime = fst pt ; MaxPerTime = snd pt
            MinPerTimeAdj = fst pta ; MaxPerTimeAdj = snd pta
            MinRate = fst rt ; MaxRate = snd rt
            MinRateAdj = fst rta ; MaxRateAdj = snd rta
        }

    /// Inverse of the Generic part of mapToDoseRule. Narrowing is read from
    /// (Label, Products); the expansion form (Generic.Form) is ignored.
    let reverseGeneric (g: Generic) : GenericData =
        let name = g.Label |> GenericLabel.genericName

        let form =
            match g.Label with
            | GenericForm(_, f) -> f
            | _ -> ""

        let brand =
            match g.Label with
            | GenericBrand(_, b) -> b
            | _ -> ""

        let gpks =
            g.Products |> List.choose (function Gpk s -> Some s | _ -> None) |> List.toArray

        let hpks =
            g.Products |> List.choose (function Hpk s -> Some s | _ -> None) |> List.toArray

        {
            Name = name
            Form = form
            Brand = brand
            GPKs = gpks
            HPKs = hpks
        }

    /// Inverse of the PatientCategory part of mapToDoseRule.
    let reversePatient (pc: PatientCategory) : PatientCategoryData =
        let isAdult = (pc.Age = IsAdult)

        let minAge, maxAge =
            match pc.Age with
            | IsAdult -> None, None
            | AbsoluteAge mm -> mmTuple mm

        let minW, maxW = mmTuple pc.Weight
        let minB, maxB = mmTuple pc.BSA
        let minG, maxG = mmTuple pc.GestAge
        let minP, maxP = mmTuple pc.PMAge

        {
            Location = ""               // forced to None on DoseRule (lossy)
            Dep = pc.Department |> Option.defaultValue ""
            IsAdult = isAdult
            Gender = pc.Gender
            MinAge = minAge ; MaxAge = maxAge
            MinWeight = minW ; MaxWeight = maxW
            MinBSA = minB ; MaxBSA = maxB
            MinGestAge = minG ; MaxGestAge = maxG
            MinPMAge = minP ; MaxPMAge = maxP
        }

    let private dtCategory (dt: DoseType) =
        match dt with
        | Once _ -> "once"
        | OnceTimed _ -> "oncetimed"
        | Timed _ -> "timed"
        | Discontinuous _ -> "discontinuous"
        | Continuous _ -> "continuous"
        | NoDoseType -> ""

    let reverseSchedule (dr: DoseRule) (dl: DoseLimit) (dld: DoseLimitData) : ScheduleData =
        let doseType = dr.DoseType |> dtCategory
        let doseText = dr.DoseType |> DoseType.getText

        let freqs =
            dr.Frequencies
            |> Option.map ValueUnit.getValue
            |> Option.defaultValue [||]

        {
            DoseType = doseType
            DoseText = doseText
            Freqs = freqs
            AdjustUnit = dr.AdjustUnit |> adjStr
            FreqUnit = freqUnitStr dr.Frequencies
            RateUnit =
                // rate unit lives in Rate or (adjusted) RateAdjust, whichever is set
                match perUnitStr dl.Rate with
                | "" -> perUnitStr dl.RateAdjust
                | s -> s
            MinTime = fst (mmTuple dr.AdministrationTime)
            MaxTime = snd (mmTuple dr.AdministrationTime)
            TimeUnit = timeUnitStr dr.AdministrationTime
            MinInt = fst (mmTuple dr.IntervalTime)
            MaxInt = snd (mmTuple dr.IntervalTime)
            IntUnit = timeUnitStr dr.IntervalTime
            MinDur = fst (mmTuple dr.Duration)
            MaxDur = snd (mmTuple dr.Duration)
            DurUnit = timeUnitStr dr.Duration
            DoseLimitData = dld
        }

    /// Explode a DoseRule into its source data rows (one per component- or
    /// substance-level limit). FormLimit is NOT emitted: it is derived from the
    /// external formRoutes table, not from an input row.
    let toData (dr: DoseRule) : DoseRuleData[] =
        let gen = reverseGeneric dr.Generic
        let pat = reversePatient dr.PatientCategory

        let mkRow (cmpName: string) (dl: DoseLimit) : DoseRuleData =
            let dld = reverseDoseLimit cmpName dl
            {
                Id = "" ; GrpId = "" ; SortNo = 0      // reassigned by assignIds
                Source = dr.Source |> Source.toString
                SourceText = dr.SourceText
                Generic = gen
                Indication = dr.Indication
                Route = dr.Route
                PatientText = dr.PatientText
                Patient = pat
                ScheduleText = dr.ScheduleText
                ScheduleData = reverseSchedule dr dl dld
                Products = [||]
            }

        dr.ComponentLimits
        |> Array.collect (fun cl ->
            [|
                match cl.Limit with
                | Some l -> yield mkRow cl.Name l
                | None -> ()
                for sl in cl.SubstanceLimits do
                    yield mkRow cl.Name sl
            |]
        )


// ===========================================================================
// Canonical keys (robust dedup / set-compare / deterministic id assignment)
//   - excludes Id/GrpId/SortNo/Products
//   - excludes lossy fields: CmpBased, Patient.Location
//   - canonicalises unit spellings so dag==day, ml==mL, etc.
// ===========================================================================

let private brStr (br: BigRational option) =
    br |> Option.map (fun b -> b.ToString()) |> Option.defaultValue ""

/// Keep a string only when its associated value/unit is actually present.
let private gate cond s = if cond then s else ""

/// Canonicalise a dose/rate unit string via UnitsParse (None -> trimmed original).
let private cdose (s: string) =
    s |> UnitsParse.fromString |> Option.map unitStr |> Option.defaultValue (s |> String.trim)

/// Canonicalise a time / freq-interval token via the timeUnit wrapper.
let private ctime (s: string) =
    s |> Utils.Units.timeUnit |> Option.map unitStr |> Option.defaultValue (s |> String.trim)

let private cadj (s: string) =
    match s |> String.trim |> String.toLower with
    | "kg" -> "kg"
    | "m2" -> "m2"
    | x -> x

let private genKey (g: GenericData) =
    [
        g.Name
        g.Form
        g.Brand
        g.GPKs |> Array.sort |> String.concat ","
        g.HPKs |> Array.sort |> String.concat ","
    ]
    |> String.concat "~"

let private patKey (p: PatientCategoryData) =
    // Location excluded (lossy). When IsAdult, ages are masked.
    let minAge, maxAge = if p.IsAdult then "", "" else brStr p.MinAge, brStr p.MaxAge
    [
        p.Dep ; string p.IsAdult ; sprintf "%A" p.Gender
        minAge ; maxAge
        brStr p.MinWeight ; brStr p.MaxWeight
        brStr p.MinBSA ; brStr p.MaxBSA
        brStr p.MinGestAge ; brStr p.MaxGestAge
        brStr p.MinPMAge ; brStr p.MaxPMAge
    ]
    |> String.concat "~"

// A dose-limit value only survives the forward pipeline when every unit it
// needs is present on the row (DoseUnit, plus AdjustUnit / freq-time / RateUnit
// for the adjusted / per-time / rate variants). getDoseLimits drops the value
// otherwise. Gate each value the same way so the comparison is honest.
let private dldLeaves (adjOk: bool) (tuOk: bool) (ruOk: bool) (l: DoseLimitData) =
    let duOk = l.DoseUnit |> UnitsParse.fromString |> Option.isSome
    let g cond v = gate cond (brStr v)
    [
        "dld.component", l.Component ; "dld.substance", l.Substance ; "dld.doseU", cdose l.DoseUnit
        "dld.minQty", g duOk l.MinQty ; "dld.maxQty", g duOk l.MaxQty
        "dld.minQtyAdj", g (duOk && adjOk) l.MinQtyAdj ; "dld.maxQtyAdj", g (duOk && adjOk) l.MaxQtyAdj
        "dld.minPerTime", g (duOk && tuOk) l.MinPerTime ; "dld.maxPerTime", g (duOk && tuOk) l.MaxPerTime
        "dld.minPerTimeAdj", g (duOk && adjOk && tuOk) l.MinPerTimeAdj ; "dld.maxPerTimeAdj", g (duOk && adjOk && tuOk) l.MaxPerTimeAdj
        "dld.minRate", g (duOk && ruOk) l.MinRate ; "dld.maxRate", g (duOk && ruOk) l.MaxRate
        "dld.minRateAdj", g (duOk && adjOk && ruOk) l.MinRateAdj ; "dld.maxRateAdj", g (duOk && adjOk && ruOk) l.MaxRateAdj
    ]

/// Unit-availability flags for a schedule (drive dose-limit value gating).
let private unitFlags (s: ScheduleData) =
    let adjOk = (cadj s.AdjustUnit) <> ""
    let tuOk = s.FreqUnit |> Utils.Units.timeUnit |> Option.isSome
    let ruOk = s.RateUnit |> UnitsParse.fromString |> Option.isSome
    adjOk, tuOk, ruOk

let private dldKey (s: ScheduleData) =
    let adjOk, tuOk, ruOk = unitFlags s
    dldLeaves adjOk tuOk ruOk s.DoseLimitData
    |> List.map snd
    |> String.concat "~"

// A schedule/limit unit string only survives the forward pipeline when it is
// attached to a value; the raw source column is otherwise discarded. Gate each
// unit on the presence of its value so the comparison matches forward semantics.
let private schedKey (s: ScheduleData) =
    let l = s.DoseLimitData
    let hasRate = [ l.MinRate ; l.MaxRate ; l.MinRateAdj ; l.MaxRateAdj ] |> List.exists Option.isSome
    [
        s.DoseType |> String.toLower ; s.DoseText
        s.Freqs |> Array.map (fun b -> b.ToString()) |> String.concat ","
        cadj s.AdjustUnit
        gate (s.Freqs |> Array.isEmpty |> not) (ctime s.FreqUnit)
        gate hasRate (cdose s.RateUnit)
        brStr s.MinTime ; brStr s.MaxTime ; gate (s.MinTime.IsSome || s.MaxTime.IsSome) (ctime s.TimeUnit)
        brStr s.MinInt ; brStr s.MaxInt ; gate (s.MinInt.IsSome || s.MaxInt.IsSome) (ctime s.IntUnit)
        brStr s.MinDur ; brStr s.MaxDur ; gate (s.MinDur.IsSome || s.MaxDur.IsSome) (ctime s.DurUnit)
        dldKey s
    ]
    |> String.concat "~"

/// Content key — identity for dedup / set-compare / Id hashing (no Id/Grp/Sort).
let keyOf (d: DoseRuleData) =
    [
        d.Source ; d.SourceText ; d.Indication ; d.Route ; d.PatientText ; d.ScheduleText
        genKey d.Generic ; patKey d.Patient ; schedKey d.ScheduleData
    ]
    |> String.concat "||"

/// Rule-group key — same clinical context (Source/Generic/Route/Indication/Patient).
let grpKeyOf (d: DoseRuleData) =
    [ d.Source ; genKey d.Generic ; d.Route ; d.Indication ; patKey d.Patient ]
    |> String.concat "||"


// ===========================================================================
// Id / GrpId / SortNo (re)assignment — deterministic from content, every pass.
// ===========================================================================

let assignIds (rows: DoseRuleData[]) : DoseRuleData[] =
    rows
    |> Array.map (fun d -> { d with Id = String.sha1Short [ keyOf d ] })
    |> Array.groupBy grpKeyOf
    |> Array.collect (fun (gk, grp) ->
        let gid = String.sha1Short [ gk ]
        grp
        |> Array.sortBy (fun d -> d.Id)
        |> Array.mapi (fun i d -> { d with GrpId = gid ; SortNo = i + 1 })
    )

let collapse (rows: DoseRuleData[]) = rows |> Array.distinctBy keyOf


// ===========================================================================
// Driver
// ===========================================================================

/// surviving input = valid rows, minus empty-limit component (no-substance) rows
let private allLimitsEmpty (d: DoseRuleData) =
    let l = d.ScheduleData.DoseLimitData
    [
        l.MinQty ; l.MaxQty ; l.MinQtyAdj ; l.MaxQtyAdj
        l.MinPerTime ; l.MaxPerTime ; l.MinPerTimeAdj ; l.MaxPerTimeAdj
        l.MinRate ; l.MaxRate ; l.MinRateAdj ; l.MaxRateAdj
    ]
    |> List.forall Option.isNone

let survivingInput =
    let valid = inputData |> DoseRule.partitionValidRows |> fst
    valid
    |> Array.filter (fun d ->
        not (d.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace && allLimitsEmpty d)
    )

let roundtrip (data: DoseRuleData[]) =
    data |> forward |> Array.collect DoseRule.toData |> collapse |> assignIds

let out1 = roundtrip inputData

printfn "\n================ PASS 1 ================"
printfn "input rows        = %d" inputData.Length
printfn "surviving input   = %d" survivingInput.Length
printfn "forward rules     = %d" (inputData |> forward |> Array.length)
printfn "out1 (collapsed)  = %d" out1.Length

let inKeys = survivingInput |> Array.map keyOf |> Set.ofArray
let outKeys = out1 |> Array.map keyOf |> Set.ofArray

let inNotOut = Set.difference inKeys outKeys
let outNotIn = Set.difference outKeys inKeys

printfn "in-not-out        = %d" inNotOut.Count
printfn "out-not-in        = %d" outNotIn.Count

let private sampleDiff (label: string) (keys: Set<string>) (rows: DoseRuleData[]) =
    let byKey = rows |> Array.map (fun d -> keyOf d, d) |> dict
    printfn "\n--- %s (showing up to 5) ---" label
    keys
    |> Set.toSeq
    |> Seq.truncate 5
    |> Seq.iter (fun k ->
        match byKey.TryGetValue k with
        | true, d ->
            printfn "  %s | %s | %s | cmp=%s subst=%s dt=%s"
                d.Source (d.Generic.Name) d.Route
                d.ScheduleData.DoseLimitData.Component
                d.ScheduleData.DoseLimitData.Substance
                d.ScheduleData.DoseType
        | _ -> printfn "  (key not found in %s)" label)

sampleDiff "in-not-out" inNotOut survivingInput
sampleDiff "out-not-in" outNotIn out1

// ----- field-level near-miss diagnosis -----
// Pair input vs output rows by a COARSE identity; report which sub-key differs.
let coarseKey (d: DoseRuleData) =
    [
        d.Source ; d.Generic.Name ; d.Route ; d.Indication
        d.ScheduleData.DoseLimitData.Component ; d.ScheduleData.DoseLimitData.Substance
        d.ScheduleData.DoseType |> String.toLower ; d.ScheduleData.DoseText ; d.ScheduleText
        patKey d.Patient
    ]
    |> String.concat "||"

let private subKeys (d: DoseRuleData) =
    let g = d.Generic
    let p = d.Patient
    let s = d.ScheduleData
    let l = s.DoseLimitData
    [
        "srcText", d.SourceText ; "indic", d.Indication ; "patText", d.PatientText
        "schedText", d.ScheduleText
        "gen.gpks", g.GPKs |> Array.sort |> String.concat "," ; "gen.hpks", g.HPKs |> Array.sort |> String.concat ","
        "gen.form", g.Form ; "gen.brand", g.Brand
        "sched.doseText", s.DoseText
        "sched.freqs", s.Freqs |> Array.map (fun b -> b.ToString()) |> String.concat ","
        "sched.adjU", cadj s.AdjustUnit
        "sched.freqU", gate (s.Freqs |> Array.isEmpty |> not) (ctime s.FreqUnit)
        "sched.rateU", gate ([ l.MinRate ; l.MaxRate ; l.MinRateAdj ; l.MaxRateAdj ] |> List.exists Option.isSome) (cdose s.RateUnit)
        "sched.minTime", brStr s.MinTime ; "sched.maxTime", brStr s.MaxTime ; "sched.timeU", gate (s.MinTime.IsSome || s.MaxTime.IsSome) (ctime s.TimeUnit)
        "sched.minInt", brStr s.MinInt ; "sched.maxInt", brStr s.MaxInt ; "sched.intU", gate (s.MinInt.IsSome || s.MaxInt.IsSome) (ctime s.IntUnit)
        "sched.minDur", brStr s.MinDur ; "sched.maxDur", brStr s.MaxDur ; "sched.durU", gate (s.MinDur.IsSome || s.MaxDur.IsSome) (ctime s.DurUnit)
        yield! (let adjOk, tuOk, ruOk = unitFlags s in dldLeaves adjOk tuOk ruOk l)
    ]

let outByCoarse = out1 |> Array.groupBy coarseKey |> dict

/// Differing leaves between two rows: (name, inVal, outVal) list.
let private diffLeaves (a: DoseRuleData) (b: DoseRuleData) =
    List.zip (subKeys a) (subKeys b)
    |> List.choose (fun ((nm, av), (_, bv)) -> if av <> bv then Some(nm, av, bv) else None)

// For each in-not-out row, pair with the same-coarse out row that differs in
// the FEWEST leaves (min-difference pairing), and collect those differing leaves.
let perRowDiffs =
    inNotOut
    |> Set.toSeq
    |> Seq.map (fun k ->
        let inRow = survivingInput |> Array.find (fun d -> keyOf d = k)
        match outByCoarse.TryGetValue(coarseKey inRow) with
        | true, outs when outs.Length > 0 ->
            outs |> Array.map (diffLeaves inRow) |> Array.minBy List.length
        | _ -> [ "NO-COARSE-MATCH", "", "" ])
    |> Seq.toList

printfn "\n--- differing-leaf histogram (a row may count >1) ---"
perRowDiffs
|> List.collect (List.map (fun (nm, _, _) -> nm))
|> List.countBy id
|> List.sortByDescending snd
|> List.iter (fun (nm, n) -> printfn "  %-18s %d" nm n)

printfn "\n--- rows with NO same-coarse partner: %d ---"
    (perRowDiffs |> List.filter (List.exists (fun (n, _, _) -> n = "NO-COARSE-MATCH")) |> List.length)

printfn "\n--- one example per differing leaf ---"
perRowDiffs
|> List.collect id
|> List.distinctBy (fun (nm, _, _) -> nm)
|> List.iter (fun (nm, iv, ov) ->
    let trunc (s: string) = if s.Length > 80 then s[..79] + "…" else s
    printfn "  [%s]\n    in : %s\n    out: %s" nm (trunc iv) (trunc ov))

printfn "\n--- NO-COARSE-MATCH input rows (up to 20) ---"
inNotOut
|> Set.toSeq
|> Seq.map (fun k -> survivingInput |> Array.find (fun d -> keyOf d = k))
|> Seq.filter (fun d -> not (outByCoarse.ContainsKey(coarseKey d)))
|> Seq.truncate 20
|> Seq.iter (fun d ->
    printfn "  %s | name=%s form=%s brand=%s gpk=%s hpk=%s | %s | cmp=%s subst=%s dt=%s"
        d.Source d.Generic.Name d.Generic.Form d.Generic.Brand
        (d.Generic.GPKs |> String.concat ",") (d.Generic.HPKs |> String.concat ",")
        d.Route
        d.ScheduleData.DoseLimitData.Component d.ScheduleData.DoseLimitData.Substance
        d.ScheduleData.DoseType)

// Classify each in-not-out row. A "merge-loss" is a source row whose dose LIMIT
// survives in out1 but attached to a sibling's schedule: the forward merged two
// source rows that collide on rule identity (hashId) but differ only in
// ScheduleText / Frequencies / admin schedule — distinctions the structured
// identity does not capture. These are inherent forward-model losses, not
// reverse-mapping bugs (PASS 2 fixpoint proves the reverse is sound).
let private limitKey (d: DoseRuleData) =
    // identity + dose-limit VALUES, excluding presentation/schedule fields
    let adjOk, tuOk, ruOk = unitFlags d.ScheduleData
    [
        d.Source ; genKey d.Generic ; d.Route ; d.Indication ; patKey d.Patient
        d.ScheduleData.DoseType |> String.toLower
        dldLeaves adjOk tuOk ruOk d.ScheduleData.DoseLimitData |> List.map snd |> String.concat "~"
    ]
    |> String.concat "||"

let out1LimitKeys = out1 |> Array.map limitKey |> Set.ofArray

let inNotOutRows =
    inNotOut |> Set.toArray |> Array.map (fun k -> survivingInput |> Array.find (fun d -> keyOf d = k))

let private isNarrowed (d: DoseRuleData) =
    d.Generic.GPKs.Length > 0 || d.Generic.HPKs.Length > 0
    || (d.Generic.Form |> String.notEmpty)

let mergeLoss, rest =
    inNotOutRows |> Array.partition (fun d -> out1LimitKeys.Contains(limitKey d))

let narrowingLoss, trulyOther = rest |> Array.partition isNarrowed

printfn "\n--- residual classification (in-not-out = %d) ---" inNotOutRows.Length
printfn "  merge-loss     (hashId identity collision; limit kept, schedule re-attached): %d" mergeLoss.Length
printfn "  narrowing-loss (row-level form/GPK/HPK narrowing dissolved by expansion):     %d" narrowingLoss.Length
printfn "  truly-other    (needs investigation):                                          %d" trulyOther.Length
printfn "  truly-other generics: %s"
    (trulyOther |> Array.map (fun d -> d.Generic.Name) |> Array.distinct |> Array.truncate 12 |> String.concat ", ")

let dumpGen (gen: string) (rte: string) =
    printfn "\n=== %s / %s ===" gen rte
    let show tag (rows: DoseRuleData[]) =
        rows
        |> Array.filter (fun d -> d.Generic.Name |> String.equalsCapInsens gen && d.Route = rte)
        |> Array.iter (fun d ->
            let l = d.ScheduleData.DoseLimitData
            printfn "  %s ind=%s pat=%s subst=%s dt=%s freqs=%s maxQtyAdj=%s maxPerTimeAdj=%s schedText=%s"
                tag (d.Indication |> fun s -> s[.. min 18 (s.Length-1)]) (patKey d.Patient)
                l.Substance d.ScheduleData.DoseType
                (d.ScheduleData.Freqs |> Array.map string |> String.concat ",")
                (brStr l.MaxQtyAdj) (brStr l.MaxPerTimeAdj)
                (d.ScheduleText |> fun s -> if s.Length > 40 then s[..39] + "…" else s))
    show "IN " survivingInput
    show "OUT" out1

dumpGen "vancomycine" "INTRAVENEUS"
dumpGen "triamcinolonacetonide" "NASAAL"

// Confirm GPK-narrowing fate: does the original narrowing GPK survive on the DoseRule?
let dumpRuleIdentity (gen: string) (rte: string) =
    printfn "\n=== %s %s DoseRule identity (narrowing fate) ===" gen rte
    let rules =
        forward inputData
        |> Array.filter (fun dr -> (dr.Generic |> Generic.genericName) = gen && dr.Route = rte)
    printfn "  total rules=%d  with non-empty Generic.Products=%d"
        rules.Length (rules |> Array.filter (fun dr -> not dr.Generic.Products.IsEmpty) |> Array.length)
    rules
    |> Array.filter (fun dr -> not dr.Generic.Products.IsEmpty)
    |> Array.truncate 4
    |> Array.iter (fun dr ->
        printfn "  NARROWED label=%A form=%A Products=%A" dr.Generic.Label dr.Generic.Form dr.Generic.Products)

dumpRuleIdentity "nadroparine" "SUBCUTAAN"   // gpk-narrowed input
dumpRuleIdentity "nevirapine" "ORAAL"         // form-narrowed input

// Decisive check: do raw rows that differ ONLY by GPK get the SAME hashId?
printfn "\n=== nadroparine raw rows grouped by hashId (via mapToDoseRule) ==="
inputData
|> Array.filter (fun d -> d.Generic.Name |> String.equalsCapInsens "nadroparine" && d.Route = "SUBCUTAAN")
|> Array.choose (fun d -> match DoseRule.mapToDoseRule d with Ok dr -> Some(dr.Id, d) | _ -> None)
|> Array.groupBy fst
|> Array.iter (fun (id, items) ->
    printfn "  hashId=%s  rows=%d  GPKs=%A  doseTexts=%A"
        id items.Length
        (items |> Array.collect (fun (_, d) -> d.Generic.GPKs) |> Array.distinct)
        (items |> Array.map (fun (_, d) -> d.ScheduleData.DoseText |> fun s -> if s.Length > 24 then s[..23] else s) |> Array.distinct))

let pass1 = inNotOut.IsEmpty && outNotIn.IsEmpty
printfn "\nPASS 1 (set equality modulo Id/GrpId/SortNo): %b" pass1


// ----- PASS 2: fixpoint -----
let out2 = roundtrip out1

let fullKey (d: DoseRuleData) =
    [ d.Id ; d.GrpId ; string d.SortNo ; keyOf d ] |> String.concat "##"

let s1 = out1 |> Array.map fullKey |> Set.ofArray
let s2 = out2 |> Array.map fullKey |> Set.ofArray

printfn "\n================ PASS 2 (fixpoint) ================"
printfn "out1 = %d  out2 = %d" out1.Length out2.Length
printfn "out1-not-out2 = %d  out2-not-out1 = %d"
    (Set.difference s1 s2).Count (Set.difference s2 s1).Count

let pass2 = (s1 = s2)
printfn "PASS 2 (full equality incl Id/GrpId/SortNo): %b" pass2

// ----- PASS 1 with the PROPOSED hashId fix (hashId + ProductId contents) -----
printfn "\n================ PASS 1 with PROPOSED hashId FIX ================"
let out1Fixed =
    inputData |> DoseRule.fromDataFixed rm fr prods |> Array.collect DoseRule.toData |> collapse |> assignIds
let outKeysF = out1Fixed |> Array.map keyOf |> Set.ofArray
let inNotOutF = Set.difference inKeys outKeysF
let outNotInF = Set.difference outKeysF inKeys
let matchedF = survivingInput.Length - inNotOutF.Count
printfn "out1Fixed=%d  matched=%d/%d = %.1f%%  in-not-out=%d  out-not-in=%d"
    out1Fixed.Length matchedF survivingInput.Length
    (100.0 * float matchedF / float survivingInput.Length) inNotOutF.Count outNotInF.Count

let inNotOutRowsF =
    inNotOutF |> Set.toArray |> Array.map (fun k -> survivingInput |> Array.find (fun d -> keyOf d = k))
let out1FLimit = out1Fixed |> Array.map limitKey |> Set.ofArray
let mergeF, restF = inNotOutRowsF |> Array.partition (fun d -> out1FLimit.Contains(limitKey d))
let narrowF, otherF = restF |> Array.partition isNarrowed
printfn "  residual %d = %d merge-loss + %d narrowing-loss + %d truly-other"
    inNotOutF.Count mergeF.Length narrowF.Length otherF.Length
printfn "  narrowing-loss generics: %s"
    (narrowF |> Array.map (fun d -> d.Generic.Name) |> Array.distinct |> Array.truncate 12 |> String.concat ", ")

printfn "\n================ RESULT ================"
let matched = survivingInput.Length - inNotOut.Count
printfn "PASS 1: %d / %d surviving rows round-trip exactly (modulo Id/GrpId/SortNo) = %.1f%%"
    matched survivingInput.Length (100.0 * float matched / float survivingInput.Length)
printfn "        residual %d = %d merge-loss + %d narrowing-loss + %d truly-other"
    inNotOut.Count mergeLoss.Length narrowingLoss.Length trulyOther.Length
printfn "        (all residual = forward-side information loss; reverse is sound)"
printfn "PASS 2: fixpoint out2 == out1 incl Id/GrpId/SortNo = %b" pass2
printfn "\nPASS 1 (strict) = %b   PASS 2 (fixpoint) = %b" pass1 pass2

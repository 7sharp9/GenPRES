(*
    CheckReview.fsx
    ---------------
    Prototype fixes for `Informedica.GenForm.Lib.Check` measured against the
    Z-Index "Implementatierichtlijn Doseringscontrole" IR V-5-0-1 (16-09-2025).

    Companion to docs/code-reviews/genform-check-vs-ir-doseringscontrole-v5-0-1.md

    SCRIPT-ONLY per AGENTS.md. Nothing here is migrated automatically — each
    helper below is annotated with the exact Check.fs location it replaces, so
    the maintainer can review and migrate deliberately.

    All helpers were validated in FSI against the live provider (7866 dose rules)
    on 2026-06-08; the deterministic checks at the bottom reproduce that.

    NOTE on loading: in a long-lived FSI session that already references a built
    Informedica.GenFORM.Lib.dll, loading the GenForm *source* collides on the
    `Informedica.GenForm.Lib.Types` namespace (FormLogging.fs aborts the #load).
    Use a FRESH FSI session for this script. The bootstrap below loads the full
    canonical source order.
*)

#load "load.fsx"

open System
Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")

// Canonical compile order (see Informedica.GenFORM.Lib.fsproj). The stale
// Scripts/load.fsx predates the v2 migration and omits GenericLabel/
// PharmaceuticalForm/ProductId/Generic/Source/DoseRuleData — load them here.
#load "../Types.fs" "../Utils.fs" "../Logging.fs" "../Mapping.fs" "../Patient.fs"
       "../Product.fs" "../Filter.fs" "../LimitTarget.fs" "../DoseLimit.fs" "../DoseType.fs"
       "../GenericLabel.fs" "../PharmaceuticalForm.fs" "../ProductId.fs" "../Generic.fs"
       "../Source.fs" "../DoseRule.fs" "../DoseRuleData.fs" "../DoseRuleLoader.fs" "../Check.fs"
       "../SolutionLimit.fs" "../SolutionRule.fs" "../RenalRule.fs"
       "../PrescriptionRule.fs" "../FormLogging.fs" "../Api.fs"

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Check


// ===========================================================================
// Shared MinMax helpers (test scaffolding only)
// ===========================================================================

let mk v u = v |> ValueUnit.singleWithUnit u |> Limit.inclusive
let mmOf vmin vmax u = { Min = Some(mk vmin u); Max = Some(mk vmax u) }


// ===========================================================================
// MEDIUM-1 — IR 4.6.1 limit-selection priority: m² (BSA) -> kg -> absolute.
//
// Check.fs createMapping selects NormWeight/AbsWeight FIRST (kg), BSA only as
// fallback (lines 391-414, 417-440) and rateFieldsFor likewise (lines 490-509).
// IR 4.6.1 requires the m² range to win when present. With weight-first, a
// per-m² GenFORM limit is silently skipped (checkAdjustUnit finds no kg match)
// even though G-Standaard *has* a per-m² range.
//
// Drop-in: replace each `if <weight> = MinMax.empty then (if <bsa> ...) else
// <weight>` block with `pickAdjust`. Also note this folds in BUG-A: the current
// quantityAdjustAbs BSA branch reads x.StartDosage.AbsBSA (line 405/408) while
// its kg branch reads x.SingleDosage.AbsWeight — inconsistent; pickAdjust takes
// both ranges from the same DoseRange so the inconsistency cannot recur.
// ===========================================================================

/// IR 4.6.1 priority. `conv` is the createMapping `convert`/`setAdjustAndOrTimeUnit`
/// partial application that stamps the adjust (and optional time) unit.
let pickAdjust
    (weightMM: MinMax)
    (bsaMM: MinMax)
    (convWeight: MinMax -> MinMax)
    (convBSA: MinMax -> MinMax)
    : MinMax =
    if bsaMM <> MinMax.empty then bsaMM |> convBSA
    elif weightMM <> MinMax.empty then weightMM |> convWeight
    else MinMax.empty


// ===========================================================================
// MEDIUM-2 — IR 4.6.2 norm-max vs absolute-max severity & conditional flow.
//
// Check.fs checks Norm and Abs ranges independently and emits identical
// "niet in bereik" strings (quantityChecks/perTimeChecks, lines 577-649),
// losing the IR distinction: exceeding *norm max* is advisory (tekst 1),
// exceeding *absolute max* is serious (tekst 3), and abs is only checked when
// norm is exceeded. Replace the bool-option result with a Severity so callers
// (UI/log) can grade signals.
// ===========================================================================

type Severity =
    | Within
    | AdvisoryOverNorm      // > norm max, <= absolute max  (IR tekst 1)
    | OverAbsolute          // > absolute max               (IR tekst 3)
    | UnderNorm             // < norm min                   (IR tekst 2)
    | UnitMismatch          // kg vs m² — cannot compare
    | NotComparable         // missing dose type / both ranges empty

/// IR 4.6.2 flow on scalar bounds (the point-vs-bounds form). For the
/// range-vs-range form used in Check.fs, apply the same precedence using the
/// GenFORM range's Max against gstand norm/abs Max, and Min against norm Min.
let classify (dose: BigRational) normMax absMax normMin : Severity =
    let overNorm = match normMax with Some n -> dose > n | None -> false
    if overNorm then
        match absMax with
        | Some a when dose > a -> OverAbsolute
        | _ -> AdvisoryOverNorm
    else
        match normMin with
        | Some n when dose < n -> UnderNorm
        | _ -> Within


// ===========================================================================
// HIGH-1 — IR 4.6.1.3 / 4.6.1.4 risk-aware, one-sided, configurable margin.
//
// Check.fs `toMinMax` (lines 455-465) applies a symmetric ±10% band
// UNCONDITIONALLY. IR: margin is (a) one-sided on the max, (b) provider-
// configurable (example 120%), and (c) MUST NOT be applied to narrow-
// therapeutic-index substances (GPRISC = *). GenFORM types currently carry no
// risk flag — see the prerequisite note at the foot of this file.
// ===========================================================================

/// Replacement for `toMinMax`. `isRisk = true` => exact (no margin).
/// `marginUpper` is the multiplier on the max (e.g. 12N/10N for 120%).
let marginedTestRange (isRisk: bool) (marginUpper: BigRational) (vuOpt: ValueUnit option) : MinMax =
    match vuOpt with
    | None -> MinMax.empty
    | Some vu when isRisk -> { Min = Some(vu |> Limit.inclusive); Max = Some(vu |> Limit.inclusive) }
    | Some vu ->
        let factor = [| marginUpper |] |> ValueUnit.withUnit Units.Count.times
        {
            Min = Some(vu |> Limit.inclusive)
            Max = Some(vu * factor |> Limit.inclusive)
        }


// ===========================================================================
// HIGH-2 — IR 1.3.2 (5,6) excludes toedieningssnelheid/-duur from the dose
// check. Check.fs runs rateChecks for Continuous/Timed/OnceTimed (lines
// 651-686). Recommended: keep the rate signal but label it out-of-scope so it
// is not presented as a guideline violation. Flag controls behaviour.
// ===========================================================================

type RateCheckMode =
    | DropRateChecks                 // strict IR conformance
    | LabelRateChecksOutOfScope      // keep, but tag (recommended)

let rateScopeLabel (mode: RateCheckMode) (msg: string) =
    match mode with
    | DropRateChecks -> None
    | LabelRateChecksOutOfScope -> Some $"[buiten G-Standaard doseringscontrole] {msg}"


// ===========================================================================
// CheckConfig — carries the HIGH-1 margin (provider-configurable, one-sided on
// the max, default 120% per IR 4.6.1.3) and the HIGH-2 RateCheckMode. Migration:
// thread this into `checkDoseRule` as `checkDoseRuleWith cfg routeMappings pat`,
// keeping `checkDoseRule routeMappings pat = checkDoseRuleWith CheckConfig.def ...`.
// ===========================================================================

type CheckConfig =
    {
        // Upper margin multiplier on the G-Standaard max for non-risk substances
        MarginUpper: BigRational
        // Whether infusion-rate rows are dropped or kept-but-labelled (HIGH-2)
        RateCheckMode: RateCheckMode
    }

let checkConfigDef =
    {
        MarginUpper = 12N / 10N
        RateCheckMode = LabelRateChecksOutOfScope
    }


// ===========================================================================
// MEDIUM-3 — IR 3.3 age categories are in months (1 mo = 30 days; <1 mo as a
// fraction). Check.fs filterPatient (lines 238-250) intersects the GenFORM
// patient age (days) with the ZForm PatientDosage age directly; the inline TODO
// confirms the month→day mapping is missing. Normalise the ZForm age to days at
// 30 days/month before intersecting. NB: confirm the exact ZForm age unit in
// `Informedica.ZForm.Lib.Types.PatientDosage.Patient.Age` before migrating.
// ===========================================================================

/// Convert a months-based age MinMax to days at the IR's 30-days/month.
let monthsMinMaxToDays (mm: MinMax) : MinMax =
    let toDays lim =
        lim
        |> Limit.getValueUnit
        |> ValueUnit.convertTo Units.Time.month   // ensure in months
        |> ValueUnit.applyToValue (Array.map ((*) 30N))
        |> ValueUnit.setUnit Units.Time.day
        |> Limit.inclusive
    {
        Min = mm.Min |> Option.map toDays
        Max = mm.Max |> Option.map toDays
    }


// ===========================================================================
// LOW-3 — IR 3.4 frequency time-unit interchangeability. Check.fs freqRow
// (lines 560-575) uses ValueUnit.isSubset, which treats "per maand" and
// "per 4 weken" as different. Canonicalise interchangeable units first.
// per-12-weken vs per-3-maanden are deliberately NOT interchangeable.
// ===========================================================================

let interchangeGroups =
    [
        set [ "per 2 dagen"; "om de dag" ]
        set [ "per 4 weken"; "per maand" ]
        set [ "per 8 weken"; "per 2 maanden" ]
        set [ "per half jaar"; "per 6 maanden" ]
    ]

let canonTimeUnit (u: string) =
    interchangeGroups
    |> List.tryFind (Set.contains u)
    |> Option.map Set.minElement
    |> Option.defaultValue u

let interchangeable a b = canonTimeUnit a = canonTimeUnit b


// ===========================================================================
// LOW-4 — IR 4.5.2 frequency message granularity (tekst 24 / 25 / 8). Check.fs
// freqRow emits one combined message; split by which component differs.
// ===========================================================================

let freqMsg (aantalDiff: bool) (eenheidDiff: bool) =
    match aantalDiff, eenheidDiff with
    | true, false -> "tekst 24 (aantal verschilt)"
    | false, true -> "tekst 25 (tijdseenheid verschilt)"
    | _ -> "tekst 8 (aantal en/of tijdseenheid verschilt)"


// ===========================================================================
// LOW-2 — IR 4.2.4 gender gate. Check.fs createDoseRulesWithMapping (line 86)
// calls `GStand.createDoseRules GStand.config a w None None gen frm` — gender is
// never passed. ZIndex RuleFinder already filters products by gender, so for the
// *dose range* check this is mostly a no-op; document the decision rather than
// add a parameter unless a gender-specific dose range case is found.
// ===========================================================================


// ===========================================================================
// Live provider + regression baseline
// ===========================================================================

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId Informedica.Logging.Lib.Logging.noOp dataUrlId

let pat =
    { Patient.patient with
        Age = 5N |> ValueUnit.singleWithUnit Units.Time.year |> Some
        Weight = 22N |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
        Height = 117N |> ValueUnit.singleWithUnit Units.Height.centiMeter |> Some
    }

let routeMappings = provider.GetRouteMappings ()

let acicloIV =
    provider.GetDoseRules ()
    |> Array.filter (fun dr ->
        dr.PatientCategory |> PatientCategory.filterPatient pat
        && (dr.Generic |> Generic.genericName) = "aciclovir"
        && dr.Route = "INTRAVENEUS")

// Regression: the existing checker still runs end-to-end.
let baseline = acicloIV |> Array.map (Check.checkDoseRule routeMappings pat)


// ===========================================================================
// HIGH-1 PREREQUISITE — Option 1: source the GPRISC high-risk flag for
// marginedTestRange from G-Standaard (no GenFORM column required).
//
// The risk flag is NOT a GenFORM property: it lives on the G-Standaard
// substance as ZIndex DoseRule.HighRisk (= GPRISC "*", ZIndex/DoseRule.fs:422,
// from bst640.GPRISC). Check.fs reaches G-Standaard via GStand.createDoseRules,
// whose final ZForm Dosage DROPS the flag — ZForm has no HighRisk field and
// Dosage.Rules is only a string tag (GStandRule/PedFormRule). So at the point
// `toMinMax` runs the bit is already gone, even though ZIndex had it.
//
// Option-1 migration target (in ZForm.Lib): add `HighRisk: bool` to the ZForm
// Dosage and set it inside GStand.createDoseRules from the ZIndexTypes.DoseRule
// it already holds internally (GStand.fs:281/352/450 — `doserules`). Then
// Check.matchWithZIndex surfaces it and `marginedTestRange isRisk ...` reads it
// per matched rule. No GenFORM schema change, no second lookup, no edit to
// docs/mdr/design-history/0003-resource-requirements.md.
//
// Script-only constraint (AGENTS.md): we cannot edit ZForm here, so to VALIDATE
// the threading end-to-end we re-derive the flag from the SAME ZIndex DoseRules
// GStand consumes (RuleFinder). This proves the bit is available at the GStand
// boundary; the production change just stops discarding it.
// ===========================================================================

module RF = Informedica.ZIndex.Lib.RuleFinder

/// GPRISC is a substance-level safety code, so it is resolved per generic+route
/// over ALL matching G-Standaard rules: ANY high-risk rule => treat as risk =>
/// no margin (conservative). Patient is intentionally NOT filtered — a narrow-TI
/// substance is narrow-TI for every patient. GenFORM routes are raw G-Standaard
/// route names (e.g. "INTRAVENEUS"), matched directly by RuleFinder.
let gstandHighRisk (gen: string) (rte: string) : bool =
    RF.createFilter None None None None gen "" rte
    |> RF.find []
    |> Array.exists _.HighRisk

/// Option-1 shape: what Check.matchWithZIndex would carry once GStand threads
/// the flag. Attaches `highRisk` next to the existing matched data so the
/// downstream `marginedTestRange isRisk ...` has the bool it needs.
let matchWithRisk routeMapping (pat: Patient) (dr: DoseRule) =
    let m = dr |> matchWithZIndex routeMapping pat
    {| m with
        highRisk = gstandHighRisk (dr.Generic |> Generic.genericName) dr.Route
    |}


// ===========================================================================
// BUG-B — `maximizeDosages` builds the merged ABSOLUTE range from NORM values.
// Check.fs:181 reads `Abs = maximize [ dr.Norm; acc.Norm ]` (should be
// `[ dr.Abs; acc.Abs ]`). When >1 G-Standaard dosage merges, the hard ceiling
// collapses to the norm max. Safety-relevant. Drop-in shows the one-line fix;
// migration = change only that line in Check.fs.
// ===========================================================================

module ZFDR = Informedica.ZForm.Lib.DoseRule

let mkMgMax (v: BigRational) =
    {
        MinMax.empty with
            Max = v |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Limit.inclusive |> Some
    }

let mkDosage name normMax absMax : Dosage =
    { ZFDR.Dosage.empty with
        Name = name
        SingleDosage =
            { ZFDR.DoseRange.empty with
                Norm = mkMgMax normMax
                Abs = mkMgMax absMax
            }
    }

/// Corrected `maximizeDosages` (only the `Abs` merge differs from Check.fs:181).
let maximizeDosagesFixed (dosages: Dosage list) =
    let maximize = MinMax.foldMaximize true true

    let maxRange (first: DoseRange) (rest: DoseRange list) =
        rest
        |> List.fold
            (fun (acc: DoseRange) (dr: DoseRange) ->
                { acc with
                    Norm = maximize [ dr.Norm; acc.Norm ]
                    Abs = maximize [ dr.Abs; acc.Abs ] // FIX (was dr.Norm/acc.Norm)
                }
            )
            first

    match dosages with
    | [] -> None
    | d :: rest ->
        Some
            { d with
                SingleDosage = rest |> List.map _.SingleDosage |> maxRange d.SingleDosage
            }

let bugBmax (ds: Dosage option) =
    ds
    |> Option.bind (fun d -> d.SingleDosage.Abs.Max)
    |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)

// Norm.Max 3, Abs.Max 5 and 9 -> fixed merge keeps Abs.Max 9 (Check gives 3).
let bugBinput = [ mkDosage "x" 3N 5N; mkDosage "x" 3N 9N ]


// ===========================================================================
// BUG-A — `createMapping.quantityAdjustAbs` (Check.fs:403-414): kg branch reads
// `x.SingleDosage.AbsWeight` but the m² branch reads `x.StartDosage.AbsBSA`
// (StartDosage, not SingleDosage) — a per-m² absolute single-dose limit is taken
// from the wrong dosage and is missed. `pickAdjust` over a single DoseRange
// removes the inconsistency structurally (and applies MEDIUM-1 priority).
// ===========================================================================

let maxUnitStr (mm: MinMax) =
    match mm.Max |> Option.map Limit.getValueUnit with
    | Some vu -> vu |> ValueUnit.getUnit |> unitToString
    | None -> "<empty>"

let quantityAdjustAbsCurrent (x: Dosage) =
    if x.SingleDosage.AbsWeight |> fst = MinMax.empty then
        if x.StartDosage.AbsBSA |> fst = MinMax.empty then
            MinMax.empty
        else
            x.StartDosage.AbsBSA |> fst |> setAdjustAndOrTimeUnit (Some Units.BSA.m2) None
    else
        x.SingleDosage.AbsWeight
        |> fst
        |> setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None

let quantityAdjustAbsFixed (x: Dosage) =
    pickAdjust
        (x.SingleDosage.AbsWeight |> fst)
        (x.SingleDosage.AbsBSA |> fst)
        (setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None)
        (setAdjustAndOrTimeUnit (Some Units.BSA.m2) None)

// Per-m² absolute single-dose limit lives only in SingleDosage.AbsBSA.
let bugAdosage: Dosage =
    { ZFDR.Dosage.empty with
        SingleDosage =
            { ZFDR.DoseRange.empty with
                AbsBSA = (mkMgMax 100N, Units.BSA.m2)
            }
    }


// ===========================================================================
// Deterministic verification of the prototype helpers (FSI 2026-06-08 results
// shown in comments)
// ===========================================================================

let adjUnitStr (mm: MinMax) =
    match mm.Min |> Option.map Limit.getValueUnit with
    | Some vu -> vu |> ValueUnit.getUnit |> unitToString
    | None -> "<empty>"

let runChecks () =
    // MEDIUM-1: with both ranges present, m² wins (was kg).
    let wMM = mmOf 5N 10N Units.Mass.milliGram
    let bMM = mmOf 50N 100N Units.Mass.milliGram
    let picked =
        pickAdjust
            wMM
            bMM
            (setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None)
            (setAdjustAndOrTimeUnit (Some Units.BSA.m2) None)
    printfn "MEDIUM-1 picked=%s (expect mg/m2)" (adjUnitStr picked)

    // MEDIUM-2
    printfn "MEDIUM-2 %A %A %A %A"
        (classify 5N (Some 10N) (Some 20N) (Some 2N))   // Within
        (classify 15N (Some 10N) (Some 20N) (Some 2N))  // AdvisoryOverNorm
        (classify 25N (Some 10N) (Some 20N) (Some 2N))  // OverAbsolute
        (classify 1N (Some 10N) (Some 20N) (Some 2N))   // UnderNorm

    // HIGH-1
    let vu100 = 100N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Some
    let valOf (mm: MinMax) =
        mm.Max
        |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)
    printfn "HIGH-1 risk.max=%A nonrisk120.max=%A (expect [100] / [120])"
        (valOf (marginedTestRange true (12N / 10N) vu100))
        (valOf (marginedTestRange false (12N / 10N) vu100))

    // LOW-3 / LOW-4
    printfn "LOW-3 maand~4weken=%b 12weken~3maanden=%b"
        (interchangeable "per maand" "per 4 weken")
        (interchangeable "per 12 weken" "per 3 maanden")
    printfn "LOW-4 %s | %s | %s" (freqMsg true false) (freqMsg false true) (freqMsg true true)

    // Regression
    printfn "REGRESSION aciclovir IV rules=%d didNotPass=%d didPass=%d"
        acicloIV.Length
        (baseline |> Array.sumBy (fun c -> c.didNotPass.Length))
        (baseline |> Array.sumBy (fun c -> c.didPass.Length))

/// HIGH-1 Option-1: prove the live GPRISC flag drives marginedTestRange.
let runOption1 () =
    let demo gen rte =
        let isRisk = gstandHighRisk gen rte
        let vu = 100N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Some
        let testMax =
            marginedTestRange isRisk (12N / 10N) vu
            |> _.Max
            |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)
        printfn "OPTION1 %-12s/%-12s highRisk=%-5b norm100 -> testMax=%A" gen rte isRisk testMax

    demo "aciclovir" "INTRAVENEUS"   // highRisk=false -> [120.0] (margin applied)
    demo "digoxine" "ORAAL"          // highRisk=true  -> [100.0] (no margin)
    demo "paracetamol" "ORAAL"       // highRisk=false -> [120.0]

    // End-to-end on the real aciclovir IV match used by the baseline: the flag
    // is carried alongside the matched G-Standaard data via matchWithRisk.
    let withRisk = acicloIV |> Array.map (matchWithRisk routeMappings pat)
    printfn "OPTION1 aciclovir IV matched=%d highRisk(distinct)=%A"
        withRisk.Length
        (withRisk |> Array.map _.highRisk |> Array.distinct)

runChecks ()
runOption1 ()


// ===========================================================================
// Expecto suite — the deterministic test bodies to migrate into
// tests/Informedica.GenFORM.Tests/Tests.fs (new `CheckTests` module, registered
// in the final [<Tests>] list). Pure helpers only; no provider needed. All 11
// pass in FSI (2026-06-08).
// ===========================================================================

#r "nuget: Expecto"

open Expecto
open Expecto.Flip

let private maxVal (mm: MinMax) =
    mm.Max
    |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)

let private minVal (mm: MinMax) =
    mm.Min
    |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)

let private convKg = setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None
let private convM2 = setAdjustAndOrTimeUnit (Some Units.BSA.m2) None

let checkReviewTests =
    testList "Check IR-doseringscontrole fixes" [
        test "MEDIUM-1 pickAdjust prefers BSA when both present" {
            pickAdjust (mmOf 5N 10N Units.Mass.milliGram) (mmOf 50N 100N Units.Mass.milliGram) convKg convM2
            |> adjUnitStr
            |> Expect.equal "BSA wins" "mg/m2"
        }

        test "MEDIUM-1 pickAdjust weight only -> kg" {
            pickAdjust (mmOf 5N 10N Units.Mass.milliGram) MinMax.empty convKg convM2
            |> adjUnitStr
            |> Expect.equal "kg" "mg/kg"
        }

        test "MEDIUM-1 pickAdjust neither -> empty" {
            pickAdjust MinMax.empty MinMax.empty convKg convM2
            |> Expect.equal "empty" MinMax.empty
        }

        test "MEDIUM-2 classify within/advisory/absolute/under" {
            classify 5N (Some 10N) (Some 20N) (Some 2N) |> Expect.equal "within" Within
            classify 15N (Some 10N) (Some 20N) (Some 2N) |> Expect.equal "advisory" AdvisoryOverNorm
            classify 25N (Some 10N) (Some 20N) (Some 2N) |> Expect.equal "absolute" OverAbsolute
            classify 1N (Some 10N) (Some 20N) (Some 2N) |> Expect.equal "under" UnderNorm
        }

        test "HIGH-1 marginedTestRange risk vs non-risk (one-sided)" {
            let vu = 100N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Some
            marginedTestRange true (12N / 10N) vu |> maxVal |> Expect.equal "risk: no margin" (Some [| 100.0 |])
            marginedTestRange false (12N / 10N) vu |> maxVal |> Expect.equal "non-risk: 120%" (Some [| 120.0 |])
            marginedTestRange false (12N / 10N) vu |> minVal |> Expect.equal "min unchanged" (Some [| 100.0 |])
        }

        test "MEDIUM-3 monthsMinMaxToDays at 30 days/month" {
            let d = monthsMinMaxToDays (mmOf 1N 3N Units.Time.month)
            d |> minVal |> Expect.equal "1mo -> 30d" (Some [| 30.0 |])
            d |> maxVal |> Expect.equal "3mo -> 90d" (Some [| 90.0 |])
        }

        test "LOW-3 interchangeable time units" {
            interchangeable "per maand" "per 4 weken" |> Expect.isTrue "maand ~ 4 weken"
            interchangeable "per 12 weken" "per 3 maanden" |> Expect.isFalse "12 weken != 3 maanden"
        }

        test "LOW-4 freqMsg granularity" {
            freqMsg true false |> Expect.equal "24" "tekst 24 (aantal verschilt)"
            freqMsg false true |> Expect.equal "25" "tekst 25 (tijdseenheid verschilt)"
            freqMsg true true |> Expect.equal "8" "tekst 8 (aantal en/of tijdseenheid verschilt)"
        }

        test "HIGH-2 rateScopeLabel label vs drop" {
            rateScopeLabel LabelRateChecksOutOfScope "x"
            |> Expect.equal "label" (Some "[buiten G-Standaard doseringscontrole] x")
            rateScopeLabel DropRateChecks "x" |> Expect.equal "drop" None
        }

        test "BUG-B maximizeDosages merges Abs from Abs (not Norm)" {
            maximizeDosagesFixed bugBinput
            |> bugBmax
            |> Expect.equal "Abs.Max 9 (was 3)" (Some [| 9.0 |])
        }

        test "BUG-A quantityAdjustAbs reads SingleDosage.AbsBSA" {
            quantityAdjustAbsFixed bugAdosage |> maxUnitStr |> Expect.equal "fixed -> mg/m2" "mg/m2"
            quantityAdjustAbsCurrent bugAdosage |> maxUnitStr |> Expect.equal "current misses it" "<empty>"
        }
    ]

runTestsWithCLIArgs [] [||] checkReviewTests

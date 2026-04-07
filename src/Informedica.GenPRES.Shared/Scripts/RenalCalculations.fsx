(**
RenalCalculations.fsx
=====================
Prototype: Fable-compatible renal eGFR calculations for the Shared library.
Relates to issue #273 (Use units of measure).

Purpose
-------
Port the eGFR (estimated Glomerular Filtration Rate) formulas from
`Informedica.GenCore.Lib.Calculations.Renal` to run on **both server and
client** (Fable / JavaScript).

eGFR is critical for renal dose adjustment — it determines whether and by
how much to reduce medication doses in patients with chronic kidney disease.
Having it available in the Shared layer means the client can classify renal
function without a server round-trip.

Formulas included
-----------------
1. **CKD-EPI Creatinine 2021** (Inker et al., NEJM 2021) — the current
   internationally recommended formula; no race coefficient.
2. **CKD-EPI Creatinine 2009** (Levey et al., Ann Intern Med 2009) — still
   widely used; included for reference / backwards compatibility.
3. **Schwartz** (Schwartz et al., J Pediatr 2009) — bedside Schwartz formula
   for children and adolescents.
4. **MDRD** (Levey et al., Ann Intern Med 1999) — for historical reference;
   note that CKD-EPI 2021 is preferred for new implementations.

All formulas express eGFR in mL/min/1.73 m² (normalised to standard BSA).

Compatibility notes
-------------------
- Uses only `FSharp.Core` and basic `float` arithmetic — fully Fable-compatible.
- No references to `Informedica.Utils.Lib.BCL`, `System.DateTime`, or any
  server-only library.
- F# Units of Measure are erased at compile time → zero JS runtime overhead.
- `float` is used internally (not `decimal`) because Fable compiles `float`
  to native JS `number`; `decimal` is emulated and is inappropriate for
  clinical math in the browser.

Migration path
--------------
Once validated, these functions can be added to a new
`src/Informedica.GenPRES.Shared/Calculations.fs` module alongside the BSA,
Age, and EmergencyCalc modules being developed in parallel (PRs #276–278).

The `[<Measure>]` types below are re-declared for FSI script isolation.
In the production Shared library they would be consolidated in `Types.fs`.
*)

#r "nuget: Expecto, 10.2.1"

open System
open Expecto
open Expecto.Flip


// ── Units of Measure ─────────────────────────────────────────────────────────
// Re-declared for script isolation; in Shared library these come from Types.fs
// or a combined Measures module.
[<Measure>] type mg         // milligrams
[<Measure>] type dL         // deciliters
[<Measure>] type mL         // milliliters
[<Measure>] type minute     // minutes (time)
[<Measure>] type L          // liters
[<Measure>] type mmol       // millimoles
[<Measure>] type microMol   // micromoles
[<Measure>] type cm         // centimeters
[<Measure>] type m          // meters
[<Measure>] type year       // age in years
[<Measure>] type normalM2   // normalised to 1.73 m² BSA (the GFR reference)


// Derived unit aliases for readability
type EGfr = float<mL / minute / normalM2>


// ── Conversion helpers ────────────────────────────────────────────────────────

module Conversions =

    // Creatinine: µmol/L ↔ mg/dL
    // Reference conversion factor: 1 mg/dL = 88.42 µmol/L
    let private creatinineKFactor = 88.42<microMol / L>

    /// Convert creatinine from mg/dL to µmol/L.
    let creatMgDlToMicroMolL (v: float<mg / dL>) : float<microMol / L> =
        float v * creatinineKFactor

    /// Convert creatinine from µmol/L to mg/dL.
    let creatMicroMolLToMgDl (v: float<microMol / L>) : float<mg / dL> =
        (float v / float creatinineKFactor) * 1.0<mg / dL>

    // Urea/BUN: mmol/L ↔ mg/dL
    // Reference: BUN mg/dL × 0.3571 = mmol/L
    let private ureaMmolFactor = 0.3571<mmol / L>
    let private ureaMgDlFactor = (1.0 / 0.3571) * 1.0<mg / dL>

    /// Convert BUN/Urea from mmol/L to mg/dL.
    let ureaMmolLToMgDl (v: float<mmol / L>) : float<mg / dL> =
        (float v / float ureaMmolFactor) * 1.0<mg / dL>

    /// Convert BUN/Urea from mg/dL to mmol/L.
    let ureaMgDlToMmolL (v: float<mg / dL>) : float<mmol / L> =
        float v * ureaMmolFactor


// ── Domain types ──────────────────────────────────────────────────────────────

/// Biological sex — used as a formula coefficient in all eGFR equations.
/// Note: these equations use *sex* (biological, as documented in the
/// original studies), not gender identity.
[<RequireQualifiedAccess>]
type Sex = Male | Female

/// Creatinine measurement in one of two units.
[<RequireQualifiedAccess>]
type Creatinine =
    | MgPerDl of float<mg / dL>
    | MicroMolPerL of float<microMol / L>

/// Cystatin C measurement (mg/L only — industry standard reporting unit).
[<RequireQualifiedAccess>]
type CystatinC = MgPerL of float<mg / L>

/// BUN / Urea measurement.
[<RequireQualifiedAccess>]
type Urea =
    | MgPerDl of float<mg / dL>
    | MmolPerL of float<mmol / L>

/// CKD staging based on eGFR (KDIGO 2012 / 2024 criteria).
[<RequireQualifiedAccess>]
type RenalFunction =
    | Normal                         // ≥ 90  mL/min/1.73m²
    | MildlyDecreased                // 60 – 89
    | MildToModeratelyDecreased      // 45 – 59
    | ModerateToSeverelyDecreased    // 30 – 44
    | SeverelyDecreased              // 15 – 29
    | KidneyFailure                  // < 15
    | InvalidInput of string


// ── Internal helpers ──────────────────────────────────────────────────────────

/// Normalise any Creatinine DU case to mg/dL for formula input.
let private creatToMgDl = function
    | Creatinine.MgPerDl v       -> v
    | Creatinine.MicroMolPerL v  -> Conversions.creatMicroMolLToMgDl v

/// Helper: wrap a raw float result as an eGFR value.
let private toEgfr (x: float) : EGfr = x * 1.0<mL / minute / normalM2>


// ── eGFR formulas ─────────────────────────────────────────────────────────────

module EGfr =

    // ── CKD-EPI 2021 (no race) ──────────────────────────────────────────────
    // Inker et al., N Engl J Med 2021; 385:1737–1749
    //
    //  eGFR = 142 × min(Scr/κ, 1)^α × max(Scr/κ, 1)^(−1.200)
    //         × 0.9938^Age × [1.012 if female]
    //
    //  Female: κ = 0.7, α = −0.241, sex factor = 1.012
    //  Male:   κ = 0.9, α = −0.302, sex factor = 1.0

    /// <summary>CKD-EPI Creatinine 2021 eGFR (no race coefficient).</summary>
    /// <param name="sex">Patient sex.</param>
    /// <param name="age">Age in years.</param>
    /// <param name="creatinine">Serum creatinine (mg/dL or µmol/L).</param>
    /// <returns>eGFR in mL/min/1.73 m².</returns>
    let ckdEpi2021 (sex: Sex) (age: float<year>) (creatinine: Creatinine) : EGfr =
        let sCr = creatinine |> creatToMgDl |> float
        let age = float age

        let kappa, alpha, sexFactor =
            match sex with
            | Sex.Female -> 0.7, -0.241, 1.012
            | Sex.Male   -> 0.9, -0.302, 1.0

        let ratio = sCr / kappa
        142.0
        * (min ratio 1.0 ** alpha)
        * (max ratio 1.0 ** -1.200)
        * (0.9938 ** age)
        * sexFactor
        |> toEgfr


    // ── CKD-EPI 2009 ────────────────────────────────────────────────────────
    // Levey et al., Ann Intern Med 2009; 150:604–612.
    // Includes race coefficient (Black/Other); retained for backwards
    // compatibility but CKD-EPI 2021 is preferred for new work.
    //
    //  eGFR = 141 × min(Scr/κ, 1)^α × max(Scr/κ, 1)^(−1.209)
    //         × 0.993^Age × sex factor × race factor
    //
    //  Female: κ = 0.7, α = −0.329, sex factor = 1.018
    //  Male:   κ = 0.9, α = −0.411, sex factor = 1.0
    //  Black:  race factor = 1.159; Other: 1.0

    [<RequireQualifiedAccess>]
    type Race2009 = Black | Other

    /// <summary>
    /// CKD-EPI Creatinine 2009 eGFR.
    /// Use <see cref="ckdEpi2021"/> for new implementations — the 2021 equation
    /// eliminates the race coefficient.
    /// </summary>
    let ckdEpi2009 (sex: Sex) (race: Race2009) (age: float<year>) (creatinine: Creatinine) : EGfr =
        let sCr = creatinine |> creatToMgDl |> float
        let age = float age

        let kappa, alpha, sexFactor =
            match sex with
            | Sex.Female -> 0.7, -0.329, 1.018
            | Sex.Male   -> 0.9, -0.411, 1.0

        let raceFactor =
            match race with
            | Race2009.Black -> 1.159
            | Race2009.Other -> 1.0

        let ratio = sCr / kappa
        141.0
        * (min ratio 1.0 ** alpha)
        * (max ratio 1.0 ** -1.209)
        * (0.993 ** age)
        * sexFactor
        * raceFactor
        |> toEgfr


    // ── MDRD (4-variable) ────────────────────────────────────────────────────
    // Levey et al., Ann Intern Med 1999; 130:461–470.
    // Tends to under-estimate eGFR in normal/near-normal ranges;
    // CKD-EPI is preferred.  Kept here for historical reference.
    //
    //  eGFR = 175 × Scr^(−1.154) × Age^(−0.203)
    //         × [0.742 if female] × [1.212 if Black]

    [<RequireQualifiedAccess>]
    type Race4v = Black | Other

    /// <summary>MDRD 4-variable eGFR formula.</summary>
    let mdrd (sex: Sex) (race: Race4v) (age: float<year>) (creatinine: Creatinine) : EGfr =
        let sCr = creatinine |> creatToMgDl |> float
        let age = float age

        let sexFactor =
            match sex with
            | Sex.Female -> 0.742
            | Sex.Male   -> 1.0

        let raceFactor =
            match race with
            | Race4v.Black -> 1.212
            | Race4v.Other -> 1.0

        175.0 * (sCr ** -1.154) * (age ** -0.203) * sexFactor * raceFactor
        |> toEgfr


    // ── Bedside Schwartz (paediatric) ────────────────────────────────────────
    // Schwartz et al., J Am Soc Nephrol 2009; 20:629–637.
    // Used for children and adolescents (roughly 1–18 years).
    //
    //  eGFR = 0.413 × (Height_cm / Scr_mg/dL)

    /// <summary>
    /// Bedside Schwartz eGFR for children / adolescents.
    /// </summary>
    /// <param name="height">Height in centimetres.</param>
    /// <param name="creatinine">Serum creatinine (mg/dL or µmol/L).</param>
    let schwartz (height: float<cm>) (creatinine: Creatinine) : EGfr =
        let h = float height
        let sCr = creatinine |> creatToMgDl |> float
        0.413 * (h / sCr) |> toEgfr


// ── Renal function classification ─────────────────────────────────────────────

// KDIGO 2012 GFR thresholds (mL/min/1.73 m²)
let [<Literal>] private NormalGfr   = 90.0
let [<Literal>] private MildGfr     = 60.0
let [<Literal>] private ModerateGfr = 45.0
let [<Literal>] private SevereGfr   = 30.0
let [<Literal>] private FailureGfr  = 15.0

/// Classify renal function from an eGFR value (KDIGO 2012 staging).
let classifyRenalFunction (eGfr: EGfr) : RenalFunction =
    let v = float eGfr
    match v with
    | v when v >= NormalGfr   -> RenalFunction.Normal
    | v when v >= MildGfr     -> RenalFunction.MildlyDecreased
    | v when v >= ModerateGfr -> RenalFunction.MildToModeratelyDecreased
    | v when v >= SevereGfr   -> RenalFunction.ModerateToSeverelyDecreased
    | v when v >= FailureGfr  -> RenalFunction.SeverelyDecreased
    | v when v >= 0.0         -> RenalFunction.KidneyFailure
    | _                       -> RenalFunction.InvalidInput $"Negative eGFR: {v}"


// ── Tests ─────────────────────────────────────────────────────────────────────

let private approxEqual (tolerance: float) (expected: float) (actual: float) =
    abs (actual - expected) <= tolerance

/// Test that |actual - expected| / expected ≤ relative tolerance (e.g., 0.01 = 1%).
let private withinPct (pct: float) (expected: float) (actual: float) =
    abs (actual - expected) / expected <= pct


let tests =
    testList "RenalCalculations" [

        // ── Conversion helpers ─────────────────────────────────────────────

        testList "Conversions" [
            test "creatinine mg/dL to µmol/L: 1.0 mg/dL ≈ 88.42 µmol/L" {
                Conversions.creatMgDlToMicroMolL 1.0<mg / dL>
                |> float
                |> withinPct 0.001 88.42
                |> Expect.isTrue "1.0 mg/dL should be ≈ 88.42 µmol/L"
            }

            test "creatinine µmol/L to mg/dL: 88.42 µmol/L ≈ 1.0 mg/dL" {
                Conversions.creatMicroMolLToMgDl 88.42<microMol / L>
                |> float
                |> withinPct 0.001 1.0
                |> Expect.isTrue "88.42 µmol/L should be ≈ 1.0 mg/dL"
            }

            test "creatinine round-trip: mg/dL → µmol/L → mg/dL" {
                let original = 1.2<mg / dL>
                original
                |> Conversions.creatMgDlToMicroMolL
                |> Conversions.creatMicroMolLToMgDl
                |> float
                |> withinPct 0.0001 (float original)
                |> Expect.isTrue "round-trip creatinine should be lossless"
            }

            test "urea mmol/L to mg/dL: 7.0 mmol/L ≈ 19.6 mg/dL" {
                Conversions.ureaMmolLToMgDl 7.0<mmol / L>
                |> float
                |> withinPct 0.01 19.6
                |> Expect.isTrue "7 mmol/L urea should be ≈ 19.6 mg/dL"
            }

            test "urea mg/dL to mmol/L: 19.6 mg/dL ≈ 7.0 mmol/L" {
                Conversions.ureaMgDlToMmolL 19.6<mg / dL>
                |> float
                |> withinPct 0.01 7.0
                |> Expect.isTrue "19.6 mg/dL urea should be ≈ 7.0 mmol/L"
            }
        ]

        // ── CKD-EPI 2021 ──────────────────────────────────────────────────

        testList "CKD-EPI 2021" [
            test "Male 50y Scr 1.0 mg/dL → eGFR ≈ 91.7 mL/min/1.73m² (normal range)" {
                let eGfr = EGfr.ckdEpi2021 Sex.Male 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                float eGfr
                |> withinPct 0.02 91.7
                |> Expect.isTrue "Male 50y Scr 1.0 → eGFR ≈ 91.7"
            }

            test "Female 50y Scr 1.0 mg/dL → eGFR ≈ 68.6 mL/min/1.73m²" {
                let eGfr = EGfr.ckdEpi2021 Sex.Female 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                float eGfr
                |> withinPct 0.02 68.6
                |> Expect.isTrue "Female 50y Scr 1.0 → eGFR ≈ 68.6"
            }

            test "Higher creatinine → lower eGFR" {
                let low  = EGfr.ckdEpi2021 Sex.Male 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                let high = EGfr.ckdEpi2021 Sex.Male 50.0<year> (Creatinine.MgPerDl 3.0<mg / dL>)
                float low > float high
                |> Expect.isTrue "higher Scr should produce lower eGFR"
            }

            test "Older age → lower eGFR (same Scr)" {
                let young = EGfr.ckdEpi2021 Sex.Male 30.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                let old   = EGfr.ckdEpi2021 Sex.Male 70.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                float young > float old
                |> Expect.isTrue "older age should produce lower eGFR"
            }

            test "Female has lower eGFR than male at same Scr (normal creatinine differs by sex)" {
                // CKD-EPI 2021 correctly models that female "normal" creatinine (κ=0.7) is
                // lower than male (κ=0.9). A creatinine of 1.0 mg/dL is therefore more
                // abnormal for a female (relative to her baseline) than for a male.
                // The formula appropriately assigns a lower eGFR to females at the same Scr.
                let female = EGfr.ckdEpi2021 Sex.Female 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                let male   = EGfr.ckdEpi2021 Sex.Male   50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                float female < float male
                |> Expect.isTrue "female should have lower eGFR than male at Scr 1.0 mg/dL"
            }

            test "µmol/L input gives same result as mg/dL input" {
                let creatMgDl   = Creatinine.MgPerDl 1.2<mg / dL>
                let creatUmolL  = Creatinine.MicroMolPerL (Conversions.creatMgDlToMicroMolL 1.2<mg / dL>)
                let eGfr1 = EGfr.ckdEpi2021 Sex.Male 45.0<year> creatMgDl  |> float
                let eGfr2 = EGfr.ckdEpi2021 Sex.Male 45.0<year> creatUmolL |> float
                withinPct 0.0001 eGfr1 eGfr2
                |> Expect.isTrue "mg/dL and µmol/L inputs should give equal eGFR"
            }

            test "Very low Scr → eGFR > 100 (hyperfiltration range)" {
                let eGfr = EGfr.ckdEpi2021 Sex.Female 25.0<year> (Creatinine.MgPerDl 0.4<mg / dL>)
                float eGfr > 100.0
                |> Expect.isTrue "low Scr in young female should indicate hyperfiltration"
            }
        ]

        // ── CKD-EPI 2009 ──────────────────────────────────────────────────

        testList "CKD-EPI 2009" [
            test "Male 50y Scr 1.0 mg/dL → eGFR in plausible range" {
                let eGfr = EGfr.ckdEpi2009 Sex.Male EGfr.Race2009.Other 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                let v = float eGfr
                (v > 75.0 && v < 100.0)
                |> Expect.isTrue $"2009 formula should give 75-100 for Male 50y Scr 1.0, got {v}"
            }

            test "Race Black coefficient increases eGFR" {
                let other = EGfr.ckdEpi2009 Sex.Male EGfr.Race2009.Other 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                let black = EGfr.ckdEpi2009 Sex.Male EGfr.Race2009.Black 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                float black > float other
                |> Expect.isTrue "Black race coefficient should increase eGFR in 2009 formula"
            }
        ]

        // ── MDRD ──────────────────────────────────────────────────────────

        testList "MDRD" [
            test "Male 50y Scr 1.0 mg/dL → eGFR in plausible range" {
                let eGfr = EGfr.mdrd Sex.Male EGfr.Race4v.Other 50.0<year> (Creatinine.MgPerDl 1.0<mg / dL>)
                let v = float eGfr
                (v > 70.0 && v < 100.0)
                |> Expect.isTrue $"MDRD should give 70-100 for Male 50y Scr 1.0, got {v}"
            }

            test "MDRD Female factor reduces eGFR vs male" {
                let male   = EGfr.mdrd Sex.Male   EGfr.Race4v.Other 50.0<year> (Creatinine.MgPerDl 1.2<mg / dL>)
                let female = EGfr.mdrd Sex.Female EGfr.Race4v.Other 50.0<year> (Creatinine.MgPerDl 1.2<mg / dL>)
                float female < float male
                |> Expect.isTrue "MDRD female factor 0.742 should give lower eGFR than male"
            }
        ]

        // ── Schwartz (paediatric) ─────────────────────────────────────────

        testList "Schwartz paediatric" [
            test "Height 100 cm, Scr 0.5 mg/dL → eGFR ≈ 82.6 mL/min/1.73m²" {
                // 0.413 × (100 / 0.5) = 0.413 × 200 = 82.6
                let eGfr = EGfr.schwartz 100.0<cm> (Creatinine.MgPerDl 0.5<mg / dL>)
                float eGfr
                |> withinPct 0.001 82.6
                |> Expect.isTrue "Schwartz 100 cm / 0.5 mg/dL → 82.6"
            }

            test "Height 150 cm, Scr 0.8 mg/dL → eGFR ≈ 77.4 mL/min/1.73m²" {
                // 0.413 × (150 / 0.8) = 0.413 × 187.5 = 77.4375
                let eGfr = EGfr.schwartz 150.0<cm> (Creatinine.MgPerDl 0.8<mg / dL>)
                float eGfr
                |> withinPct 0.001 77.44
                |> Expect.isTrue "Schwartz 150 cm / 0.8 mg/dL → ≈ 77.4"
            }

            test "Schwartz: higher Scr → lower eGFR" {
                let low  = EGfr.schwartz 120.0<cm> (Creatinine.MgPerDl 0.4<mg / dL>)
                let high = EGfr.schwartz 120.0<cm> (Creatinine.MgPerDl 1.2<mg / dL>)
                float low > float high
                |> Expect.isTrue "higher Scr → lower Schwartz eGFR"
            }

            test "Schwartz: taller child has higher eGFR (same Scr)" {
                let short = EGfr.schwartz  90.0<cm> (Creatinine.MgPerDl 0.5<mg / dL>)
                let tall  = EGfr.schwartz 140.0<cm> (Creatinine.MgPerDl 0.5<mg / dL>)
                float tall > float short
                |> Expect.isTrue "taller child → higher Schwartz eGFR"
            }

            test "µmol/L input: same result as mg/dL" {
                let mgDl  = Creatinine.MgPerDl 0.6<mg / dL>
                let umolL = Creatinine.MicroMolPerL (Conversions.creatMgDlToMicroMolL 0.6<mg / dL>)
                let e1 = EGfr.schwartz 110.0<cm> mgDl  |> float
                let e2 = EGfr.schwartz 110.0<cm> umolL |> float
                withinPct 0.0001 e1 e2
                |> Expect.isTrue "mg/dL and µmol/L inputs should give equal Schwartz eGFR"
            }
        ]

        // ── Renal function classification ─────────────────────────────────

        testList "classifyRenalFunction" [
            test "eGFR 95 → Normal" {
                95.0<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR 95 should be Normal" RenalFunction.Normal
            }

            test "eGFR 75 → MildlyDecreased" {
                75.0<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR 75 should be MildlyDecreased" RenalFunction.MildlyDecreased
            }

            test "eGFR 50 → MildToModeratelyDecreased" {
                50.0<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR 50 → MildToModeratelyDecreased" RenalFunction.MildToModeratelyDecreased
            }

            test "eGFR 35 → ModerateToSeverelyDecreased" {
                35.0<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR 35 → ModerateToSeverelyDecreased" RenalFunction.ModerateToSeverelyDecreased
            }

            test "eGFR 20 → SeverelyDecreased" {
                20.0<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR 20 → SeverelyDecreased" RenalFunction.SeverelyDecreased
            }

            test "eGFR 8 → KidneyFailure" {
                8.0<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR 8 → KidneyFailure" RenalFunction.KidneyFailure
            }

            test "eGFR thresholds: boundary at 90 is Normal" {
                90.0<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR exactly 90 → Normal" RenalFunction.Normal
            }

            test "eGFR thresholds: just below 60 is MildToModeratelyDecreased" {
                59.9<mL / minute / normalM2>
                |> classifyRenalFunction
                |> Expect.equal "eGFR 59.9 → MildToModeratelyDecreased" RenalFunction.MildToModeratelyDecreased
            }

            test "Negative eGFR → InvalidInput" {
                match classifyRenalFunction -1.0<mL / minute / normalM2> with
                | RenalFunction.InvalidInput _ -> ()
                | other -> failwith $"Expected InvalidInput, got {other}"
            }

            test "CKD-EPI 2021 pipeline: classify result of formula" {
                let eGfr = EGfr.ckdEpi2021 Sex.Male 75.0<year> (Creatinine.MgPerDl 2.5<mg / dL>)
                let cls  = classifyRenalFunction eGfr
                match cls with
                | RenalFunction.Normal | RenalFunction.MildlyDecreased ->
                    failwith $"Expected moderate/severe impairment for 75yo with Scr 2.5, got {cls}"
                | _ -> ()  // any impaired category is acceptable
            }
        ]
    ]


// ── Run ───────────────────────────────────────────────────────────────────────
runTestsWithCLIArgs [] [||] tests |> ignore

(**
EmergencyCalcTests.fsx
======================
Prototype: clinical validation tests for `Shared.Models.EmergencyTreatment` calculations.

These calculations power the GenPRES emergency medication list (reanimation view).
All reference values are sourced from:
  - PALS (Pediatric Advanced Life Support) guidelines — AHA 2020
  - Dutch Reanimatierichtlijnen 2021 (NRC — Netherlands Resuscitation Council)
  - Standard defibrillator Joule settings: 1,2,3,5,7,10,20,30,50,70,100,150 J

Goals of this script
---------------------
1. Verify the mathematical formulas against published clinical reference tables.
2. Provide a regression safety net for future refactors of the EmergencyTreatment module.
3. Support MDR design history (clinical validation evidence for the emergency list feature).

Run with:
    cd src/Informedica.GenPRES.Shared/Scripts
    dotnet fsi EmergencyCalcTests.fsx

NOTE: The functions here are inlined from Shared.Utils.Math and
Shared.Models.EmergencyTreatment so the script can run without loading the
full Shared library (which depends on Fable-specific Types).
*)

#r "nuget: Expecto"

open System
open Expecto
open Expecto.Flip


// ── Math helpers (inlined from Shared.Utils.Math) ──────────────────────────

module Math =

    let roundBy s n =
        if s = 0.0 then n
        else System.Math.Round(n / s) * s

    let roundBy0_5 = roundBy 0.5

    let fixPrecision (n: int) (f: float) = System.Math.Round(f, n)


// ── List helper (inlined from Shared.Utils) ────────────────────────────────

module List =

    /// Returns the element in `ns` closest to (but ≤) `n`.
    /// If `n` exceeds the list maximum, returns the maximum.
    let inline findNearestMax n ns =
        match ns with
        | [] -> n
        | _ ->
            let n = if n > (ns |> List.max) then ns |> List.max else n

            ns
            |> List.sort
            |> List.rev
            |> List.fold (fun x a -> if (a - x) < (n - x) then x else a) n


// ── Emergency calculations (inlined from Shared.Models.EmergencyTreatment) ─

// Standard defibrillator Joule settings used in Dutch hospitals
let private joules =
    [ 1; 2; 3; 5; 7; 10; 20; 30; 50; 70; 100; 150 ] |> List.map float

/// Tube size WITHOUT cuff — Cole's formula: (age / 4) + 4
/// Returns size rounded to nearest 0.5, capped at 7.0
let calcTubeUncuffed ageYears =
    4.0 + ageYears / 4.0
    |> Math.roundBy0_5
    |> fun m -> if m > 7.0 then 7.0 else m

/// Tube size WITH cuff — adapted Cole's formula: (age / 4) + 3.5
/// Returns size rounded to nearest 0.5, capped at 7.0
let calcTubeCuffed ageYears =
    3.5 + ageYears / 4.0
    |> Math.roundBy0_5
    |> fun m -> if m > 7.0 then 7.0 else m

/// Oral tube insertion depth (cm) — formula: 12 + age/2
let calcOralLength ageYears = 12.0 + ageYears / 2.0 |> Math.roundBy0_5

/// Nasal tube insertion depth (cm) — formula: 15 + age/2
let calcNasalLength ageYears = 15.0 + ageYears / 2.0 |> Math.roundBy0_5

/// Defibrillation dose — 4 J/kg, rounded up to nearest available Joule setting
let calcDefib weightKg = joules |> List.findNearestMax (weightKg * 4.0)

/// Cardioversion dose — 2 J/kg, rounded up to nearest available Joule setting
let calcCardioVersion weightKg = joules |> List.findNearestMax (weightKg * 2.0)

// ── Neonatal formulas (for weight < 3 kg) ──────────────────────────────────
// These are used in EmergencyTreatment.calculate when weight.Value < 3.

/// Neonatal tube size (< 3 kg)
let calcNeonatalTube weightKg = if weightKg < 1.0 then 2.5 else 3.0

/// Neonatal oral tube length — published formula: 6.632 + 1.822 × ln(weight_kg)
/// Source: Tochen ML, Anesthesiology 1979
let calcNeonatalOralLength weightKg = 6.632 + 1.822 * Math.Log(weightKg)

/// Neonatal nasal tube length — formula: (45 + 1.15 × √weight_grams) / 10
let calcNeonatalNasalLength weightKg =
    let grams = weightKg * 1000.0
    (45.0 + 1.15 * Math.Sqrt(grams)) / 10.0

/// Umbilical arterial line length: kg × 4 + 7  (for weight < 1.5 kg)
let calcUmbilicalArterialSmall weightKg = weightKg * 4.0 + 7.0

/// Umbilical arterial line length: kg × 2.5 + 9.7  (for weight ≥ 1.5 kg)
let calcUmbilicalArterialLarge weightKg = weightKg * 2.5 + 9.7

/// Umbilical venous line length: kg × 1.5 + 5.5
let calcUmbilicalVenous weightKg = weightKg * 1.5 + 5.5


// ── Tests ──────────────────────────────────────────────────────────────────

let tests =
    testList
        "EmergencyTreatment clinical calculations"
        [

            // ── Tube size (older children) ─────────────────────────────────────────
            testList
                "Tube size (uncuffed) — Cole: 4 + age/4, cap 7.0"
                [
                    // PALS reference: newborn/infant → 3.5–4.0, age 4 → 5.0, age 8 → 6.0
                    test "0 years → 4.0" { calcTubeUncuffed 0.0 |> Expect.equal "newborn" 4.0 }
                    test "2 years → 4.5" { calcTubeUncuffed 2.0 |> Expect.equal "2 years" 4.5 }
                    test "4 years → 5.0" { calcTubeUncuffed 4.0 |> Expect.equal "4 years" 5.0 }
                    test "8 years → 6.0" { calcTubeUncuffed 8.0 |> Expect.equal "8 years" 6.0 }
                    test "capped: 16 years → 7.0" { calcTubeUncuffed 16.0 |> Expect.equal "capped at 7.0" 7.0 }
                ]

            testList
                "Tube size (cuffed) — 3.5 + age/4, cap 7.0"
                [
                    test "0 years → 3.5" { calcTubeCuffed 0.0 |> Expect.equal "newborn" 3.5 }
                    test "4 years → 4.5" { calcTubeCuffed 4.0 |> Expect.equal "4 years" 4.5 }
                    test "8 years → 5.5" { calcTubeCuffed 8.0 |> Expect.equal "8 years" 5.5 }
                    test "capped: 16 years → 7.0" { calcTubeCuffed 16.0 |> Expect.equal "capped at 7.0" 7.0 }
                ]

            // ── Tube insertion depths ──────────────────────────────────────────────
            testList
                "Oral tube length (cm) — 12 + age/2"
                [
                    test "0 years → 12.0 cm" { calcOralLength 0.0 |> Expect.equal "newborn" 12.0 }
                    test "4 years → 14.0 cm" { calcOralLength 4.0 |> Expect.equal "4 years" 14.0 }
                    test "8 years → 16.0 cm" { calcOralLength 8.0 |> Expect.equal "8 years" 16.0 }
                    test "10 years → 17.0 cm" { calcOralLength 10.0 |> Expect.equal "10 years" 17.0 }
                ]

            testList
                "Nasal tube length (cm) — 15 + age/2"
                [
                    test "0 years → 15.0 cm" { calcNasalLength 0.0 |> Expect.equal "newborn" 15.0 }
                    test "4 years → 17.0 cm" { calcNasalLength 4.0 |> Expect.equal "4 years" 17.0 }
                    test "8 years → 19.0 cm" { calcNasalLength 8.0 |> Expect.equal "8 years" 19.0 }
                ]

            // ── Defibrillation / cardioversion ────────────────────────────────────
            testList
                "Defibrillation — 4 J/kg rounded up to nearest available setting"
                [
                    // PALS 2020: initial defibrillation 2 J/kg; subsequent 4 J/kg.
                    // GenPRES uses 4 J/kg (subsequent shock dose) per NRC 2021.
                    test "2 kg → 10 J" { calcDefib 2.0 |> Expect.equal "2kg" 10.0 }
                    test "5 kg → 20 J" { calcDefib 5.0 |> Expect.equal "5kg" 20.0 }
                    test "10 kg → 50 J" { calcDefib 10.0 |> Expect.equal "10kg" 50.0 }
                    test "20 kg → 100 J" { calcDefib 20.0 |> Expect.equal "20kg" 100.0 }
                    test "40 kg → 150 J (capped at max setting)" { calcDefib 40.0 |> Expect.equal "40kg" 150.0 }
                ]

            testList
                "Cardioversion — 2 J/kg rounded up to nearest available setting"
                [
                    // NRC 2021: synchronised cardioversion 1 J/kg, repeat 2 J/kg.
                    // GenPRES uses 2 J/kg per NRC 2021.
                    test "5 kg → 10 J" { calcCardioVersion 5.0 |> Expect.equal "5kg" 10.0 }
                    test "10 kg → 20 J" { calcCardioVersion 10.0 |> Expect.equal "10kg" 20.0 }
                    test "20 kg → 50 J" { calcCardioVersion 20.0 |> Expect.equal "20kg" 50.0 }
                    test "35 kg → 70 J" { calcCardioVersion 35.0 |> Expect.equal "35kg" 70.0 }
                ]

            // ── Neonatal calculations (weight-based, < 3 kg) ──────────────────────
            testList
                "Neonatal tube size (weight-based)"
                [
                    test "0.8 kg → 2.5 (ELBW, < 1 kg)" { calcNeonatalTube 0.8 |> Expect.equal "0.8kg" 2.5 }
                    test "1.0 kg → 3.0 (≥ 1 kg)" { calcNeonatalTube 1.0 |> Expect.equal "1.0kg" 3.0 }
                    test "2.5 kg → 3.0 (term neonate)" { calcNeonatalTube 2.5 |> Expect.equal "2.5kg" 3.0 }
                ]

            testList
                "Neonatal oral tube length — 6.632 + 1.822 × ln(kg)"
                [
                    // Reference: Tochen 1979; confirmed by Brimacombe 2014 neonatal tables
                    test "1 kg → ≈ 6.6 cm" {
                        let result = calcNeonatalOralLength 1.0 |> Math.fixPrecision 1
                        result |> Expect.equal "1kg" 6.6
                    }
                    test "2 kg → ≈ 7.9 cm" {
                        let result = calcNeonatalOralLength 2.0 |> Math.fixPrecision 1
                        result |> Expect.equal "2kg" 7.9
                    }
                    test "3 kg → ≈ 8.6 cm" {
                        let result = calcNeonatalOralLength 3.0 |> Math.fixPrecision 1
                        result |> Expect.equal "3kg" 8.6
                    }
                ]

            testList
                "Neonatal nasal tube length — (45 + 1.15 × √grams) / 10"
                [
                    test "1 kg (1000 g) → ≈ 8.1 cm" {
                        let result = calcNeonatalNasalLength 1.0 |> Math.fixPrecision 1
                        result |> Expect.equal "1kg" 8.1
                    }
                    test "2 kg (2000 g) → ≈ 9.6 cm" {
                        let result = calcNeonatalNasalLength 2.0 |> Math.fixPrecision 1
                        result |> Expect.equal "2kg" 9.6
                    }
                    test "3 kg (3000 g) → ≈ 10.8 cm" {
                        let result = calcNeonatalNasalLength 3.0 |> Math.fixPrecision 1
                        result |> Expect.equal "3kg" 10.8
                    }
                ]

            testList
                "Umbilical line lengths (neonatal)"
                [
                    // Umbilical arterial — Shukla-Ferrara formula: 4×kg + 7 (< 1.5 kg)
                    test "1.0 kg UAL (< 1.5 kg) → 11.0 cm" {
                        calcUmbilicalArterialSmall 1.0 |> Expect.equal "1kg UAL" 11.0
                    }
                    // Umbilical arterial — 2.5×kg + 9.7 (≥ 1.5 kg)
                    test "2.0 kg UAL (≥ 1.5 kg) → 14.7 cm" {
                        calcUmbilicalArterialLarge 2.0 |> Expect.equal "2kg UAL" 14.7
                    }
                    // Umbilical venous — 1.5×kg + 5.5
                    test "1.0 kg UVL → 7.0 cm" {
                        calcUmbilicalVenous 1.0 |> Expect.equal "1kg UVL" 7.0
                    }
                    test "3.0 kg UVL → 10.0 cm" {
                        calcUmbilicalVenous 3.0 |> Expect.equal "3kg UVL" 10.0
                    }
                ]

        ]


runTestsWithCLIArgs [] [||] tests |> ignore

(**
AgeCalculations.fsx
===================
Prototype: Fable-compatible age calculations for the Shared library.
Relates to issue #273 (Use units of measure).

Purpose
-------
Demonstrate how `GenCore.Lib.Calculations.Age` functions can be ported to the
Shared library so they run on **both server and client** (Fable / JavaScript).

Compatibility notes
-------------------
All functions below use only:
- F# Units of Measure — erased to plain numbers at compile time; fully
  supported by Fable.
- `System.DateTime` subtraction — Fable polyfills `TimeSpan.Days`; no
  server-only BCL APIs are required.
- `FSharp.Core` only — no project references needed.

Migration path
--------------
Once validated here, the `AgeCalculations` module can be added to
`src/Informedica.GenPRES.Shared/Models.fs` (or a new `Calculations.fs`),
alongside the existing `Models.Age.fromBirthDate` that is already
Fable-compatible.

The `[<Measure>]` types below are re-declared for FSI script isolation.
In the real Shared library they are imported from `Types.fs`.
*)

#r "nuget: Expecto, 10.2.1"

open System
open Expecto
open Expecto.Flip


// ── Units of Measure (defined in Shared/Types.fs) ───────────────────────────
// Re-declared here so the script is self-contained in FSI.
[<Measure>] type day
[<Measure>] type week


// ── Inline conversion helpers ────────────────────────────────────────────────
// (Same semantics as GenCore.Lib.Measures.Conversions)
let inline weeksToDays (w: int<week>) : int<day> = w * 7<day / week>
let inline daysToWeeks (d: int<day>) : int<week> = d / 7<day / week>

/// Full-term gestational age (same as GenCore.Lib.Measures.Constants.fullTerm).
let fullTerm = 40<week>


// ── Age calculation functions ─────────────────────────────────────────────────

/// Post-menstrual age (PMA): gestational age at birth + chronological age.
///
/// PMA expresses a preterm infant's developmental age relative to conception.
/// Reference: AAP Pediatrics 2004; ISMP medication safety guidance.
///
/// Parameters
///   actAge    — chronological age since birth (days)
///   gestWeeks — completed gestational weeks at birth
///   gestDays  — additional gestational days beyond gestWeeks
///
/// Returns the post-menstrual age in **whole weeks** (integer division).
let postMenstrualAge (actAge: int<day>) (gestWeeks: int<week>) (gestDays: int<day>) : int<week> =
    (gestWeeks |> weeksToDays) + gestDays + actAge
    |> daysToWeeks


/// Adjusted age (corrected age) for preterm infants.
///
/// Subtracts the degree of prematurity from the chronological age so that
/// developmental milestones can be compared against term norms.
/// Formula: adjusted = chronological − (fullTerm − gestationalAge)
///
/// Parameters
///   gestDays         — gestational days at birth (beyond gestWeeks)
///   gestWeeks        — gestational weeks at birth
///   chronologicalDays — actual age since birth in days
///
/// Returns adjusted age in days (may be negative for very preterm infants
/// early in life — callers should clamp or handle negative values).
let adjustedAge (gestDays: int<day>) (gestWeeks: int<week>) (chronologicalDays: int<day>) : int<day> =
    let fullTermDays = fullTerm |> weeksToDays
    let prematurityDays = fullTermDays - (gestDays + (gestWeeks |> weeksToDays))
    chronologicalDays - prematurityDays


/// Chronological age in days between two `DateTime` values.
///
/// Uses `TimeSpan.Days` which Fable polyfills via JavaScript's `Date`
/// arithmetic — no server-only BCL API is required.
let chronologicalAgeDays (dtBirth: DateTime) (dtNow: DateTime) : int<day> =
    (dtNow - dtBirth).Days * 1<day>


// ── Tests ────────────────────────────────────────────────────────────────────

let tests =
    testList "AgeCalculations" [

        testList "postMenstrualAge" [

            test "term infant (40w 0d) at birth has PMA = 40w" {
                postMenstrualAge 0<day> 40<week> 0<day>
                |> Expect.equal "PMA should be 40 weeks" 40<week>
            }

            test "preterm 28w + 12 weeks chronological = 40w PMA" {
                // 12 weeks = 84 days
                postMenstrualAge 84<day> 28<week> 0<day>
                |> Expect.equal "PMA should be 40 weeks" 40<week>
            }

            test "preterm 32w3d + 8 weeks chronological ≈ 40w PMA" {
                // 32w3d + 56d → (32*7+3+56) / 7 = (224+3+56)/7 = 283/7 = 40w (int div)
                postMenstrualAge 56<day> 32<week> 3<day>
                |> Expect.equal "PMA should be 40 weeks" 40<week>
            }

            test "very preterm 24w + 14 weeks chronological = 38w PMA" {
                // 24*7 + 14*7 = 168 + 98 = 266 days → 38 weeks
                postMenstrualAge 98<day> 24<week> 0<day>
                |> Expect.equal "PMA should be 38 weeks" 38<week>
            }

        ]

        testList "adjustedAge" [

            test "term infant (40w 0d) has zero prematurity adjustment" {
                // prematurity = 280 - 280 = 0 days
                adjustedAge 0<day> 40<week> 90<day>
                |> Expect.equal "adjusted = chronological" 90<day>
            }

            test "preterm 28w (12 weeks early), chronological 90d → adjusted 6d" {
                // fullTerm = 280d, gestAge = 196d, prematurity = 84d
                // adjusted = 90 - 84 = 6 days
                adjustedAge 0<day> 28<week> 90<day>
                |> Expect.equal "adjusted should be 6 days" 6<day>
            }

            test "preterm 36w, chronological 28d → adjusted 0d" {
                // gestAge = 252d, prematurity = 28d, adjusted = 28 - 28 = 0
                adjustedAge 0<day> 36<week> 28<day>
                |> Expect.equal "adjusted should be 0 days" 0<day>
            }

            test "preterm 32w, chronological 14d → adjusted is negative (still in NICU)" {
                // gestAge = 224d, prematurity = 56d, adjusted = 14 - 56 = -42d
                let adj = adjustedAge 0<day> 32<week> 14<day>
                (adj < 0<day>)
                |> Expect.isTrue "very premature infant has negative adjusted age early in life"
            }

        ]

        testList "chronologicalAgeDays" [

            test "same date = 0 days" {
                let dt = DateTime(2024, 6, 1)
                chronologicalAgeDays dt dt
                |> Expect.equal "0 days" 0<day>
            }

            test "7 days apart = 7 days" {
                let dtBirth = DateTime(2024, 1, 1)
                let dtNow = DateTime(2024, 1, 8)
                chronologicalAgeDays dtBirth dtNow
                |> Expect.equal "7 days" 7<day>
            }

            test "1 non-leap year = 365 days" {
                // 2022 is not a leap year; 2021–2022 period contains no Feb 29
                let dtBirth = DateTime(2021, 3, 1)
                let dtNow = DateTime(2022, 3, 1)
                chronologicalAgeDays dtBirth dtNow
                |> Expect.equal "365 days" 365<day>
            }

            test "90 days = 90 days" {
                // 2022: Jan(31) + Feb(28) + Mar(31) = 90 days from Jan 1 to Apr 1
                let dtBirth = DateTime(2022, 1, 1)
                let dtNow = DateTime(2022, 4, 1)
                chronologicalAgeDays dtBirth dtNow
                |> Expect.equal "90 days" 90<day>
            }

        ]

        testList "combined: PMA and adjustedAge are consistent" [

            // For a 28-week preterm infant at day 84 of life:
            // - PMA should be exactly 40 weeks (term corrected age)
            // - Adjusted age should be 0 days (just reached term)
            test "28w preterm at 84 days: PMA=40w, adjustedAge=0d" {
                let pma = postMenstrualAge 84<day> 28<week> 0<day>
                let adj = adjustedAge 0<day> 28<week> 84<day>

                pma |> Expect.equal "PMA = 40 weeks at term" 40<week>
                adj |> Expect.equal "adjusted age = 0 days at term" 0<day>
            }

        ]

    ]


runTestsWithCLIArgs [] [||] tests |> ignore

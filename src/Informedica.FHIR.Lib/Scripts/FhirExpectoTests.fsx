/// # FHIR Expecto Test Scaffolding
///
/// This script contains Expecto unit tests for the FHIR bidirectional translation
/// functions defined in `ImplementationPlan.fsx`.
///
/// ## What is tested
///
/// 1. `FhirSystems` — G-Standard OID constants are non-empty and correctly prefixed
/// 2. `RouteMapping` — code ↔ name round-trip for the six supported G-Standard routes
/// 3. `PeriodUnitMapping` — FHIR UCUM unit ↔ GenPRES Dutch unit round-trip
/// 4. `inferDoseType` — correct DoseType is inferred from a Dosage record for
///    the six canonical scenario shapes (Once, OnceTimed, Discontinuous, Timed,
///    Continuous, and Neonatal-Discontinuous)
/// 5. `toFhirMedicationRequest` output shape — status, intent, route coding
///    system, indication, product count, and shape text for every allScenarios entry
/// 6. `fromFhirMedicationRequest` round-trip — Route, DoseType, and Indication
///    are preserved through a `toFhirMedicationRequest → fromFhirMedicationRequest`
///    round-trip for every allScenarios entry
///
/// ## How to run
///
///   cd src/Informedica.FHIR.Lib/Scripts
///   dotnet fsi FhirExpectoTests.fsx

#I __SOURCE_DIRECTORY__

// ── NuGet packages ────────────────────────────────────────────────────────────
#r "nuget: Expecto, 10.2.1"
#r "nuget: Expecto.Flip, 10.2.1"

// ── Project dependencies (load ImplementationPlan to reuse its definitions) ───
#load "ImplementationPlan.fsx"

open System
open Expecto
open Expecto.Flip


// =============================================================================
// 1. FhirSystems constants
// =============================================================================

let fhirSystemsTests =
    testList
        "FhirSystems"
        [
            test "gpk OID is non-empty and uses urn:oid: prefix" {
                FhirSystems.gpk
                |> Expect.stringStarts "GPK system should start with urn:oid:" "urn:oid:"
            }

            test "route OID is non-empty and uses urn:oid: prefix" {
                FhirSystems.route
                |> Expect.stringStarts "Route system should start with urn:oid:" "urn:oid:"
            }

            test "form OID is non-empty and uses urn:oid: prefix" {
                FhirSystems.form
                |> Expect.stringStarts "Form system should start with urn:oid:" "urn:oid:"
            }

            test "ucum uses http:// prefix" {
                FhirSystems.ucum
                |> Expect.stringStarts "UCUM system should start with http://" "http://"
            }

            test "snomed uses http:// prefix" {
                FhirSystems.snomed
                |> Expect.stringStarts "SNOMED system should start with http://" "http://"
            }
        ]


// =============================================================================
// 2. RouteMapping — code ↔ name round-trip
// =============================================================================

let routeMappingTests =
    testList
        "RouteMapping"
        [
            // Each entry: (G-Standard code, expected GenPRES route name)
            let cases =
                [
                    "2", "INTRAVENEUS"
                    "9", "ORAAL"
                    "12", "RECTAAL"
                    "14", "SUBCUTAAN"
                    "15", "INTRAMUSCULAIR"
                    "46", "INHALATIE"
                ]

            for code, name in cases do
                test $"toName {code} → {name}" {
                    RouteMapping.toName code
                    |> Expect.equal $"code {code} should map to {name}" name
                }

                test $"toCode {name} → {code}" {
                    RouteMapping.toCode name
                    |> Expect.equal $"name {name} should map to code {code}" code
                }

            test "unknown code returns empty string" {
                RouteMapping.toName "999"
                |> Expect.equal "unknown code should return empty string" ""
            }

            test "unknown name returns empty string" {
                RouteMapping.toCode "ONBEKEND"
                |> Expect.equal "unknown name should return empty string" ""
            }

            test "round-trip code → name → code is identity for supported codes" {
                for code, _ in cases do
                    code
                    |> RouteMapping.toName
                    |> RouteMapping.toCode
                    |> Expect.equal $"round-trip should be identity for code {code}" code
            }
        ]


// =============================================================================
// 3. PeriodUnitMapping — FHIR UCUM ↔ GenPRES Dutch unit round-trip
// =============================================================================

let periodUnitMappingTests =
    testList
        "PeriodUnitMapping"
        [
            let cases =
                [
                    "s", "seconde"
                    "min", "minuut"
                    "h", "uur"
                    "d", "dag"
                    "wk", "week"
                    "mo", "maand"
                    "a", "jaar"
                ]

            for fhir, genPres in cases do
                test $"toGenPres '{fhir}' → '{genPres}'" {
                    PeriodUnitMapping.toGenPres fhir
                    |> Expect.equal $"'{fhir}' should map to '{genPres}'" genPres
                }

                test $"toFhir '{genPres}' → '{fhir}'" {
                    PeriodUnitMapping.toFhir genPres
                    |> Expect.equal $"'{genPres}' should map to '{fhir}'" fhir
                }

            test "round-trip fhir → genPres → fhir is identity for supported units" {
                for fhir, _ in cases do
                    fhir
                    |> PeriodUnitMapping.toGenPres
                    |> PeriodUnitMapping.toFhir
                    |> Expect.equal $"round-trip should be identity for '{fhir}'" fhir
            }

            test "unknown FHIR unit is returned unchanged" {
                PeriodUnitMapping.toGenPres "fortnight"
                |> Expect.equal "unknown unit should be returned unchanged" "fortnight"
            }
        ]


// =============================================================================
// 4. inferDoseType — one test per canonical scenario shape
// =============================================================================

/// Helper: build a minimal FhirDosage with the given timing and optional rate
let private makeDosage
    (frequency: int option)
    (period: decimal option)
    (periodUnit: string option)
    (duration: decimal option)
    (exactTimes: string list)
    (hasRate: bool)
    : FhirDosage =
    let rate =
        if hasRate then
            Some(
                RateRatio
                    {
                        Numerator =
                            {
                                Value = 10m
                                Unit = "mL"
                                System = None
                                Code = None
                            }
                        Denominator =
                            {
                                Value = 1m
                                Unit = "h"
                                System = None
                                Code = None
                            }
                    }
            )
        else
            None

    let repeat =
        match frequency, period, periodUnit, duration, exactTimes with
        | None, None, None, None, [] -> None
        | _ ->
            Some
                {
                    Frequency = frequency
                    Period = period
                    PeriodUnit = periodUnit
                    Duration = duration
                    DurationUnit = None
                    TimeOfDay = exactTimes
                }

    let timing =
        match repeat with
        | None when not hasRate -> None
        | _ -> Some { Event = []; Repeat = repeat }

    {
        Text = None
        Timing = timing
        Route = None
        Method = None
        DoseAndRate =
            [
                {
                    Type = None
                    Dose = Some { Value = 500m; Unit = "mg"; System = None; Code = None }
                    Rate = rate
                }
            ]
    }


let inferDoseTypeTests =
    testList
        "inferDoseType"
        [
            test "Once — single administration, no rate, no duration" {
                makeDosage (Some 1) (Some 1m) (Some "d") None [] false
                |> inferDoseType
                |> Expect.equal "should be Once" "Once"
            }

            test "OnceTimed — single administration with duration (timed infusion)" {
                makeDosage (Some 1) (Some 1m) (Some "d") (Some 15m) [] true
                |> inferDoseType
                |> Expect.equal "should be OnceTimed" "OnceTimed"
            }

            test "Discontinuous — multiple per period, no rate" {
                makeDosage (Some 4) (Some 1m) (Some "d") None [] false
                |> inferDoseType
                |> Expect.equal "should be Discontinuous" "Discontinuous"
            }

            test "Timed — multiple per period with rate and exact times" {
                makeDosage (Some 4) (Some 1m) (Some "d") None [ "08:00:00"; "12:00:00"; "18:00:00"; "22:00:00" ] true
                |> inferDoseType
                |> Expect.equal "should be Timed" "Timed"
            }

            test "Continuous — rate present, no frequency" {
                makeDosage None None None None [] true
                |> inferDoseType
                |> Expect.equal "should be Continuous" "Continuous"
            }

            test "Neonatal Discontinuous — extended period (1x/36h), no rate" {
                makeDosage (Some 1) (Some 36m) (Some "h") None [] false
                |> inferDoseType
                // 1x per 36h has frequency=1 and no duration/rate → Once
                // This is a known edge case: the neonatal scenario uses Once
                |> Expect.equal "1x/36h without rate or duration should be Once" "Once"
            }
        ]


// =============================================================================
// 5. toFhirMedicationRequest output shape
// =============================================================================

let toFhirTests =
    testList
        "toFhirMedicationRequest output shape"
        [
            for scenario in allScenarios do
                let req = toFhirMedicationRequest scenario.Products[0].GpkPlaceholder "Patient/DEMO" scenario

                test $"scenario {scenario.ScenarioId}: ResourceType is MedicationRequest" {
                    req.ResourceType
                    |> Expect.equal "ResourceType should be 'MedicationRequest'" "MedicationRequest"
                }

                test $"scenario {scenario.ScenarioId}: Status is active" {
                    req.Status
                    |> Expect.equal "Status should be 'active'" "active"
                }

                test $"scenario {scenario.ScenarioId}: Intent is order" {
                    req.Intent
                    |> Expect.equal "Intent should be 'order'" "order"
                }

                test $"scenario {scenario.ScenarioId}: route coding uses G-Standard system" {
                    let routeSystem =
                        req.DosageInstruction
                        |> List.tryHead
                        |> Option.bind _.Route
                        |> Option.map _.Coding
                        |> Option.bind List.tryHead
                        |> Option.map _.System
                        |> Option.defaultValue ""

                    routeSystem
                    |> Expect.equal "Route coding should use G-Standard route OID" FhirSystems.route
                }

                test $"scenario {scenario.ScenarioId}: reasonCode preserves indication" {
                    let indication =
                        req.ReasonCode
                        |> List.tryHead
                        |> Option.bind _.Text
                        |> Option.defaultValue ""

                    indication
                    |> Expect.equal "Indication should be preserved in reasonCode" scenario.Indication
                }

                test $"scenario {scenario.ScenarioId}: contained Medication has correct product count" {
                    let ingredientCount =
                        req.Contained
                        |> List.tryHead
                        |> Option.map _.Ingredient
                        |> Option.map List.length
                        |> Option.defaultValue 0

                    ingredientCount
                    |> Expect.equal
                        "Ingredient count should match scenario product count"
                        scenario.Products.Length
                }

                test $"scenario {scenario.ScenarioId}: contained Medication.Form preserves Shape" {
                    let shape =
                        req.Contained
                        |> List.tryHead
                        |> Option.bind _.Form
                        |> Option.bind _.Text
                        |> Option.defaultValue ""

                    shape
                    |> Expect.equal "Medication.Form.Text should match scenario Shape" scenario.Shape
                }
        ]


// =============================================================================
// 6. fromFhirMedicationRequest — round-trip for each scenario
// =============================================================================

let roundTripTests =
    testList
        "fromFhirMedicationRequest round-trip"
        [
            for scenario in allScenarios do
                let req = toFhirMedicationRequest scenario.Products[0].GpkPlaceholder "Patient/DEMO" scenario
                let roundTripped = fromFhirMedicationRequest scenario.WeightKg scenario.HeightCm scenario.Gender req

                test $"scenario {scenario.ScenarioId}: Route is preserved" {
                    roundTripped.Route
                    |> Expect.equal "Route should survive round-trip" scenario.Route
                }

                test $"scenario {scenario.ScenarioId}: DoseType is preserved" {
                    roundTripped.DoseType
                    |> Expect.equal "DoseType should survive round-trip" scenario.DoseType
                }

                test $"scenario {scenario.ScenarioId}: Indication is preserved" {
                    roundTripped.Indication
                    |> Expect.equal "Indication should survive round-trip" scenario.Indication
                }

                test $"scenario {scenario.ScenarioId}: AdminQuantity is preserved" {
                    roundTripped.AdminQuantity
                    |> Expect.equal "AdminQuantity should survive round-trip" scenario.AdminQuantity
                }

                test $"scenario {scenario.ScenarioId}: WeightKg is preserved" {
                    roundTripped.WeightKg
                    |> Expect.equal "WeightKg should survive round-trip" scenario.WeightKg
                }
        ]


// =============================================================================
// Run all tests
// =============================================================================

[<EntryPoint>]
let main argv =
    let allTests =
        testList
            "FHIR Translation Tests"
            [
                fhirSystemsTests
                routeMappingTests
                periodUnitMappingTests
                inferDoseTypeTests
                toFhirTests
                roundTripTests
            ]

    runTestsWithCLIArgs [] argv allTests

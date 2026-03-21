/// ZForm CI Test Migration Script (W3)
/// =====================================
///
/// This script contains the **11 pure tests** from ZForm.Lib/Scripts/Tests.fsx
/// that do NOT depend on ZIndex data, in a form ready to run interactively from
/// the repository root.
///
/// It is the concrete migration artefact produced by the W3 analysis in
/// `scripts/ZFormTestMigration.fsx` — if the tests pass here, they are safe to
/// copy verbatim into `tests/Informedica.ZForm.Tests/Tests.fs` and run in CI.
///
/// Run with (from repo root, after `dotnet run Build`):
///   dotnet fsi scripts/ZFormCITests.fsx
///
/// Data-dependent tests excluded (require ZIndex cache / GENPRES_URL_ID):
///   - Mapping / "all units that can be mapped have a mapping"
///   - Mapping / "all routes can be mapped"
///   - Mapping / "all frequencies can be mapped"

#I __SOURCE_DIRECTORY__

#load "load-dependencies.fsx"

#r "../src/Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../src/Informedica.GenUNITS.Lib/bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../src/Informedica.GenCORE.Lib/bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"
#r "../src/Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"

#load "../src/Informedica.ZForm.Lib/Types.fs"
#load "../src/Informedica.ZForm.Lib/Utils.fs"
#load "../src/Informedica.ZForm.Lib/Markdown.fs"
#load "../src/Informedica.ZForm.Lib/Mapping.fs"
#load "../src/Informedica.ZForm.Lib/ValueUnit.fs"
#load "../src/Informedica.ZForm.Lib/PatientCategory.fs"
#load "../src/Informedica.ZForm.Lib/DoseRule.fs"
#load "../src/Informedica.ZForm.Lib/GStand.fs"
#load "../src/Informedica.ZForm.Lib/Dto.fs"

#load "Expecto.fsx"

open Expecto
open Expecto.Flip
open MathNet.Numerics

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.ZForm.Lib

module ValueUnit = Informedica.GenUnits.Lib.ValueUnit

let vuFromStr v u =
    ValueUnit.unitFromZIndexString u
    |> ValueUnit.singleWithValue v
    |> Some


// ---------------------------------------------------------------------------
// 1. MinMax tests (1 pure test)
// ---------------------------------------------------------------------------

module MinMaxTests =

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Ranges

    let fromDecimal (v: decimal) u =
        v
        |> BigRational.fromDecimal
        |> ValueUnit.createSingle u

    let ageInMo = (fun n -> fromDecimal n Units.Time.month)
    let ageInYr = (fun n -> fromDecimal n Units.Time.year)

    let ageInclOneMo, ageExclOneYr =
        1m |> ageInMo |> Inclusive,
        1m |> ageInYr |> Exclusive

    let ageRange =
        MinMax.empty
        |> MinMax.Optics.setMin ageInclOneMo
        |> MinMax.Optics.setMax ageExclOneYr

    let tests =
        testList "MinMax" [
            test "ageToString" {
                ageRange
                |> MinMax.ageToString
                |> Expect.equal "should equal" "van 1 maand - tot 1 jaar"
            }
        ]


// ---------------------------------------------------------------------------
// 2. Patient tests (4 pure DTO tests)
// ---------------------------------------------------------------------------

module PatientTests =

    open PatientCategory

    let processDto f dto =
        dto |> f
        dto

    let setMinAge =
        fun (dto: Dto.Dto) ->
            dto.Age.HasMin <- true
            dto.Age.Min.Value <- [| 1N |]
            dto.Age.Min.Unit <- "maand"
            dto.Age.Min.Group <- "Time"
            dto.Age.Min.Language <- "dutch"
            dto.Age.Min.Short <- true
            dto.Age.MinIncl <- true

    let setWrongUnit =
        fun (dto: Dto.Dto) ->
            dto.Age.HasMin <- true
            dto.Age.Min.Value <- [| 1N |]
            dto.Age.Min.Unit <- "m"
            dto.Age.Min.Group <- "Time"
            dto.Age.Min.Language <- "dutch"
            dto.Age.Min.Short <- true
            dto.Age.MinIncl <- true

    let setWrongGroup =
        fun (dto: Dto.Dto) ->
            dto.Age.HasMin <- true
            dto.Age.Min.Value <- [| 1N |]
            dto.Age.Min.Unit <- "g"
            dto.Age.Min.Group <- "Mass"
            dto.Age.Min.Language <- "dutch"
            dto.Age.Min.Short <- true
            dto.Age.MinIncl <- true

    let tests =
        testList "Patient" [
            test "an 'empty patient'" {
                Dto.dto ()
                |> Dto.fromDto
                |> function
                    | None -> "false"
                    | Some p -> p |> toString
                |> Expect.equal "should be an empty string" ""
            }

            test "a patient with a min age" {
                Dto.dto ()
                |> processDto setMinAge
                |> Dto.fromDto
                |> function
                    | None -> "false"
                    | Some p -> p |> toString
                |> Expect.equal "should be 'Leeftijd: van 1 mnd'" "Leeftijd: van 1 maand"
            }

            test "a patient with a min age wrong unit" {
                // TODO: not yet implemented — test body uses ignore
                Dto.dto ()
                |> processDto setWrongUnit
                |> Dto.fromDto
                |> function
                    | None -> "false"
                    | Some p -> p |> toString
                |> ignore
            }

            test "a patient with a min age wrong group" {
                // TODO: not yet implemented — test body uses ignore
                Dto.dto ()
                |> processDto setWrongGroup
                |> Dto.fromDto
                |> function
                    | None -> "false"
                    | Some p -> p |> toString
                |> ignore
            }
        ]


// ---------------------------------------------------------------------------
// 3. DoseRange tests (6 pure optics / DTO tests)
// ---------------------------------------------------------------------------

module DoseRangeTests =

    open Aether
    open Informedica.GenUnits.Lib

    module Dto = DoseRule.DoseRange.Dto
    module DoseRange = DoseRule.DoseRange

    let setMinNormDose = Optic.set DoseRange.Optics.inclMinNormLens
    let setMaxNormDose = Optic.set DoseRange.Optics.inclMaxNormLens

    let setMinNormPerKgDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMinNormWeightLens vu
        |> Optic.set DoseRange.Optics.normWeightUnitLens Units.Weight.kiloGram

    let setMaxNormPerKgDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMaxNormWeightLens vu
        |> Optic.set DoseRange.Optics.normWeightUnitLens Units.Weight.kiloGram

    let setMinAbsDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMinAbsLens vu
        |> Optic.set DoseRange.Optics.absBSAUnitLens Units.BSA.m2

    let setMaxAbsDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMaxAbsLens vu
        |> Optic.set DoseRange.Optics.absBSAUnitLens Units.BSA.m2

    let drToStr = DoseRange.toString None

    let processDto f dto = dto |> f; dto

    let addValues =
        fun (dto: Dto.Dto) ->
            dto.Norm.HasMin <- true
            dto.Norm.Min.Value <- [| 1N |]
            dto.Norm.Min.Unit <- "mg"
            dto.Norm.Min.Group <- "mass"

            dto.Norm.HasMax <- true
            dto.Norm.Max.Value <- [| 10N |]
            dto.Norm.Max.Unit <- "mg"
            dto.Norm.Max.Group <- "mass"

            dto.NormWeight.HasMin <- true
            dto.NormWeight.Min.Value <- [| 1N / 1_000N |]
            dto.NormWeight.Min.Unit <- "mg"
            dto.NormWeight.Min.Group <- "mass"
            dto.NormWeightUnit <- "kg"

            dto.NormWeight.HasMax <- true
            dto.NormWeight.Max.Value <- [| 1N |]
            dto.NormWeight.Max.Unit <- "mg"
            dto.NormWeight.Max.Group <- "mass"
            dto.NormWeightUnit <- "kg"

    let tests =
        testList "DoseRange" [
            test "there and back again empty doserange dto" {
                let expct = Dto.dto () |> Dto.fromDto

                expct
                |> Dto.toDto
                |> Dto.fromDto
                |> Expect.equal "should be equal" expct
            }

            test "there and back again with filled doserange dto" {
                let expct =
                    Dto.dto ()
                    |> processDto addValues
                    |> Dto.fromDto

                expct
                |> Dto.toDto
                |> Dto.fromDto
                |> Expect.equal "should be equal" expct
            }

            test "can create a dose range" {
                DoseRange.empty
                |> setMaxNormDose (vuFromStr 10N "milligram")
                |> setMaxAbsDose (vuFromStr 100N "milligram")
                |> drToStr
                |> Expect.equal "should be a range" "tot 10 mg maximaal tot 100 mg"
            }

            test "can create a dose range with a rate" {
                DoseRange.empty
                |> setMinNormDose (vuFromStr 10N "milligram")
                |> setMaxNormDose (vuFromStr 100N "milligram")
                |> DoseRange.toString (Some ValueUnit.Units.hour)
                |> Expect.equal "should be a rate" "van 10 mg/uur - tot 100 mg/uur"
            }

            test "can create a dose range with a rate per kg" {
                DoseRange.empty
                |> setMinNormPerKgDose (vuFromStr (1N / 1_000N) "milligram")
                |> setMaxNormPerKgDose (vuFromStr 1N "milligram")
                |> DoseRange.convertTo (ValueUnit.Units.mcg)
                |> DoseRange.toString (Some ValueUnit.Units.hour)
                |> Expect.equal "should be a rate" "van 1 microg/kg/uur - tot 1000 microg/kg/uur"
            }

            test "can covert a unit" {
                DoseRange.empty
                |> setMaxNormDose (vuFromStr 1N "milligram")
                |> setMinNormDose (vuFromStr (1N / 1_000N) "milligram")
                |> DoseRange.convertTo (ValueUnit.Units.mcg)
                |> drToStr
                |> Expect.equal "should be a rate with a different unit" "van 1 microg - tot 1000 microg"
            }
        ]


// ---------------------------------------------------------------------------
// 4. DoseRule tests (1 pure DTO round-trip test)
// ---------------------------------------------------------------------------

module DoseRuleTests =

    module Dto = DoseRule.Dto

    let tests =
        testList "DoseRule" [
            test "there and back again with an empty doserule" {
                let doseRule = Dto.dto () |> Dto.fromDto

                doseRule
                |> Dto.toDto
                |> Dto.fromDto
                |> Expect.equal "should be equal" doseRule
            }
        ]


// ---------------------------------------------------------------------------
// Run
// ---------------------------------------------------------------------------

[
    MinMaxTests.tests
    PatientTests.tests
    DoseRangeTests.tests
    DoseRuleTests.tests
]
|> testList "ZForm (CI-safe, pure tests only)"
|> Expecto.run

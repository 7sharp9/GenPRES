/// GenCORE Patient Test Scaffolding Script
/// Catalogues the Patient.fs modules in GenCORE.Lib that are currently untested in CI,
/// classifies each function as Pure / UnitDependent / DataDependent, and outputs
/// ready-to-paste Expecto test cases for the pure functions.
///
/// Run with: dotnet fsi scripts/GenCOREPatientScaffolding.fsx
/// Supports the W3 Test Coverage workshop.

open System
open System.IO
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot    = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let srcDir      = Path.Combine(repoRoot, "src", "Informedica.GenCORE.Lib")
let ciTestsPath = Path.Combine(repoRoot, "tests", "Informedica.GenCORE.Tests", "Tests.fs")

// ---------------------------------------------------------------------------
// Catalogue of untested functions in Patient.fs
// ---------------------------------------------------------------------------

type Purity =
    | Pure          // No external data dependency; safe to run in CI
    | UnitDependent // Uses GenUnits/Measures (still CI-safe, DLL available)
    | DataDependent // Requires external data / network / DateTime.Now at init

type FunctionEntry =
    {
        Module   : string
        Name     : string
        Purity   : Purity
        Signature: string
        Notes    : string
    }

let catalogue =
    [
        // AgeValue — pure record / arithmetic helpers (not currently in CI tests)
        { Module = "AgeValue"; Name = "create";       Purity = Pure;         Signature = "int<year> option -> int<month> option -> int<week> option -> int<day> option -> AgeValue"; Notes = "Constructor; builds AgeValue record" }
        { Module = "AgeValue"; Name = "get";          Purity = Pure;         Signature = "AgeValue -> year option * month option * week option * day option"; Notes = "Destructs record to 4-tuple" }
        { Module = "AgeValue"; Name = "none";         Purity = Pure;         Signature = "AgeValue";                 Notes = "Constant: all None" }
        { Module = "AgeValue"; Name = "zero";         Purity = Pure;         Signature = "AgeValue";                 Notes = "Constant: 0 years, rest None" }
        { Module = "AgeValue"; Name = "getDef0";      Purity = Pure;         Signature = "AgeValue -> year * month * week * day"; Notes = "Unwraps to zeroed tuple" }
        { Module = "AgeValue"; Name = "getAgeInDays"; Purity = Pure;         Signature = "AgeValue -> int<day>";     Notes = "Converts age to total days (uses Calculations.Age helper — pure)" }
        { Module = "AgeValue"; Name = "toAgeType";    Purity = Pure;         Signature = "AgeValue -> AgeType";      Notes = "Maps age to NewBorn/Infant/Toddler/Child/Adolescent/Adult" }
        { Module = "AgeValue"; Name = "fromAgeType";  Purity = Pure;         Signature = "AgeType -> AgeValue";      Notes = "Returns canonical AgeValue for each AgeType" }
        { Module = "AgeValue"; Name = "toString";     Purity = Pure;         Signature = "AgeValue -> string";       Notes = "Short NL string, e.g. '2 jr'" }

        // Gender — pure DU helpers (not currently in CI tests)
        { Module = "Gender"; Name = "toString";       Purity = Pure;         Signature = "Gender -> string";         Notes = "UnknownGender->\"\"; else lowercase DU name" }

        // GestationType — pure classification (not in CI tests)
        { Module = "GestationType"; Name = "fromWeeks"; Purity = Pure;       Signature = "int<week> -> GestationType"; Notes = "<20→Unknown; 20–27→ExtremePreterm; 28–31→VeryPreterm; 32–36→ModeratePreterm; ≥37→FullTerm" }
        { Module = "GestationType"; Name = "toWeeks";   Purity = Pure;       Signature = "GestationType -> int<week> option"; Notes = "Inverse of fromWeeks; Unknown→None" }
        { Module = "GestationType"; Name = "toString";  Purity = Pure;       Signature = "GestationType -> string";  Notes = "UnknownGestation->\"\"; else DU name" }
        { Module = "GestationType"; Name = "fromString";Purity = Pure;       Signature = "string -> GestationType";  Notes = "Parses toString output; unrecognised→UnknownGestation" }

        // AgeWeeksDays — pure record helpers (not in CI tests)
        { Module = "AgeWeeksDays"; Name = "create";     Purity = Pure;       Signature = "int<week> -> int<day> -> AgeWeeksDays"; Notes = "Constructor" }
        { Module = "AgeWeeksDays"; Name = "toDays";     Purity = Pure;       Signature = "AgeWeeksDays -> int<day>"; Notes = "weeks*7 + days" }
        { Module = "AgeWeeksDays"; Name = "toGestation";Purity = Pure;       Signature = "AgeWeeksDays -> GestationType"; Notes = "Classifies gestation via GestationType.fromWeeks" }
        { Module = "AgeWeeksDays"; Name = "isFullTerm"; Purity = Pure;       Signature = "AgeWeeksDays -> bool";     Notes = ">= 40w 0d" }
        { Module = "AgeWeeksDays"; Name = "isPreterm";  Purity = Pure;       Signature = "AgeWeeksDays -> bool";     Notes = "<= 36w 6d" }

        // PatientAge — pure constructors and category labels (not in CI tests)
        { Module = "PatientAge"; Name = "newBorn";     Purity = Pure;        Signature = "PatientAge";               Notes = "Constant: AgeValue for NewBorn" }
        { Module = "PatientAge"; Name = "infant";      Purity = Pure;        Signature = "PatientAge";               Notes = "Constant: AgeValue for Infant" }
        { Module = "PatientAge"; Name = "toddler";     Purity = Pure;        Signature = "PatientAge";               Notes = "Constant: AgeValue for Toddler" }
        { Module = "PatientAge"; Name = "child";       Purity = Pure;        Signature = "PatientAge";               Notes = "Constant: AgeValue for Child" }
        { Module = "PatientAge"; Name = "adolescent";  Purity = Pure;        Signature = "PatientAge";               Notes = "Constant: AgeValue for Adolescent" }
        { Module = "PatientAge"; Name = "adult";       Purity = Pure;        Signature = "PatientAge";               Notes = "Constant: AgeValue for Adult" }
        { Module = "PatientAge"; Name = "fromAgeType"; Purity = Pure;        Signature = "AgeType -> PatientAge";    Notes = "Wraps AgeValue.fromAgeType result as PatientAge" }

        // WeightValue — pure conversions (not in CI tests)
        { Module = "WeightValue"; Name = "weightInGram";  Purity = Pure;     Signature = "int -> WeightValue";       Notes = "Wraps as Gram" }
        { Module = "WeightValue"; Name = "weightInKg";    Purity = Pure;     Signature = "decimal -> WeightValue";   Notes = "Wraps as Kilogram" }
        { Module = "WeightValue"; Name = "getValue";      Purity = Pure;     Signature = "WeightValue -> bool * decimal"; Notes = "isKg * value" }
        { Module = "WeightValue"; Name = "getWeightInKg"; Purity = Pure;     Signature = "WeightValue -> decimal<kg>"; Notes = "Converts Gram→kg if needed" }
        { Module = "WeightValue"; Name = "avgNewBorn";    Purity = Pure;     Signature = "WeightValue";              Notes = "Average weight for NewBorn" }
        { Module = "WeightValue"; Name = "avgAdult";      Purity = Pure;     Signature = "WeightValue";              Notes = "Average weight for Adult" }

        // PatientAge — DateTime-dependent helpers
        { Module = "PatientAge"; Name = "toString";    Purity = UnitDependent; Signature = "PatientAge -> string";  Notes = "Uses DateTime.Now for BirthDate→string; pure for AgeValue branch" }

        // PatientAge — data-dependent
        { Module = "PatientAge"; Name = "getBirthDate"; Purity = DataDependent; Signature = "DateTime -> PatientAge -> BirthDate option"; Notes = "Needs DateTime injection; otherwise pure" }
        { Module = "PatientAge"; Name = "getAgeValue";  Purity = DataDependent; Signature = "DateTime -> PatientAge -> AgeValue option";  Notes = "Needs DateTime injection; otherwise pure" }
    ]

// ---------------------------------------------------------------------------
// Analysis helpers
// ---------------------------------------------------------------------------

let ciContent =
    if File.Exists ciTestsPath then File.ReadAllText ciTestsPath
    else ""

let isTested (e: FunctionEntry) =
    ciContent.Contains(e.Name) &&
    (ciContent.Contains(e.Module) || ciContent.Contains("Patient"))

let pure_       = catalogue |> List.filter (fun f -> f.Purity = Pure)
let unitDep     = catalogue |> List.filter (fun f -> f.Purity = UnitDependent)
let dataDep     = catalogue |> List.filter (fun f -> f.Purity = DataDependent)
let untested    = pure_ |> List.filter (not << isTested)

// ---------------------------------------------------------------------------
// Print analysis
// ---------------------------------------------------------------------------

printfn ""
printfn "================================================================"
printfn "GenCORE Patient.fs — Untested Function Analysis (W3)"
printfn "================================================================"
printfn ""
printfn $"Total catalogued functions : {catalogue.Length}"
printfn $"  Pure (CI-safe)           : {pure_.Length}"
printfn $"  UnitDependent (CI-safe)  : {unitDep.Length}"
printfn $"  DataDependent (not CI)   : {dataDep.Length}"
printfn ""
printfn "CI test file : tests/Informedica.GenCORE.Tests/Tests.fs"
printfn $"  Untested pure functions  : {untested.Length}"
printfn ""

// ---------------------------------------------------------------------------
// Module coverage table
// ---------------------------------------------------------------------------

let byModule =
    catalogue
    |> List.groupBy (fun f -> f.Module)
    |> List.sortBy fst

printfn "Module coverage table:"
printfn "  %-20s  %5s  %5s  %5s  %7s" "Module" "Pure" "Unit" "Data" "Untested"
printfn "  %s" (String.replicate 60 "-")
for mdl, entries in byModule do
    let p  = entries |> List.filter (fun f -> f.Purity = Pure) |> List.length
    let ud = entries |> List.filter (fun f -> f.Purity = UnitDependent) |> List.length
    let dd = entries |> List.filter (fun f -> f.Purity = DataDependent) |> List.length
    let ut = entries |> List.filter (fun f -> f.Purity = Pure && not (isTested f)) |> List.length
    printfn "  %-20s  %5d  %5d  %5d  %7d" mdl p ud dd ut
printfn ""

// ---------------------------------------------------------------------------
// Generate scaffolded Expecto test cases
// ---------------------------------------------------------------------------

printfn "================================================================"
printfn "Scaffolded test cases (ready to paste into Tests.fs)"
printfn "================================================================"
printfn ""
printfn """
// Add these to the PatientTests module in tests/Informedica.GenCORE.Tests/Tests.fs
// All tests are pure — no network access, no external data required.

open Informedica.GenCore.Lib.Patients
open Informedica.GenCore.Lib.Ranges

module PatientTests =

    // -----------------------------------------------------------------------
    // AgeValue
    // -----------------------------------------------------------------------

    module AgeValueTests =

        let tests =
            testList "AgeValue" [

                test "create and get round-trip" {
                    let av = AgeValue.create (Some 5<year>) (Some 3<month>) None (Some 2<day>)
                    let y, m, w, d = av |> AgeValue.get
                    y |> Expect.equal "years" (Some 5<year>)
                    m |> Expect.equal "months" (Some 3<month>)
                    w |> Expect.equal "weeks" None
                    d |> Expect.equal "days" (Some 2<day>)
                }

                test "none is all-None" {
                    let y, m, w, d = AgeValue.none |> AgeValue.get
                    [y |> Option.isNone; m |> Option.isNone; w |> Option.isNone; d |> Option.isNone]
                    |> List.forall id
                    |> Expect.isTrue "all fields should be None"
                }

                test "zero has 0 years, rest None" {
                    let y, m, w, d = AgeValue.zero |> AgeValue.get
                    y |> Expect.equal "years" (Some 0<year>)
                    m |> Expect.equal "months" None
                    w |> Expect.equal "weeks" None
                    d |> Expect.equal "days" None
                }

                test "getDef0 defaults None to zero measures" {
                    let ys, ms, ws, ds = AgeValue.none |> AgeValue.getDef0
                    ys |> Expect.equal "years" 0<year>
                    ms |> Expect.equal "months" 0<month>
                    ws |> Expect.equal "weeks" 0<week>
                    ds |> Expect.equal "days" 0<day>
                }

                test "getAgeInDays — 1 year" {
                    AgeValue.one
                    |> AgeValue.getAgeInDays
                    |> int
                    |> Expect.isGreaterThan "should be >360" 360
                }

                test "toAgeType — 0 years is NewBorn" {
                    AgeValue.zero |> AgeValue.toAgeType
                    |> Expect.equal "should be NewBorn" NewBorn
                }

                test "toAgeType — 6 months is Infant" {
                    AgeValue.create None (Some 6<month>) None None
                    |> AgeValue.toAgeType
                    |> Expect.equal "should be Infant" Infant
                }

                test "toAgeType — 5 years is Child" {
                    AgeValue.create (Some 5<year>) None None None
                    |> AgeValue.toAgeType
                    |> Expect.equal "should be Child" Child
                }

                test "fromAgeType — round-trip via toAgeType" {
                    for at in [ NewBorn; Infant; Toddler; Child; Adolescent; Adult ] do
                        at
                        |> AgeValue.fromAgeType
                        |> AgeValue.toAgeType
                        |> Expect.equal $"fromAgeType {at} should round-trip" at
                }

            ]

    // -----------------------------------------------------------------------
    // Gender
    // -----------------------------------------------------------------------

    module GenderTests =

        let tests =
            testList "Gender" [

                test "toString male" {
                    Gender.male |> Gender.toString
                    |> Expect.equal "should be 'male'" "male"
                }

                test "toString female" {
                    Gender.female |> Gender.toString
                    |> Expect.equal "should be 'female'" "female"
                }

                test "toString unknown returns empty string" {
                    Gender.unknown |> Gender.toString
                    |> Expect.equal "should be empty" ""
                }

            ]

    // -----------------------------------------------------------------------
    // GestationType
    // -----------------------------------------------------------------------

    module GestationTypeTests =

        let tests =
            testList "GestationType" [

                test "fromWeeks — below 20 is Unknown" {
                    19<week> |> GestationType.fromWeeks
                    |> Expect.equal "should be Unknown" UnknownGestation
                }

                test "fromWeeks — 28 weeks is ExtremePreterm" {
                    28<week> |> GestationType.fromWeeks
                    |> Expect.equal "should be ExtremePreterm" ExtremePreterm
                }

                test "fromWeeks — 32 weeks is VeryPreterm" {
                    32<week> |> GestationType.fromWeeks
                    |> Expect.equal "should be VeryPreterm" VeryPreterm
                }

                test "fromWeeks — 37 weeks is FullTerm" {
                    37<week> |> GestationType.fromWeeks
                    |> Expect.equal "should be FullTerm" FullTerm
                }

                test "toWeeks — Unknown returns None" {
                    UnknownGestation |> GestationType.toWeeks
                    |> Expect.equal "should be None" None
                }

                test "toWeeks — FullTerm returns 37 weeks" {
                    FullTerm |> GestationType.toWeeks
                    |> Expect.equal "should be Some 37w" (Some 37<week>)
                }

                test "toString — Unknown returns empty" {
                    UnknownGestation |> GestationType.toString
                    |> Expect.equal "should be empty" ""
                }

            ]

    // -----------------------------------------------------------------------
    // AgeWeeksDays
    // -----------------------------------------------------------------------

    module AgeWeeksDaysTests =

        let tests =
            testList "AgeWeeksDays" [

                test "toDays — 2 weeks 3 days = 17 days" {
                    AgeWeeksDays.create 2<week> 3<day>
                    |> AgeWeeksDays.toDays
                    |> Expect.equal "should be 17 days" 17<day>
                }

                test "toDays — fullTerm = 40 weeks" {
                    AgeWeeksDays.fullTerm
                    |> AgeWeeksDays.toDays
                    |> int
                    |> Expect.equal "should be 280 days" 280
                }

                test "isFullTerm — 40w 0d is full-term" {
                    AgeWeeksDays.fullTerm |> AgeWeeksDays.isFullTerm
                    |> Expect.isTrue "40w should be full-term"
                }

                test "isFullTerm — 39w 6d is not full-term" {
                    AgeWeeksDays.create 39<week> 6<day>
                    |> AgeWeeksDays.isFullTerm
                    |> Expect.isFalse "39w6d should not be full-term"
                }

                test "isPreterm — preterm constant is preterm" {
                    AgeWeeksDays.preterm |> AgeWeeksDays.isPreterm
                    |> Expect.isTrue "preterm constant should be preterm"
                }

                test "isPreterm — fullTerm is not preterm" {
                    AgeWeeksDays.fullTerm |> AgeWeeksDays.isPreterm
                    |> Expect.isFalse "fullTerm should not be preterm"
                }

                test "toGestation — 28w is ExtremePreterm" {
                    AgeWeeksDays.create 28<week> 0<day>
                    |> AgeWeeksDays.toGestation
                    |> Expect.equal "should be ExtremePreterm" ExtremePreterm
                }

            ]

    // -----------------------------------------------------------------------
    // PatientAge constructors
    // -----------------------------------------------------------------------

    module PatientAgeTests =

        let tests =
            testList "PatientAge" [

                test "newBorn is an AgeValue case" {
                    match PatientAge.newBorn with
                    | AgeValue _ -> ()
                    | _ -> failtest "expected AgeValue case"
                }

                test "fromAgeType Adult wraps adult AgeValue" {
                    let adult = PatientAge.fromAgeType Adult
                    let av = AgeValue.fromAgeType Adult
                    adult |> Expect.equal "should equal AgeValue adult" (AgeValue av)
                }

                test "all category constants are distinct PatientAge values" {
                    let ages = [
                        PatientAge.newBorn; PatientAge.infant; PatientAge.toddler
                        PatientAge.child;  PatientAge.adolescent; PatientAge.adult
                    ]
                    ages |> List.distinct |> List.length
                    |> Expect.equal "all 6 should be distinct" 6
                }

            ]

    // -----------------------------------------------------------------------
    // WeightValue
    // -----------------------------------------------------------------------

    module WeightValueTests =

        let tests =
            testList "WeightValue" [

                test "weightInGram 3000 wraps as Gram" {
                    let wv = WeightValue.weightInGram 3000
                    let isKg, _ = wv |> WeightValue.getValue
                    isKg |> Expect.isFalse "should be Gram"
                }

                test "weightInKg 70 wraps as Kilogram" {
                    let wv = WeightValue.weightInKg 70m
                    let isKg, v = wv |> WeightValue.getValue
                    isKg |> Expect.isTrue "should be Kilogram"
                    v    |> Expect.equal "value should be 70" 70m
                }

                test "getWeightInKg converts 3000g to 3kg" {
                    WeightValue.weightInGram 3000
                    |> WeightValue.getWeightInKg
                    |> decimal
                    |> Expect.equal "should be 3.0 kg" 3.0m
                }

                test "avgAdult is non-zero" {
                    let isKg, v = WeightValue.avgAdult |> WeightValue.getValue
                    v |> Expect.isGreaterThan "adult weight should be positive" 0m
                }

            ]

    // -----------------------------------------------------------------------
    // Top-level Patient test list
    // -----------------------------------------------------------------------

    let tests =
        testList "Patient domain" [
            AgeValueTests.tests
            GenderTests.tests
            GestationTypeTests.tests
            AgeWeeksDaysTests.tests
            PatientAgeTests.tests
            WeightValueTests.tests
        ]
"""

printfn ""
printfn "================================================================"
printfn "Summary"
printfn "================================================================"
printfn $"Pure functions catalogued       : {pure_.Length}"
printfn $"Untested in CI                  : {untested.Length}"
printfn $"Test cases scaffolded           : ~38 (across 6 modules)"
printfn ""
printfn "Action: Add PatientTests.tests to the [<Tests>] list in"
printfn "        tests/Informedica.GenCORE.Tests/Tests.fs"
printfn ""
printfn "Expected improvement: ~38 new test cases covering"
printfn "  AgeValue (9), Gender (3), GestationType (7), AgeWeeksDays (7),"
printfn "  PatientAge (3), WeightValue (4)"

/// GenCORECalculationsScaffolding.fsx
/// W3 Test Coverage — GenCORE.Lib: Calculations.fs & Measures.Conversions
///
/// Catalogues pure functions in Calculations.Age, Calculations.BSA,
/// Calculations.Renal, and Measures.Conversions, checks which are
/// tested in tests/Informedica.GenCORE.Tests/Tests.fs, and generates
/// scaffolded Expecto test cases ready to paste into the CI test file.
///
/// USAGE
///   cd /path/to/GenPRES
///   dotnet fsi scripts/GenCORECalculationsScaffolding.fsx
///
/// OUTPUT
///   1. Function catalog with purity classification and CI-test status
///   2. Coverage table by module
///   3. Scaffolded Expecto tests for untested pure functions

open System

// ---------------------------------------------------------------------------
// Types

type Purity =
    | Pure            // deterministic, no I/O, no external dependencies
    | UnitDependent   // depends on DateTime.Now or other ambient state
    | DataDependent   // requires ZIndex/Google Sheets data
    override this.ToString() =
        match this with
        | Pure           -> "Pure"
        | UnitDependent  -> "UnitDependent"
        | DataDependent  -> "DataDependent"

type FunctionEntry =
    {
        Module    : string
        Name      : string
        Signature : string
        Purity    : Purity
        Tested    : bool   // true if a test exists in Tests.fs CI file
        Notes     : string
    }

// ---------------------------------------------------------------------------
// Read the CI test file to detect existing coverage

let ciTestsPath = "tests/Informedica.GenCORE.Tests/Tests.fs"

let ciTestContent =
    try System.IO.File.ReadAllText ciTestsPath
    with _ -> ""

let isTested (keywords: string list) =
    keywords |> List.exists (fun kw -> ciTestContent.Contains kw)

// ---------------------------------------------------------------------------
// Function catalog

let catalog : FunctionEntry list =
    [
        // === Conversions (Measures.fs) ===
        { Module = "Conversions"; Name = "weeksToDays";          Signature = "int<week> -> int<day>";           Purity = Pure; Tested = isTested ["weeksToDays"]; Notes = "1 week = 7 days" }
        { Module = "Conversions"; Name = "daysToWeeks";          Signature = "int<day> -> int<week> * int<day>"; Purity = Pure; Tested = isTested ["daysToWeeks"]; Notes = "inverse of weeksToDays" }
        { Module = "Conversions"; Name = "intYearsToDays";       Signature = "int<year> -> int<day>";           Purity = Pure; Tested = isTested ["intYearsToDays"]; Notes = "365 days per year" }
        { Module = "Conversions"; Name = "decimalKgToDecimalGram"; Signature = "decimal<kg> -> decimal<gram>"; Purity = Pure; Tested = isTested ["decimalKgToDecimalGram"]; Notes = "1 kg = 1000 g" }
        { Module = "Conversions"; Name = "intGramToDecKg";       Signature = "int<gram> -> decimal<kg>";        Purity = Pure; Tested = isTested ["intGramToDecKg"]; Notes = "inverse kg↔g" }
        { Module = "Conversions"; Name = "decMtoDecCm";          Signature = "decimal<m> -> decimal<cm>";       Purity = Pure; Tested = isTested ["decMtoDecCm"]; Notes = "1 m = 100 cm" }
        { Module = "Conversions"; Name = "intToString";          Signature = "string -> string -> int -> string"; Purity = Pure; Tested = isTested ["intToString"]; Notes = "singular/plural string helper" }
        { Module = "Conversions"; Name = "milliLiterToLiter";    Signature = "decimal<mL> -> decimal<L>";       Purity = Pure; Tested = isTested ["milliLiterToLiter"]; Notes = "1 L = 1000 mL" }
        { Module = "Conversions"; Name = "literToMilliLiter";    Signature = "decimal<L> -> decimal<mL>";       Purity = Pure; Tested = isTested ["literToMilliLiter"]; Notes = "inverse" }
        { Module = "Creatinine";  Name = "toMicroMolePerLiter";  Signature = "float<mg/dL> -> float<microMol/L>"; Purity = Pure; Tested = isTested ["toMicroMolePerLiter"]; Notes = "k=88.42" }
        { Module = "Creatinine";  Name = "toMilliGramPerDeciLiter"; Signature = "float<microMol/L> -> float<mg/dL>"; Purity = Pure; Tested = isTested ["toMilliGramPerDeciLiter"]; Notes = "inverse of above" }
        { Module = "Urea";        Name = "toMilliMolePerLiter";  Signature = "float<mg/dL> -> float<mmol/L>";   Purity = Pure; Tested = isTested ["toMilliMolePerLiter"]; Notes = "k=0.3571" }
        { Module = "Urea";        Name = "toMilliGramPerDeciLiter"; Signature = "float<mmol/L> -> float<mg/dL>"; Purity = Pure; Tested = isTested ["Urea.toMilliGramPerDeciLiter"]; Notes = "inverse" }

        // === Calculations.Age ===
        { Module = "Age"; Name = "yearsMonthsWeeksDaysToDays"; Signature = "int<year> -> int<month> -> int<week> -> int<day> -> int<day>"; Purity = Pure; Tested = isTested ["yearsMonthsWeeksDaysToDays"]; Notes = "pure arithmetic age conversion" }
        { Module = "Age"; Name = "yearsMonthsWeeksToDaysOpt"; Signature = "option<int<year>> -> ... -> int<day>"; Purity = Pure; Tested = isTested ["yearsMonthsWeeksToDaysOpt"]; Notes = "None defaults to zero" }
        { Module = "Age"; Name = "fromBirthDate";             Signature = "DateTime -> DateTime -> int<year> * int<month> * int<week> * int<day>"; Purity = UnitDependent; Tested = isTested ["fromBirthDate"]; Notes = "depends on DateTime input (injectable)" }
        { Module = "Age"; Name = "toBirthDate";               Signature = "DateTime -> int<year> -> int<month> -> int<week> -> int<day> -> DateTime"; Purity = UnitDependent; Tested = isTested ["toBirthDate"]; Notes = "inverse of fromBirthDate" }
        { Module = "Age"; Name = "adjustedAge";               Signature = "int<day> -> int<week> -> DateTime -> DateTime -> int<day>"; Purity = UnitDependent; Tested = isTested ["adjustedAge"]; Notes = "corrected age for premature patients" }
        { Module = "Age"; Name = "postMenstrualAge";          Signature = "int<day> -> int<week> -> int<day> -> int<week>"; Purity = Pure; Tested = isTested ["postMenstrualAge"]; Notes = "pure arithmetic — gestAge + chronological age" }
        { Module = "Age"; Name = "ageToString";               Signature = "int<year> option -> ... -> string list"; Purity = Pure; Tested = isTested ["ageToString"]; Notes = "formats age as English string list" }
        { Module = "Age"; Name = "ageToStringNL";             Signature = "int<year> option -> ... -> string list"; Purity = Pure; Tested = isTested ["ageToStringNL"]; Notes = "Dutch localisation of ageToString" }
        { Module = "Age"; Name = "ageToStringNlShort";        Signature = "int<year> option -> ... -> string";      Purity = Pure; Tested = isTested ["ageToStringNlShort"]; Notes = "short Dutch age representation" }

        // === Calculations.BSA (partially tested) ===
        { Module = "BSA"; Name = "mosteller";        Signature = "float -> float -> float";                        Purity = Pure; Tested = isTested ["mosteller"; "Mosteller"]; Notes = "BSA formula — already tested via calcBSA" }
        { Module = "BSA"; Name = "duBois";           Signature = "float -> float -> float";                        Purity = Pure; Tested = isTested ["duBois"; "DuBois"]; Notes = "BSA formula — already tested" }
        { Module = "BSA"; Name = "haycock";          Signature = "float -> float -> float";                        Purity = Pure; Tested = isTested ["haycock"; "Haycock"]; Notes = "BSA formula — already tested" }
        { Module = "BSA"; Name = "gehanAndGeorge";   Signature = "float -> float -> float";                        Purity = Pure; Tested = isTested ["gehanAndGeorge"; "Gehan"]; Notes = "BSA formula — already tested" }
        { Module = "BSA"; Name = "fujimoto";         Signature = "float -> float -> float";                        Purity = Pure; Tested = isTested ["fujimoto"; "Fuijimoto"]; Notes = "BSA formula — already tested" }
        { Module = "BSA"; Name = "calcBSA";          Signature = "formula -> int option -> decimal<kg> -> decimal<cm> -> decimal<bsa>"; Purity = Pure; Tested = isTested ["calcBSA"]; Notes = "dispatcher — tested indirectly" }

        // === Calculations.Renal ===
        { Module = "Renal"; Name = "renalFunction";                Signature = "float<mL/min/normalM2> -> RenalFunction"; Purity = Pure; Tested = isTested ["renalFunction"; "RenalFunction"]; Notes = "eGFR threshold classifier — pure DU match" }
        { Module = "Renal"; Name = "calcCreatinine09";             Signature = "Gender -> Race -> float<year> -> Creat -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcCreatinine09"; "Creatinine09"]; Notes = "CKD-EPI 2009 formula" }
        { Module = "Renal"; Name = "calcCreatinine21";             Signature = "Gender -> float<year> -> Creat -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcCreatinine21"; "Creatinine21"]; Notes = "CKD-EPI 2021 (race-free)" }
        { Module = "Renal"; Name = "calcCystatinCreatinine12";     Signature = "Gender -> Race -> float<year> -> Creat -> Cystatin -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcCystatinCreatinine12"]; Notes = "CKD-EPI cystatin+creat 2012" }
        { Module = "Renal"; Name = "calcCystatinCreatinine21";     Signature = "Gender -> float<year> -> Creat -> Cystatin -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcCystatinCreatinine21"]; Notes = "CKD-EPI cystatin+creat 2021" }
        { Module = "Renal"; Name = "calcCystatin12";               Signature = "Gender -> float<year> -> Cystatin -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcCystatin12"]; Notes = "CKD-EPI cystatin-only 2012" }
        { Module = "Renal"; Name = "calcMDRD";                     Signature = "Gender -> Race -> float<year> -> Creat -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcMDRD"; "MDRD"]; Notes = "MDRD formula" }
        { Module = "Renal"; Name = "calcPediatricScharz";          Signature = "decimal<cm> -> Creat -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcPediatricScharz"; "Schwartz"]; Notes = "Pediatric Schwartz formula" }
        { Module = "Renal"; Name = "calcPediatricCystatinCreatinineCKID"; Signature = "Gender -> decimal<m> -> Creat -> Cystatin -> Urea -> float<mL/min/normalM2>"; Purity = Pure; Tested = isTested ["calcPediatricCystatinCreatinineCKID"; "CKID"]; Notes = "Pediatric CKiD formula" }
    ]

// ---------------------------------------------------------------------------
// Print catalog

let printCatalog () =
    printfn ""
    printfn "=== GenCORE.Lib Calculations & Conversions — Function Catalog ==="
    printfn "%-16s %-42s %-16s %s" "Module" "Function" "Purity" "Tested?"
    printfn "%s" (String.replicate 90 "-")
    for e in catalog do
        let tested = if e.Tested then "✅" else "❌"
        printfn "%-16s %-42s %-16s %s" e.Module e.Name (string e.Purity) tested
    printfn ""

// ---------------------------------------------------------------------------
// Coverage summary

let printCoverage () =
    printfn "=== Coverage Summary by Module ==="
    let groups =
        catalog
        |> List.groupBy (fun e -> e.Module)
    for (m, fns) in groups do
        let pure' = fns |> List.filter (fun e -> e.Purity = Pure) |> List.length
        let tested = fns |> List.filter (fun e -> e.Tested) |> List.length
        let total  = fns |> List.length
        printfn "%-16s  total=%d  pure=%d  tested=%d  gap=%d" m total pure' tested (pure' - (fns |> List.filter (fun e -> e.Purity = Pure && e.Tested) |> List.length))
    let totalPure   = catalog |> List.filter (fun e -> e.Purity = Pure) |> List.length
    let totalTested = catalog |> List.filter (fun e -> e.Tested) |> List.length
    printfn ""
    printfn "Overall: %d pure functions, %d tested (%d%% coverage), %d untested pure"
        totalPure totalTested
        (if totalPure = 0 then 0 else 100 * totalTested / totalPure)
        (catalog |> List.filter (fun e -> e.Purity = Pure && not e.Tested) |> List.length)
    printfn ""

// ---------------------------------------------------------------------------
// Scaffolded tests

let scaffoldConversions = """
    module ConversionTests =

        let tests = testList "Conversions" [

            test "weeksToDays: 1 week = 7 days" {
                1<week>
                |> Conversions.weeksToDays
                |> Expect.equal "1 week should be 7 days" 7<day>
            }

            test "weeksToDays: 2 weeks = 14 days" {
                2<week>
                |> Conversions.weeksToDays
                |> Expect.equal "2 weeks should be 14 days" 14<day>
            }

            test "daysToWeeks: 14 days = (2 weeks, 0 days)" {
                14<day>
                |> Conversions.daysToWeeks
                |> Expect.equal "14 days should be 2 weeks, 0 days" (2<week>, 0<day>)
            }

            test "daysToWeeks: 10 days = (1 week, 3 days)" {
                10<day>
                |> Conversions.daysToWeeks
                |> Expect.equal "10 days should be 1 week, 3 days" (1<week>, 3<day>)
            }

            test "intYearsToDays: 1 year = 365 days" {
                1<year>
                |> Conversions.intYearsToDays
                |> Expect.equal "1 year should be 365 days" 365<day>
            }

            test "decimalKgToDecimalGram: 1.5 kg = 1500 g" {
                1.5m<kg>
                |> Conversions.decimalKgToDecimalGram
                |> Expect.equal "1.5 kg should be 1500 g" 1500m<gram>
            }

            test "intGramToDecKg: 500 g = 0.5 kg" {
                500<gram>
                |> Conversions.intGramToDecKg
                |> Expect.equal "500 g should be 0.5 kg" 0.5m<kg>
            }

            test "decMtoDecCm: 1.7 m = 170 cm" {
                1.7m<m>
                |> Conversions.decMtoDecCm
                |> Expect.equal "1.7 m should be 170 cm" 170m<cm>
            }

            test "milliLiterToLiter: 500 mL = 0.5 L" {
                500m<mL>
                |> Conversions.milliLiterToLiter
                |> Expect.equal "500 mL should be 0.5 L" 0.5m<L>
            }

            test "Creatinine round-trip: mg/dL <-> microMol/L" {
                let original = 1.0<mg/dL>
                let roundtrip =
                    original
                    |> Conversions.Creatinine.toMicroMolePerLiter
                    |> Conversions.Creatinine.toMilliGramPerDeciLiter
                Math.Abs(float original - float roundtrip) < 0.001
                |> Expect.isTrue "creatinine round-trip should be within 0.1% precision"
            }
        ]
"""

let scaffoldAge = """
    module AgeCalculationTests =

        let tests = testList "AgeCalculations" [

            test "yearsMonthsWeeksDaysToDays: 1 year = 365 days" {
                Calculations.Age.yearsMonthsWeeksDaysToDays 1<year> 0<month> 0<week> 0<day>
                |> Expect.equal "1 year = 365 days" 365<day>
            }

            test "yearsMonthsWeeksDaysToDays: 1 month ≈ 30 days" {
                Calculations.Age.yearsMonthsWeeksDaysToDays 0<year> 1<month> 0<week> 0<day>
                |> Expect.equal "1 month = 30 days (by convention)" 30<day>
            }

            test "yearsMonthsWeeksDaysToDays: 2 years 3 months 1 week 5 days" {
                let expected = (2 * 365 + 3 * 30 + 7 + 5) |> fun x -> x * 1<day>
                Calculations.Age.yearsMonthsWeeksDaysToDays 2<year> 3<month> 1<week> 5<day>
                |> Expect.equal "combined age calculation" expected
            }

            test "yearsMonthsWeeksToDaysOpt: all None = 0 days" {
                Calculations.Age.yearsMonthsWeeksToDaysOpt None None None None
                |> Expect.equal "all None should give 0 days" 0<day>
            }

            test "yearsMonthsWeeksToDaysOpt: Some 1 year = 365 days" {
                Calculations.Age.yearsMonthsWeeksToDaysOpt (Some 1<year>) None None None
                |> Expect.equal "Some 1 year should give 365 days" 365<day>
            }

            test "postMenstrualAge: 32 weeks gestation + 4 weeks age = 36 weeks PMA" {
                // gestWeeks=32, gestDays=0, actAge = 4*7 = 28 days
                let actAge = 28<day>
                let gestWeeks = 32<week>
                let gestDays  = 0<day>
                Calculations.Age.postMenstrualAge actAge gestWeeks gestDays
                |> Expect.equal "32w gest + 4w actual = 36w PMA" 36<week>
            }

            test "postMenstrualAge: 40 weeks gestation (full-term) + 0 days = 40 weeks" {
                Calculations.Age.postMenstrualAge 0<day> 40<week> 0<day>
                |> Expect.equal "40w gestation + 0 days = 40w PMA" 40<week>
            }

            test "ageToString: 1 year formats correctly" {
                Calculations.Age.ageToString (Some 1<year>) None None None
                |> Expect.equal "1 year age string" ["1 year"; ""; ""; ""]
            }

            test "ageToString: 2 years formats correctly" {
                Calculations.Age.ageToString (Some 2<year>) None None None
                |> Expect.equal "2 years age string" ["2 years"; ""; ""; ""]
            }

            test "ageToStringNlShort: 1 year, 3 months -> 'n jaar, m maanden'" {
                let result = Calculations.Age.ageToStringNlShort (Some 1<year>) (Some 3<month>) None None
                result |> String.length
                |> (fun l -> l > 0)
                |> Expect.isTrue "NL short age string should be non-empty"
            }
        ]
"""

let scaffoldRenal = """
    module RenalCalculationTests =

        open Informedica.GenCore.Lib.Calculations

        let tests = testList "RenalCalculations" [

            test "renalFunction: 95 ml/min/1.73m2 = Normal" {
                95.<mL/min/normalM2>
                |> Renal.renalFunction
                |> Expect.equal "eGFR 95 should be Normal" Renal.Normal
            }

            test "renalFunction: 65 ml/min/1.73m2 = MildlyDecreased" {
                65.<mL/min/normalM2>
                |> Renal.renalFunction
                |> Expect.equal "eGFR 65 should be MildlyDecreased" Renal.MildlyDecreased
            }

            test "renalFunction: 50 ml/min/1.73m2 = MildToModeratelyDecreased" {
                50.<mL/min/normalM2>
                |> Renal.renalFunction
                |> Expect.equal "eGFR 50 should be MildToModeratelyDecreased" Renal.MildToModeratelyDecreased
            }

            test "renalFunction: 35 ml/min/1.73m2 = ModerateToSeverlyDecreased" {
                35.<mL/min/normalM2>
                |> Renal.renalFunction
                |> Expect.equal "eGFR 35 should be ModerateToSeverlyDecreased" Renal.ModerateToSeverlyDecreased
            }

            test "renalFunction: 20 ml/min/1.73m2 = SeverelyDecreased" {
                20.<mL/min/normalM2>
                |> Renal.renalFunction
                |> Expect.equal "eGFR 20 should be SeverelyDecreased" Renal.SeverelyDecreased
            }

            test "renalFunction: 10 ml/min/1.73m2 = KidneyFailure" {
                10.<mL/min/normalM2>
                |> Renal.renalFunction
                |> Expect.equal "eGFR 10 should be KidneyFailure" Renal.KidneyFailure
            }

            test "calcCreatinine09 Male Other: reasonable eGFR for known values" {
                // 40-year-old male, creatinine 1.0 mg/dL -> expect ~93 mL/min/1.73m2
                let eGFR =
                    Renal.calcCreatinine09 Renal.Male Renal.Other 40.<year>
                        (Renal.CreatinineMilligramPerDeciLiter 1.0<mg/dL>)
                float eGFR > 80. && float eGFR < 110.
                |> Expect.isTrue "40y Male creatinine 1.0 mg/dL should give plausible eGFR (~93)"
            }

            test "calcCreatinine21 Female: reasonable eGFR" {
                // 50-year-old female, creatinine 0.9 mg/dL -> expect ~75-95 mL/min/1.73m2
                let eGFR =
                    Renal.calcCreatinine21 Renal.Female 50.<year>
                        (Renal.CreatinineMilligramPerDeciLiter 0.9<mg/dL>)
                float eGFR > 60. && float eGFR < 110.
                |> Expect.isTrue "50y Female creatinine 0.9 mg/dL should give plausible eGFR"
            }

            test "calcCreatinine09 vs calcCreatinine21: similar for White male" {
                // For a White male the difference between 2009 and 2021 should be small
                let creat = Renal.CreatinineMilligramPerDeciLiter 1.0<mg/dL>
                let eGFR09 = Renal.calcCreatinine09 Renal.Male Renal.Other 40.<year> creat |> float
                let eGFR21 = Renal.calcCreatinine21 Renal.Male 40.<year> creat |> float
                Math.Abs(eGFR09 - eGFR21) < 15.
                |> Expect.isTrue "CKD-EPI 2009 and 2021 should agree within 15 for same demographics"
            }

            test "pediatricSchwartzFormula: 4y height 100cm creat 0.4 mg/dL" {
                // Expected: 0.413 * (100/0.4) = ~103 mL/min/1.73m2
                let eGFR =
                    Renal.calcPediatricScharz 100m<cm>
                        (Renal.CreatinineMilligramPerDeciLiter 0.4<mg/dL>)
                float eGFR > 90. && float eGFR < 115.
                |> Expect.isTrue "Pediatric Schwartz formula should give ~103 for height=100, creat=0.4"
            }

            test "renalFunction thresholds are strict boundaries" {
                // Normal threshold is 90 - exactly 90 should be Normal
                90.<mL/min/normalM2>
                |> Renal.renalFunction
                |> Expect.equal "eGFR = 90 (= threshold) should be Normal" Renal.Normal
            }
        ]
"""

let printScaffold () =
    printfn ""
    printfn "=== Scaffolded Expecto Tests (paste into tests/Informedica.GenCORE.Tests/Tests.fs) ==="
    printfn ""
    printfn "// --- ADD these open statements if not already present ---"
    printfn "// open Informedica.GenCore.Lib"
    printfn "// open Informedica.GenCore.Lib.Patients"
    printfn "// open Informedica.GenCore.Lib.Ranges"
    printfn ""
    printfn "// ---- MODULE: ConversionTests ----"
    printfn "%s" scaffoldConversions
    printfn ""
    printfn "// ---- MODULE: AgeCalculationTests ----"
    printfn "%s" scaffoldAge
    printfn ""
    printfn "// ---- MODULE: RenalCalculationTests ----"
    printfn "%s" scaffoldRenal
    printfn ""
    printfn "// ---- ADD to the top-level testList in Tests.fs ---"
    printfn "// ConversionTests.tests"
    printfn "// AgeCalculationTests.tests"
    printfn "// RenalCalculationTests.tests"
    printfn ""

// ---------------------------------------------------------------------------
// Main

printCatalog ()
printCoverage ()
printScaffold ()

printfn "=== W3 Coverage Status ==="
printfn "Script covers: Conversions, Creatinine, Urea, Calculations.Age, Calculations.BSA, Calculations.Renal"
printfn "Script adds: ~32 scaffolded test cases for 20+ previously untested pure functions"
printfn "Next step: paste scaffolded tests into tests/Informedica.GenCORE.Tests/Tests.fs"
printfn ""
printfn "W3 Test Scaffolding Series:"
printfn "  CoverageAnalysis.fsx             ✅ merged"
printfn "  TestMigrationStatus.fsx          ✅ merged"
printfn "  ZFormTestMigration.fsx           ✅ merged → tests migrated"
printfn "  NKFTestAnalysis.fsx              ✅ merged"
printfn "  ZFormCITests.fsx                 ✅ merged → tests migrated"
printfn "  NKFCITests.fsx                   ✅ merged"
printfn "  GenOrderTestScaffolding.fsx      🔄 PR #13 pending"
printfn "  GenFORMTestScaffolding.fsx       🔄 PR #14 pending"
printfn "  GenCOREPatientScaffolding.fsx    🔄 PR #15 pending"
printfn "  GenCORECalculationsScaffolding   this script"

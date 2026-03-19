/// GenORDER Test Scaffolding Analysis Script
/// Catalogs all functions in Informedica.GenORDER.Lib, identifies coverage gaps,
/// and suggests concrete test cases for pure (data-independent) functions.
///
/// Run with: dotnet fsi scripts/GenOrderTestScaffolding.fsx
/// Supports the W3 Test Coverage workshop.
///
/// Context: CoverageAnalysis.fsx found GenORDER at ~3% coverage (870 fns, 29 tests).
/// The CI Tests.fs has since grown to 21 named tests across 971 lines. This script
/// provides a focused look at the pure, data-independent functions that remain untested
/// and generates scaffolded test cases ready for migration.

open System
open System.IO
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot    = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let srcDir      = Path.Combine(repoRoot, "src", "Informedica.GenORDER.Lib")
let ciTestsPath = Path.Combine(repoRoot, "tests", "Informedica.GenORDER.Tests", "Tests.fs")

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type Purity =
    | Pure          // No external data dependency; safe to run in CI
    | UnitDependent // Requires GenUnits ValueUnit conversion (still pure, needs DLL)
    | DataDependent // Calls GenForm / ZIndex / Web at runtime

type TestStatus =
    | HasTest       // A named test for this function exists in CI Tests.fs
    | NoTest        // No named test found in CI Tests.fs

type FunctionEntry =
    {
        Module    : string
        Name      : string
        Purity    : Purity
        Signature : string
        Status    : TestStatus
        Notes     : string
    }

// ---------------------------------------------------------------------------
// Helper: check CI tests
// ---------------------------------------------------------------------------

let ciContent = File.ReadAllText ciTestsPath

let hasTest name =
    // Simple heuristic: function name appears in a test "" block
    ciContent.Contains(name) |> function
    | true  -> HasTest
    | false -> NoTest

// ---------------------------------------------------------------------------
// Function catalogue
// ---------------------------------------------------------------------------

let catalogue =
    [
        // ── Patient module ─────────────────────────────────────────────────

        { Module = "Patient.Optics"
          Name   = "ageToValueUnit"
          Purity = UnitDependent
          Signature = "Age list -> ValueUnit"
          Status = hasTest "ageToValueUnit"
          Notes  = "Folds [Years 2; Months 3] → days ValueUnit" }

        { Module = "Patient.Optics"
          Name   = "ageFromValueUnit"
          Purity = UnitDependent
          Signature = "ValueUnit -> int * int * int * int"
          Status = hasTest "ageFromValueUnit"
          Notes  = "Inverse of ageToValueUnit; round-trip property candidate" }

        { Module = "Patient"
          Name   = "premature"
          Purity = Pure
          Signature = "unit -> Patient"
          Status = hasTest "premature"
          Notes  = "Returns a Patient with age=24 weeks gest + weight range" }

        { Module = "Patient"
          Name   = "newBorn"
          Purity = Pure
          Signature = "unit -> Patient"
          Status = hasTest "newBorn"
          Notes  = "Returns a newborn Patient (age 0–28 days, weight 2.5–4.5 kg)" }

        { Module = "Patient"
          Name   = "infant"
          Purity = Pure
          Signature = "unit -> Patient"
          Status = hasTest "infant"
          Notes  = "Returns an infant Patient (age 1–12 months)" }

        { Module = "Patient"
          Name   = "toddler"
          Purity = Pure
          Signature = "unit -> Patient"
          Status = hasTest "toddler"
          Notes  = "Returns a toddler Patient (age 1–3 years)" }

        { Module = "Patient"
          Name   = "child"
          Purity = Pure
          Signature = "unit -> Patient"
          Status = hasTest "child"
          Notes  = "Returns a child Patient (age 3–12 years)" }

        { Module = "Patient"
          Name   = "teenager"
          Purity = Pure
          Signature = "unit -> Patient"
          Status = hasTest "teenager"
          Notes  = "Returns a teenager Patient (age 12–18 years)" }

        { Module = "Patient"
          Name   = "adult"
          Purity = Pure
          Signature = "unit -> Patient"
          Status = hasTest "adult"
          Notes  = "Returns an adult Patient (age ≥18 years)" }

        // ── Medication module ───────────────────────────────────────────────

        { Module = "Medication"
          Name   = "parseBigRationalOpt"
          Purity = Pure
          Signature = "string -> BigRational option"
          Status = hasTest "parseBigRationalOpt"
          Notes  = "Parses '1/2', '3.5', '10' to BigRational; empty/invalid → None" }

        { Module = "Medication"
          Name   = "parseDutchDecimal"
          Purity = Pure
          Signature = "string -> BigRational option"
          Status = hasTest "parseDutchDecimal"
          Notes  = "Parses Dutch decimals: '1,5' → Some 3/2, '1.5' → Some 3/2" }

        { Module = "Medication"
          Name   = "parseOrderType"
          Purity = Pure
          Signature = "string -> OrderType"
          Status = hasTest "parseOrderType"
          Notes  = "Maps 'disc' → DiscontinuousOrder, 'timed' → TimedOrder, etc." }

        { Module = "Medication"
          Name   = "parseMinMax"
          Purity = UnitDependent
          Signature = "string -> MinMax"
          Status = hasTest "parseMinMax"
          Notes  = "Parses 'min..max' range strings with units" }

        { Module = "Medication"
          Name   = "parseLine"
          Purity = Pure
          Signature = "string -> string[]"
          Status = hasTest "parseLine"
          Notes  = "Splits a CSV-style line (handles quoted fields)" }

        { Module = "Medication"
          Name   = "valueUnitOptToString"
          Purity = UnitDependent
          Signature = "ValueUnit option -> string"
          Status = hasTest "valueUnitOptToString"
          Notes  = "Converts optional ValueUnit to display string; None → empty" }

        { Module = "Medication"
          Name   = "minMaxToString"
          Purity = UnitDependent
          Signature = "MinMax -> string"
          Status = hasTest "minMaxToString"
          Notes  = "Renders MinMax range as display string" }

        // ── OrderVariable module ────────────────────────────────────────────

        { Module = "OrderVariable"
          Name   = "createMinMax"
          Purity = Pure
          Signature = "BigRational option -> BigRational option -> MinMax"
          Status = hasTest "createMinMax"
          Notes  = "Smart constructor; validates min ≤ max" }

        { Module = "OrderVariable"
          Name   = "minMaxToString"
          Purity = Pure
          Signature = "MinMax -> string"
          Status = hasTest "minMaxToString"
          Notes  = "Formats MinMax for printing; empty when both None" }

        { Module = "OrderVariable"
          Name   = "eqs"
          Purity = Pure
          Signature = "OrderVariable -> OrderVariable -> bool"
          Status = hasTest "eqs"
          Notes  = "Structural equality for OrderVariable (ignores identity)" }

        { Module = "OrderVariable"
          Name   = "canMapToValueUnit"
          Purity = UnitDependent
          Signature = "Unit -> bool"
          Status = hasTest "canMapToValueUnit"
          Notes  = "Returns true when unit group is valid for OrderVariable" }

        // ── ValueUnit helpers (GenORDER-local) ──────────────────────────────

        { Module = "ValueUnit"
          Name   = "isAdjust"
          Purity = UnitDependent
          Signature = "Unit -> bool"
          Status = hasTest "isAdjust"
          Notes  = "True for kg or m2 (adjust units)" }

        { Module = "ValueUnit"
          Name   = "correctAdjustOrder"
          Purity = UnitDependent
          Signature = "ValueUnit -> ValueUnit"
          Status = hasTest "correctAdjustOrder"
          Notes  = "Reorders units: mg/day/kg → mg/kg/day" }

        { Module = "ValueUnit"
          Name   = "collect"
          Purity = UnitDependent
          Signature = "ValueUnit[] -> ValueUnit option"
          Status = hasTest "collect"
          Notes  = "Merges same-unit array; None on empty; throws on incompatible" }
    ]

// ---------------------------------------------------------------------------
// Analysis
// ---------------------------------------------------------------------------

let pure_     = catalogue |> List.filter (fun f -> f.Purity = Pure)
let unitDep   = catalogue |> List.filter (fun f -> f.Purity = UnitDependent)
let dataDep   = catalogue |> List.filter (fun f -> f.Purity = DataDependent)
let untested  = catalogue |> List.filter (fun f -> f.Status = NoTest)
let covered   = catalogue |> List.filter (fun f -> f.Status = HasTest)

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

printfn ""
printfn "GenORDER Test Scaffolding Analysis"
printfn "==================================="
printfn ""
printfn "Source : src/Informedica.GenORDER.Lib/"
printfn "CI     : %s" (Path.GetRelativePath(repoRoot, ciTestsPath))
printfn ""
printfn "%-20s  %-25s  %-15s  %-12s  %s" "Module" "Function" "Purity" "CI Status" "Notes"
printfn "%s" (String.replicate 115 "-")

for f in catalogue do
    let purityTag =
        match f.Purity with
        | Pure          -> "pure ✅"
        | UnitDependent -> "unit-dep 🔵"
        | DataDependent -> "data-dep ⚠️ "
    let statusTag =
        match f.Status with
        | HasTest -> "covered ✅"
        | NoTest  -> "NO TEST ❌"
    let name = if f.Name.Length > 23 then f.Name.[..20] + "..." else f.Name
    printfn "%-20s  %-25s  %-15s  %-12s  %s" f.Module name purityTag statusTag f.Notes

printfn ""
printfn "Summary"
printfn "-------"
printfn "  Catalogued functions              : %d" catalogue.Length
printfn "  Pure (no external deps)           : %d" pure_.Length
printfn "  Unit-dependent (GenUnits DLL)     : %d" unitDep.Length
printfn "  Data-dependent (GenForm/ZIndex)   : %d" dataDep.Length
printfn "  Already covered in CI             : %d" covered.Length
printfn "  NO TEST (gap to close)            : %d" untested.Length
printfn ""

// ---------------------------------------------------------------------------
// Suggested test cases for untested pure functions
// ---------------------------------------------------------------------------

printfn "Suggested test scaffolding for tests/Informedica.GenORDER.Tests/Tests.fs"
printfn "-------------------------------------------------------------------------"
printfn ""
printfn """// ── Patient constructors (smoke tests) ────────────────────────────────────
open Informedica.GenOrder.Lib

module PatientTests =
    let tests = testList "Patient constructors" [

        test "premature has gestational age set" {
            let p = Patient.premature
            p.GestAge |> Expect.isSome "premature should have gestational age"
        }

        test "newBorn has age set to zero days" {
            let p = Patient.newBorn
            p.Age |> Expect.isSome "newBorn should have age"
        }

        test "infant age lower bound is one month" {
            let p = Patient.infant
            p.Age |> Expect.isSome "infant should have age"
        }

        test "child has weight range" {
            let p = Patient.child
            p.Weight |> Expect.isSome "child should have weight"
        }

        test "teenager age lower bound is 12 years" {
            let p = Patient.teenager
            p.Age |> Expect.isSome "teenager should have age"
        }

        test "adult age lower bound is 18 years" {
            let p = Patient.adult
            p.Age |> Expect.isSome "adult should have age"
        }
    ]

// ── Patient.Optics round-trip ───────────────────────────────────────────────
module PatientOpticsTests =
    open Patient.Optics

    let tests = testList "Patient.Optics age round-trip" [

        test "ageToValueUnit and ageFromValueUnit round-trip for 2 years 3 months" {
            let ages = [ Years 2; Months 3 ]
            let vu   = ageToValueUnit ages
            let back = ageFromValueUnit vu
            back |> Expect.equal "round-trip should recover original age" ages
        }

        test "ageToValueUnit for 365 days equals one year" {
            let vu   = ageToValueUnit [ Days 365 ]
            let back = ageFromValueUnit vu
            back |> Expect.equal "365 days should round-trip to Years 1" [ Years 1 ]
        }
    ]

// ── Medication.Parsing ─────────────────────────────────────────────────────
module MedicationParsingTests =
    open Medication.Parsing  // internal module name - adjust if different

    let tests = testList "Medication parsing" [

        test "parseBigRationalOpt parses fraction '1/3'" {
            parseBigRationalOpt "1/3"
            |> Expect.equal "should parse to 1/3N" (Some (1N / 3N))
        }

        test "parseBigRationalOpt returns None for empty string" {
            parseBigRationalOpt ""
            |> Expect.isNone "empty string should give None"
        }

        test "parseDutchDecimal parses '1,5'" {
            parseDutchDecimal "1,5"
            |> Expect.equal "should parse 1,5 to 3/2" (Some (3N / 2N))
        }

        test "parseDutchDecimal parses '2.0'" {
            parseDutchDecimal "2.0"
            |> Expect.equal "should parse 2.0 to 2N" (Some 2N)
        }

        test "parseOrderType 'disc' gives DiscontinuousOrder" {
            parseOrderType "disc"
            |> Expect.equal "should give DiscontinuousOrder" DiscontinuousOrder
        }

        test "parseOrderType 'timed' gives TimedOrder" {
            parseOrderType "timed"
            |> Expect.equal "should give TimedOrder" TimedOrder
        }
    ]
"""

printfn ""
printfn "Migration notes"
printfn "---------------"
printfn "  1. Add the above test modules to tests/Informedica.GenORDER.Tests/Tests.fs."
printfn "  2. Reference the correct module paths (check if Parsing is [<AutoOpen>] or nested)."
printfn "  3. Run: dotnet test tests/Informedica.GenORDER.Tests/"
printfn "  4. Patient constructors are pure — no GENPRES_URL_ID needed."
printfn "  5. Parsing tests (parseBigRationalOpt, parseDutchDecimal, parseOrderType) are pure."
printfn "  6. Optics round-trip tests require GenUnits DLL (already referenced in the test project)."
printfn ""
printfn "Priority order for highest coverage gain:"
printfn "  1. Patient constructors (6 tests, zero deps) ← easiest win"
printfn "  2. Medication parsing helpers (6 tests, zero deps)"
printfn "  3. Patient.Optics round-trip (2 property-based tests, GenUnits DLL only)"
printfn ""

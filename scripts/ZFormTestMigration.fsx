/// ZForm Test Migration Analysis Script
/// Analyses the 15 test cases in ZForm.Lib/Scripts/Tests.fsx and categorises
/// them as pure (safe to add to CI) or data-dependent (require ZIndex cache).
///
/// Run with: dotnet fsi scripts/ZFormTestMigration.fsx
/// Supports the W3 Test Migration workshop.

open System
open System.IO
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let zformTestsPath = Path.Combine(repoRoot, "src", "Informedica.ZForm.Lib", "Scripts", "Tests.fsx")
let ciTestsPath    = Path.Combine(repoRoot, "tests", "Informedica.ZForm.Tests", "Tests.fs")

// ---------------------------------------------------------------------------
// Test catalogue (derived from manual analysis of Scripts/Tests.fsx)
// ---------------------------------------------------------------------------

type TestCategory =
    | Pure          // No external data dependency; safe to run in CI
    | DataDependent // Calls ZIndex cache data; requires GENPRES_URL_ID

type TestEntry =
    {
        Suite    : string
        Name     : string
        Category : TestCategory
        Notes    : string
    }

let testCatalogue =
    [
        // MinMax suite - 1 test
        { Suite = "MinMax"; Name = "ageToString"; Category = Pure; Notes = "Uses only BigRational + Range types" }

        // Mapping suite - 3 tests (all data-dependent: call ZIndex.Lib APIs)
        { Suite = "Mapping"; Name = "all units that can be mapped have a mapping";  Category = DataDependent; Notes = "Calls ZIndex.Lib.Names.getFormUnits / getGenericUnits" }
        { Suite = "Mapping"; Name = "all routes can be mapped";                      Category = DataDependent; Notes = "Calls ZIndex.Lib.GenPresProduct.get and DoseRule.get" }
        { Suite = "Mapping"; Name = "all frequencies can be mapped";                 Category = DataDependent; Notes = "Calls ZIndex.Lib.DoseRule.get" }

        // Patient suite - 4 tests (pure DTO tests)
        { Suite = "Patient"; Name = "an 'empty patient'";               Category = Pure; Notes = "Creates Dto.dto() in memory" }
        { Suite = "Patient"; Name = "a patient with a min age";         Category = Pure; Notes = "Sets DTO fields, calls Dto.fromDto" }
        { Suite = "Patient"; Name = "a patient with a min age wrong unit"; Category = Pure; Notes = "Uses ignore — TODO not implemented yet" }
        { Suite = "Patient"; Name = "a patient with a min age wrong group"; Category = Pure; Notes = "Uses ignore — TODO not implemented yet" }

        // DoseRange suite - 6 tests (pure DTO + optics tests)
        { Suite = "DoseRange"; Name = "there and back again empty doserange dto";    Category = Pure; Notes = "Round-trip serialization test" }
        { Suite = "DoseRange"; Name = "there and back again with filled doserange dto"; Category = Pure; Notes = "Round-trip serialization test" }
        { Suite = "DoseRange"; Name = "can create a dose range";                     Category = Pure; Notes = "Optics-based construction" }
        { Suite = "DoseRange"; Name = "can create a dose range with a rate";         Category = Pure; Notes = "Optics-based construction" }
        { Suite = "DoseRange"; Name = "can create a dose range with a rate per kg";  Category = Pure; Notes = "Optics with unit conversion" }
        { Suite = "DoseRange"; Name = "can covert a unit";                           Category = Pure; Notes = "Optics with convertTo" }

        // DoseRule suite - 1 test (pure DTO round-trip)
        { Suite = "DoseRule"; Name = "there and back again with an empty doserule"; Category = Pure; Notes = "Round-trip serialization test" }
    ]

// ---------------------------------------------------------------------------
// Analysis
// ---------------------------------------------------------------------------

let total          = testCatalogue.Length
let pure_          = testCatalogue |> List.filter (fun t -> t.Category = Pure)
let dataDependent  = testCatalogue |> List.filter (fun t -> t.Category = DataDependent)
let pureCount      = pure_.Length
let dataCount      = dataDependent.Length

// Count existing CI tests (excluding hello-world stub)
let ciContent = File.ReadAllText ciTestsPath
let ciStubOnly = ciContent.Contains("Hello World") && not (ciContent.Contains("ageToString"))

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

printfn ""
printfn "ZForm Test Migration Analysis"
printfn "============================="
printfn ""
printfn "Source : %s" (Path.GetRelativePath(repoRoot, zformTestsPath))
printfn "CI     : %s" (Path.GetRelativePath(repoRoot, ciTestsPath))
printfn ""
printfn "%-10s  %-45s  %-15s  %s" "Suite" "Test name" "Category" "Notes"
printfn "%s" (String.replicate 110 "-")
for t in testCatalogue do
    let cat = match t.Category with Pure -> "pure ✅" | DataDependent -> "data-dep ⚠️ "
    let name = if t.Name.Length > 43 then t.Name.[..40] + "..." else t.Name
    printfn "%-10s  %-45s  %-15s  %s" t.Suite name cat t.Notes

printfn ""
printfn "Summary"
printfn "-------"
printfn "  Total tests in Scripts/Tests.fsx : %d" total
printfn "  Pure (safe for CI)               : %d" pureCount
printfn "  Data-dependent (need ZIndex cache): %d" dataCount
printfn "  Current CI test count            : %s" (if ciStubOnly then "1 (hello-world stub only)" else ">1")
printfn ""

if ciStubOnly then
    printfn "⚠️  CI currently has only a hello-world stub."
    printfn "   Migrating the %d pure tests would increase meaningful CI coverage." pureCount
    printfn ""
    printfn "Migration steps:"
    printfn "  1. Copy the MinMax, Patient, DoseRange and DoseRule test suites"
    printfn "     from src/Informedica.ZForm.Lib/Scripts/Tests.fsx"
    printfn "     into tests/Informedica.ZForm.Tests/Tests.fs"
    printfn "  2. Remove the hello-world stub."
    printfn "  3. Ensure the test project references Aether (for Optics) and"
    printfn "     Informedica.GenCore.Lib (for Ranges / MinMax)."
    printfn "  4. Run: dotnet test tests/Informedica.ZForm.Tests/"
    printfn ""
    printfn "Data-dependent tests (Mapping suite) should NOT be migrated"
    printfn "to CI unless the ZIndex cache data is guaranteed to be present"
    printfn "in the CI environment (GENPRES_URL_ID set and data downloaded)."
else
    printfn "✅  CI already has non-stub tests. Review the catalogue above for any gaps."

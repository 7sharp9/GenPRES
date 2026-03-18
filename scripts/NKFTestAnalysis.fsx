/// NKF (Nederlands Kinderformularium) Test Analysis Script
/// Identifies the pure, data-independent functions in NKF.Lib and documents
/// concrete test cases that can be migrated to tests/Informedica.NKF.Tests/Tests.fs.
///
/// Run with: dotnet fsi scripts/NKFTestAnalysis.fsx
/// Supports the W3 Test Coverage workshop.

open System
open System.IO
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot    = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let nkfSrcDir   = Path.Combine(repoRoot, "src", "Informedica.NKF.Lib")
let ciTestsPath = Path.Combine(repoRoot, "tests", "Informedica.NKF.Tests", "Tests.fs")

// ---------------------------------------------------------------------------
// Catalogue of pure functions in NKF.Lib
// ---------------------------------------------------------------------------

type Purity =
    | Pure          // No external data dependency; safe to run in CI
    | DataDependent // Uses Web / ZIndex / GENPRES_URL_ID at init time
    | UnitDependent // Uses GenUnits ValueUnit conversion (pure, but requires DLL)

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
        // Utils.Regex (pure string utilities)
        { Module = "Utils.Regex";    Name = "matchFloat";      Purity = Pure;          Signature = "string -> string";              Notes = "Extracts floating-point substring" }
        { Module = "Utils.Regex";    Name = "matchAlpha";      Purity = Pure;          Signature = "string -> string";              Notes = "Extracts alpha substring" }
        { Module = "Utils.Regex";    Name = "matchFloatAlpha"; Purity = Pure;          Signature = "string -> string * string";     Notes = "Extracts (float, unit) pair" }

        // Drug.Frequency (pure discriminated-union logic)
        { Module = "Drug.Frequency"; Name = "isValid";         Purity = Pure;          Signature = "Frequency -> bool";             Notes = "Validates Max>0 && Time>0 && Unit non-empty for Frequency/PRN" }
        { Module = "Drug.Frequency"; Name = "toDoseType";      Purity = Pure;          Signature = "Frequency -> string";           Notes = "Maps Frequency→'onderhoud', PRN→'prn', etc." }
        { Module = "Drug.Frequency"; Name = "getFrequency";    Purity = Pure;          Signature = "Frequency -> string";           Notes = "Returns ';'-delimited min..max range or empty" }

        // Drug.Target (pure pattern-match logic)
        { Module = "Drug.Target";    Name = "getTarget";       Purity = Pure;          Signature = "Target -> (TargetType * TargetAge * TargetWeight) option"; Notes = "Unwraps Target DU; Unknown → None" }
        { Module = "Drug.Target";    Name = "getTargetType";   Purity = Pure;          Signature = "Target -> TargetType option";   Notes = "Extracts TargetType from Target" }
        { Module = "Drug.Target";    Name = "genderToString";  Purity = Pure;          Signature = "Target -> string";              Notes = "Boy→'man', Girl→'vrouw', else ''" }
        { Module = "Drug.Target";    Name = "getAge";          Purity = Pure;          Signature = "Target -> QuantityUnit option * QuantityUnit option"; Notes = "Extracts Age range; non-Age variants return defaults" }
        { Module = "Drug.Target";    Name = "getWeight";       Purity = Pure;          Signature = "Target -> QuantityUnit option * QuantityUnit option"; Notes = "Extracts Weight range" }
        { Module = "Drug.Target";    Name = "getQuantityUnit"; Purity = Pure;          Signature = "QuantityUnit -> float * string"; Notes = "Destructs QuantityUnit record" }

        // Drug (top-level)
        { Module = "Drug";           Name = "createDrug";      Purity = Pure;          Signature = "string -> string -> string -> string -> Drug"; Notes = "Smart constructor; Doses=[], Shape=''" }

        // Export (pure string transformations)
        { Module = "Export";         Name = "cleanGenericName";Purity = Pure;          Signature = "Drug -> Drug";                  Notes = "Normalises generic name (lower-case, ë→e, ï→i, strip combinatie)" }

        // Drug.Target (unit-dependent conversions)
        { Module = "Drug.Target";    Name = "getAgeInDays";    Purity = UnitDependent; Signature = "Target -> float option * float option"; Notes = "Converts age to days via GenUnits; Neonate→(None,Some 30.0)" }
        { Module = "Drug.Target";    Name = "getGestAgeInDays";Purity = UnitDependent; Signature = "Target -> float option * float option"; Notes = "Gestational-age version of getAgeInDays" }
        { Module = "Drug.Target";    Name = "getWeightInGram"; Purity = UnitDependent; Signature = "Target -> float option * float option"; Notes = "Converts weight to grams via GenUnits" }

        // Mapping (data-dependent — reads Google Sheets at init)
        { Module = "Mapping";        Name = "routeMapping";    Purity = DataDependent; Signature = "unit -> {| long; short; kinderFormularium |}[]"; Notes = "Calls Web.getDataFromSheet 'Routes'" }
        { Module = "Mapping";        Name = "productMapping";  Purity = DataDependent; Signature = "unit -> {| medication; route; generic; ... |}[]"; Notes = "Calls Web.getDataFromSheet 'Kinderformularium'" }
        { Module = "Mapping";        Name = "mapRoute";        Purity = DataDependent; Signature = "string -> string option";        Notes = "Depends on routeMapping (data-dep at init)" }
        { Module = "Mapping";        Name = "mapUnit";         Purity = DataDependent; Signature = "string -> Unit option";          Notes = "Depends on unitMapping (data-dep at init)" }
    ]

// ---------------------------------------------------------------------------
// Analysis
// ---------------------------------------------------------------------------

let pure_       = catalogue |> List.filter (fun f -> f.Purity = Pure)
let unitDep     = catalogue |> List.filter (fun f -> f.Purity = UnitDependent)
let dataDep     = catalogue |> List.filter (fun f -> f.Purity = DataDependent)
let ciContent   = File.ReadAllText ciTestsPath
let ciStubOnly  = ciContent.Contains("Hello World") && not (ciContent.Contains("matchFloat"))

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

printfn ""
printfn "NKF Library Test Analysis"
printfn "========================="
printfn ""
printfn "Source : src/Informedica.NKF.Lib/"
printfn "CI     : %s" (Path.GetRelativePath(repoRoot, ciTestsPath))
printfn ""
printfn "%-20s  %-25s  %-15s  %s" "Module" "Function" "Purity" "Notes"
printfn "%s" (String.replicate 100 "-")
for f in catalogue do
    let purityTag =
        match f.Purity with
        | Pure          -> "pure ✅"
        | UnitDependent -> "unit-dep 🔵"
        | DataDependent -> "data-dep ⚠️ "
    let fn = if f.Name.Length > 23 then f.Name.[..20] + "..." else f.Name
    printfn "%-20s  %-25s  %-15s  %s" f.Module fn purityTag f.Notes

printfn ""
printfn "Summary"
printfn "-------"
printfn "  Pure (no external deps)           : %d" pure_.Length
printfn "  Unit-dependent (GenUnits DLL)     : %d" unitDep.Length
printfn "  Data-dependent (Web/GoogleSheets) : %d" dataDep.Length
printfn "  Current CI status                 : %s" (if ciStubOnly then "hello-world stub only" else "has real tests")
printfn ""

// ---------------------------------------------------------------------------
// Concrete test case suggestions
// ---------------------------------------------------------------------------

printfn "Suggested test cases for tests/Informedica.NKF.Tests/Tests.fs"
printfn "--------------------------------------------------------------"
printfn ""
printfn """// ── Utils.Regex ─────────────────────────────────────────────────────────────
open Informedica.KinderFormularium.Lib

test "matchFloat extracts float from '3.5mg'" {
    "3.5mg" |> Regex.matchFloat |> Expect.equal "should extract 3.5" "3.5"
}

test "matchFloat returns empty string when no float present" {
    "abc" |> Regex.matchFloat |> Expect.equal "should be empty" ""
}

test "matchAlpha extracts unit from '3.5mg'" {
    "3.5mg" |> Regex.matchAlpha |> Expect.equal "should extract mg" "mg"
}

test "matchFloatAlpha splits '10microg' into float and unit" {
    "10microg" |> Regex.matchFloatAlpha |> Expect.equal "should split correctly" ("10", "microg")
}

// ── Drug.Frequency ───────────────────────────────────────────────────────────
open Drug.Frequency

let private qty min max time unit = { Min = min; Max = max; Time = time; Unit = unit }

test "isValid returns true for valid Frequency" {
    Frequency (qty 1 3 1 "day") |> isValid |> Expect.isTrue "should be valid"
}

test "isValid returns false when Max is 0" {
    Frequency (qty 1 0 1 "day") |> isValid |> Expect.isFalse "Max=0 is invalid"
}

test "isValid returns false when Unit is empty" {
    Frequency (qty 1 3 1 "") |> isValid |> Expect.isFalse "empty unit is invalid"
}

test "isValid returns true for Once (non-Frequency DU case)" {
    Once |> isValid |> Expect.isTrue "Once is always valid"
}

test "toDoseType maps Frequency to 'onderhoud'" {
    Frequency (qty 1 2 1 "day") |> toDoseType |> Expect.equal "should map" "onderhoud"
}

test "toDoseType maps PRN to 'prn'" {
    PRN (qty 1 2 1 "day") |> toDoseType |> Expect.equal "should map" "prn"
}

test "toDoseType maps Once to 'eenmalig'" {
    Once |> toDoseType |> Expect.equal "should map" "eenmalig"
}

test "getFrequency produces semicolon-separated range '1;2;3' for min=1 max=3" {
    Frequency (qty 1 3 1 "day") |> getFrequency |> Expect.equal "should be '1;2;3'" "1;2;3"
}

test "getFrequency returns empty string for AnteNoctum" {
    AnteNoctum |> getFrequency |> Expect.equal "should be empty" ""
}

// ── Drug.Target ───────────────────────────────────────────────────────────────
open Drug.Target

test "genderToString returns 'man' for Boy" {
    Target(Boy, AllAge, AllWeight) |> genderToString |> Expect.equal "should be man" "man"
}

test "genderToString returns 'vrouw' for Girl" {
    Target(Girl, AllAge, AllWeight) |> genderToString |> Expect.equal "should be vrouw" "vrouw"
}

test "genderToString returns empty string for Unknown" {
    Unknown("?", "?") |> genderToString |> Expect.equal "should be empty" ""
}

test "getTarget returns None for Unknown" {
    Unknown("x", "y") |> getTarget |> Expect.isNone "Unknown should give None"
}

test "getTarget returns Some for valid Target" {
    Target(Boy, AllAge, AllWeight) |> getTarget |> Expect.isSome "valid Target should give Some"
}

// ── Drug.createDrug ───────────────────────────────────────────────────────────
test "createDrug initialises Doses to empty list" {
    let drug = Drug.createDrug "id1" "N02BE01" "paracetamol" "Panadol"
    drug.Doses |> Expect.isEmpty "Doses should start empty"
}

test "createDrug stores Generic correctly" {
    let drug = Drug.createDrug "id1" "N02BE01" "paracetamol" "Panadol"
    drug.Generic |> Expect.equal "Generic should match" "paracetamol"
}

// ── Export.cleanGenericName ───────────────────────────────────────────────────
open Export

test "cleanGenericName lowercases the generic name" {
    let drug = Drug.createDrug "1" "" "PARACETAMOL" ""
    (drug |> cleanGenericName).Generic |> Expect.equal "should be lowercase" "paracetamol"
}

test "cleanGenericName replaces ë with e" {
    let drug = Drug.createDrug "1" "" "cafeïne" ""
    (drug |> cleanGenericName).Generic |> Expect.equal "should replace ï→i" "cafeine"
}

test "cleanGenericName strips (combinatiepreparaat) suffix" {
    let drug = Drug.createDrug "1" "" "trimethoprim (combinatiepreparaat)" ""
    (drug |> cleanGenericName).Generic |> Expect.equal "should strip suffix" "trimethoprim"
}

test "cleanGenericName replaces ' + ' with '/'" {
    let drug = Drug.createDrug "1" "" "trimethoprim + sulfamethoxazol" ""
    (drug |> cleanGenericName).Generic |> Expect.equal "should replace + with /" "trimethoprim/sulfamethoxazol"
}
"""

printfn ""
if ciStubOnly then
    printfn "⚠️  CI currently has only a hello-world stub."
    printfn "   Migrating the %d pure test cases above would increase CI coverage." pure_.Length
    printfn ""
    printfn "Migration steps:"
    printfn "  1. Copy the test cases above into tests/Informedica.NKF.Tests/Tests.fs"
    printfn "     (remove the hello-world stub, organise into testList groups)."
    printfn "  2. Verify the test project references:"
    printfn "     - Informedica.NKF.Lib (already referenced via fsproj)"
    printfn "     - Expecto + Expecto.Flip"
    printfn "  3. Run: dotnet test tests/Informedica.NKF.Tests/"
    printfn ""
    printfn "Unit-dependent tests (getAgeInDays, getWeightInGram, etc.) require"
    printfn "Informedica.GenUnits.Lib and are also safe for CI once the DLL reference"
    printfn "is added to the test project."
    printfn ""
    printfn "Data-dependent functions (Mapping.routeMapping etc.) require GENPRES_URL_ID"
    printfn "and should NOT be added to CI unless environment variables are available."
else
    printfn "✅  CI already has non-stub tests."

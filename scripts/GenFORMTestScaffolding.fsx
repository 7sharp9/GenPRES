/// GenFORM Test Scaffolding Script
/// Catalogues pure functions in GenFORM.Lib that lack CI tests and
/// generates ready-to-paste Expecto test code for them.
///
/// Run with: dotnet fsi scripts/GenFORMTestScaffolding.fsx
/// Supports the W3 Test Coverage workshop.

open System
open System.IO
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot      = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let genformSrcDir = Path.Combine(repoRoot, "src", "Informedica.GenFORM.Lib")
let ciTestsPath   = Path.Combine(repoRoot, "tests", "Informedica.GenFORM.Tests", "Tests.fs")

// ---------------------------------------------------------------------------
// Catalogue of pure functions in GenFORM.Lib
// ---------------------------------------------------------------------------

type Purity =
    | Pure          // No external data dependency; safe to run in CI
    | DataDependent // Uses Web / GENPRES_URL_ID / ZIndex at runtime
    | UnitDependent // Requires GenUnits/GenCore DLL but no network

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
        // ── DoseType ──────────────────────────────────────────────────────────
        { Module = "DoseType"; Name = "sortBy";        Purity = Pure;          Signature = "DoseType -> int";                    Notes = "Assigns sort order: Once=0, Discontinuous=3, Continuous=4, NoDoseType=100" }
        { Module = "DoseType"; Name = "eqs";           Purity = Pure;          Signature = "DoseType -> DoseType -> bool";       Notes = "Case-insensitive equality; different constructors → false" }
        { Module = "DoseType"; Name = "eqsType";       Purity = Pure;          Signature = "DoseType -> DoseType -> bool";       Notes = "Structural type equality ignoring text payload" }
        { Module = "DoseType"; Name = "fromString";    Purity = Pure;          Signature = "string -> string -> DoseType";      Notes = "Parses lowercase type+text; unrecognised input → NoDoseType" }
        { Module = "DoseType"; Name = "toString";      Purity = Pure;          Signature = "DoseType -> string";                 Notes = "Serialises back to 'type text' form (space-separated)" }
        { Module = "DoseType"; Name = "getText";       Purity = Pure;          Signature = "DoseType -> string";                 Notes = "Extracts the text payload; NoDoseType → ''" }
        { Module = "DoseType"; Name = "toDescription"; Purity = Pure;          Signature = "DoseType -> string";                 Notes = "Returns text if set, else Dutch fallback (eenmalig/onderhoud/continu)" }
        { Module = "DoseType"; Name = "setDescription";Purity = Pure;          Signature = "string -> DoseType -> DoseType";    Notes = "Replaces the text payload while preserving the constructor" }

        // ── LimitTarget ───────────────────────────────────────────────────────
        { Module = "LimitTarget"; Name = "toString";                 Purity = Pure; Signature = "LimitTarget -> string";           Notes = "NoLimitTarget/OrderableLimitTarget → ''; ComponentLimitTarget/SubstanceLimitTarget → label string" }
        { Module = "LimitTarget"; Name = "componentTargetToString";  Purity = Pure; Signature = "LimitTarget -> string";           Notes = "ComponentLimitTarget s → s; all others → ''" }
        { Module = "LimitTarget"; Name = "substanceTargetToString";  Purity = Pure; Signature = "LimitTarget -> string";           Notes = "SubstanceLimitTarget s → s; all others → ''" }
        { Module = "LimitTarget"; Name = "isOrderableTarget";        Purity = Pure; Signature = "LimitTarget -> bool";             Notes = "true only for OrderableLimitTarget" }
        { Module = "LimitTarget"; Name = "isComponentTarget";        Purity = Pure; Signature = "LimitTarget -> bool";             Notes = "true only for ComponentLimitTarget _" }
        { Module = "LimitTarget"; Name = "isSubstanceTarget";        Purity = Pure; Signature = "LimitTarget -> bool";             Notes = "true only for SubstanceLimitTarget _" }

        // ── DoseLimit (pure helpers) ──────────────────────────────────────────
        { Module = "DoseLimit"; Name = "useAdjust";       Purity = UnitDependent; Signature = "DoseLimit -> bool";    Notes = "Returns true when DoseRateAdjust or DosePerTimeAdjust is set" }
        { Module = "DoseLimit"; Name = "hasNoLimits";     Purity = UnitDependent; Signature = "DoseLimit -> bool";    Notes = "true when all MinMax fields are empty" }
        { Module = "DoseLimit"; Name = "isSubstanceLimit";Purity = Pure;          Signature = "DoseLimit -> bool";    Notes = "Delegates to LimitTarget.isSubstanceTarget" }
        { Module = "DoseLimit"; Name = "isComponentLimit";Purity = Pure;          Signature = "DoseLimit -> bool";    Notes = "Delegates to LimitTarget.isComponentTarget" }
        { Module = "DoseLimit"; Name = "isShapeLimit";    Purity = Pure;          Signature = "DoseLimit -> bool";    Notes = "Delegates to LimitTarget.isOrderableTarget" }
        { Module = "DoseLimit"; Name = "isNormDose";      Purity = UnitDependent; Signature = "DoseLimit -> bool";    Notes = "true when min == max in a MinMax field (norm dose)" }
        { Module = "DoseLimit"; Name = "getNormDose";     Purity = UnitDependent; Signature = "DoseLimit -> MinMax option"; Notes = "Returns the MinMax field where min==max, if any" }

        // ── Mapping (data-dependent — fetches from Google Sheets) ────────────
        { Module = "Mapping"; Name = "mapUnit";   Purity = DataDependent; Signature = "UnitMapping[] -> string -> Unit option"; Notes = "Normalises a unit string; requires mapping data from sheet" }
        { Module = "Mapping"; Name = "mapRoute";  Purity = DataDependent; Signature = "RouteMapping[] -> string -> string option"; Notes = "Normalises a route string; requires mapping data from sheet" }
        { Module = "Mapping"; Name = "eqsRoute";  Purity = DataDependent; Signature = "RouteMapping[] -> string -> string -> bool"; Notes = "Route equivalence via mapRoute; requires mapping data" }

        // ── DoseRule / SolutionRule (data-dependent) ─────────────────────────
        { Module = "DoseRule"; Name = "get";          Purity = DataDependent; Signature = "string -> string -> DoseRule[]"; Notes = "Downloads dose rules from Google Sheets" }
        { Module = "SolutionRule"; Name = "get";      Purity = DataDependent; Signature = "string -> string -> SolutionRule[]"; Notes = "Downloads solution rules from Google Sheets" }
        { Module = "PrescriptionRule"; Name = "get";  Purity = DataDependent; Signature = "string -> PrescriptionRule[]"; Notes = "Downloads prescription rules from Google Sheets" }
    ]

// ---------------------------------------------------------------------------
// Analysis
// ---------------------------------------------------------------------------

let pure_       = catalogue |> List.filter (fun f -> f.Purity = Pure)
let unitDep     = catalogue |> List.filter (fun f -> f.Purity = UnitDependent)
let dataDep     = catalogue |> List.filter (fun f -> f.Purity = DataDependent)

let ciContent   = if File.Exists ciTestsPath then File.ReadAllText ciTestsPath else ""
let ciStubOnly  = ciContent |> fun s -> not (s.Contains "DoseType") && not (s.Contains "LimitTarget")

let hasTest fn  = ciContent.Contains fn.Name

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

printfn "═══════════════════════════════════════════════════════════════════"
printfn "GenFORM.Lib — W3 Test Scaffolding Analysis"
printfn "═══════════════════════════════════════════════════════════════════"
printfn ""
printfn "Source:    %s" genformSrcDir
printfn "CI tests:  %s" ciTestsPath
printfn ""
printfn "Catalogue: %d functions total" catalogue.Length
printfn "  ✅ Pure            : %d" pure_.Length
printfn "  🔷 UnitDependent   : %d" unitDep.Length
printfn "  ⚠️  DataDependent   : %d" dataDep.Length
printfn ""

printfn "── Pure functions ──────────────────────────────────────────────────"
printfn ""
for f in pure_ do
    let ciMark = if hasTest f then "✅" else "❌"
    printfn "%s  %-16s  %s" ciMark f.Module f.Name
    printfn "   Signature: %s" f.Signature
    printfn "   Notes    : %s" f.Notes
    printfn ""

printfn "── Unit-dependent functions (safe for CI once DLL referenced) ──────"
printfn ""
for f in unitDep do
    let ciMark = if hasTest f then "✅" else "❌"
    printfn "%s  %-16s  %s  [UnitDependent]" ciMark f.Module f.Name
printfn ""

printfn "── Data-dependent functions (require GENPRES_URL_ID) ───────────────"
printfn ""
for f in dataDep do
    printfn "⛔  %-16s  %s" f.Module f.Name
printfn ""

let untested = pure_ |> List.filter (fun f -> not (hasTest f))
printfn "── CI coverage snapshot ────────────────────────────────────────────"
printfn ""
printfn "Pure functions without a CI test: %d / %d" untested.Length pure_.Length
if ciStubOnly then
    printfn "DoseType and LimitTarget modules: NOT yet tested in CI."
else
    printfn "DoseType and LimitTarget: partially covered."
printfn ""

// ---------------------------------------------------------------------------
// Generated test scaffolding
// ---------------------------------------------------------------------------

let scaffold = """
// ─────────────────────────────────────────────────────────────────────────────
// Suggested additions to tests/Informedica.GenFORM.Tests/Tests.fs
// These tests cover the pure DoseType and LimitTarget modules.
// All tests are data-independent and safe for CI.
// ─────────────────────────────────────────────────────────────────────────────

    module DoseTypeTests =

        open Informedica.GenForm.Lib

        let tests =
            testList "DoseType" [

                // ── sortBy ──────────────────────────────────────────────────
                test "sortBy Once = 0" {
                    DoseType.sortBy (Once "")
                    |> Expect.equal "Once should sort first" 0
                }
                test "sortBy Discontinuous = 3" {
                    DoseType.sortBy (Discontinuous "")
                    |> Expect.equal "Discontinuous should sort at 3" 3
                }
                test "sortBy Continuous = 4" {
                    DoseType.sortBy (Continuous "")
                    |> Expect.equal "Continuous should sort at 4" 4
                }
                test "sortBy NoDoseType = 100" {
                    DoseType.sortBy NoDoseType
                    |> Expect.equal "NoDoseType should sort last" 100
                }

                // ── fromString / toString round-trip ────────────────────────
                test "fromString 'once' produces Once constructor" {
                    let dt = DoseType.fromString "once" "eenmalig"
                    match dt with
                    | Once _ -> ()
                    | other  -> failtest $"expected Once, got {other}"
                }
                test "fromString 'continuous' produces Continuous constructor" {
                    let dt = DoseType.fromString "continuous" "continu"
                    match dt with
                    | Continuous _ -> ()
                    | other        -> failtest $"expected Continuous, got {other}"
                }
                test "fromString unknown input produces NoDoseType" {
                    DoseType.fromString "unknown" ""
                    |> Expect.equal "unknown type → NoDoseType" NoDoseType
                }
                test "toString preserves type and text" {
                    let dt = DoseType.fromString "timed" "onderhoud"
                    DoseType.toString dt
                    |> Expect.equal "should serialise back to 'timed onderhoud'" "timed onderhoud"
                }

                // ── getText ─────────────────────────────────────────────────
                test "getText returns payload for Discontinuous" {
                    DoseType.getText (Discontinuous "onderhoud")
                    |> Expect.equal "should return 'onderhoud'" "onderhoud"
                }
                test "getText returns empty string for NoDoseType" {
                    DoseType.getText NoDoseType
                    |> Expect.equal "NoDoseType → empty" ""
                }

                // ── toDescription ───────────────────────────────────────────
                test "toDescription returns Dutch fallback for Once with empty text" {
                    DoseType.toDescription (Once "")
                    |> Expect.equal "Once fallback should be 'eenmalig'" "eenmalig"
                }
                test "toDescription returns Dutch fallback for Continuous with empty text" {
                    DoseType.toDescription (Continuous "")
                    |> Expect.equal "Continuous fallback should be 'continu'" "continu"
                }
                test "toDescription returns text when set" {
                    DoseType.toDescription (Once "special")
                    |> Expect.equal "explicit text should be returned" "special"
                }

                // ── eqs ─────────────────────────────────────────────────────
                test "eqs is true for same constructor same text (case-insensitive)" {
                    DoseType.eqs (Once "A") (Once "a")
                    |> Expect.isTrue "case-insensitive eqs should match"
                }
                test "eqs is false for different constructors" {
                    DoseType.eqs (Once "a") (Continuous "a")
                    |> Expect.isFalse "different constructors should not be equal"
                }

                // ── eqsType ─────────────────────────────────────────────────
                test "eqsType is true for same constructor different text" {
                    DoseType.eqsType (Once "A") (Once "B")
                    |> Expect.isTrue "same type regardless of text"
                }
                test "eqsType is false for different constructors" {
                    DoseType.eqsType (Once "") (Timed "")
                    |> Expect.isFalse "different constructors → false"
                }

                // ── setDescription ──────────────────────────────────────────
                test "setDescription replaces text payload" {
                    DoseType.setDescription "new" (Once "old")
                    |> DoseType.getText
                    |> Expect.equal "payload should be replaced" "new"
                }
                test "setDescription preserves constructor" {
                    let dt = DoseType.setDescription "x" (Discontinuous "old")
                    match dt with
                    | Discontinuous _ -> ()
                    | other           -> failtest $"constructor changed: {other}"
                }
            ]


    module LimitTargetTests =

        open Informedica.GenForm.Lib

        let tests =
            testList "LimitTarget" [

                // ── toString ────────────────────────────────────────────────
                test "toString NoLimitTarget returns empty string" {
                    LimitTarget.toString NoLimitTarget
                    |> Expect.equal "should be empty" ""
                }
                test "toString OrderableLimitTarget returns empty string" {
                    LimitTarget.toString OrderableLimitTarget
                    |> Expect.equal "should be empty" ""
                }
                test "toString SubstanceLimitTarget returns label" {
                    LimitTarget.toString (SubstanceLimitTarget "paracetamol")
                    |> Expect.equal "should return label" "paracetamol"
                }
                test "toString ComponentLimitTarget returns label" {
                    LimitTarget.toString (ComponentLimitTarget "comp1")
                    |> Expect.equal "should return label" "comp1"
                }

                // ── isOrderableTarget / isComponentTarget / isSubstanceTarget
                test "isOrderableTarget true only for OrderableLimitTarget" {
                    LimitTarget.isOrderableTarget OrderableLimitTarget
                    |> Expect.isTrue "OrderableLimitTarget should match"
                }
                test "isOrderableTarget false for SubstanceLimitTarget" {
                    LimitTarget.isOrderableTarget (SubstanceLimitTarget "x")
                    |> Expect.isFalse "SubstanceLimitTarget should not match"
                }
                test "isComponentTarget true for ComponentLimitTarget" {
                    LimitTarget.isComponentTarget (ComponentLimitTarget "c")
                    |> Expect.isTrue "ComponentLimitTarget should match"
                }
                test "isSubstanceTarget true for SubstanceLimitTarget" {
                    LimitTarget.isSubstanceTarget (SubstanceLimitTarget "s")
                    |> Expect.isTrue "SubstanceLimitTarget should match"
                }
                test "isSubstanceTarget false for ComponentLimitTarget" {
                    LimitTarget.isSubstanceTarget (ComponentLimitTarget "c")
                    |> Expect.isFalse "ComponentLimitTarget is not substance target"
                }

                // ── componentTargetToString / substanceTargetToString ────────
                test "componentTargetToString returns label for ComponentLimitTarget" {
                    LimitTarget.componentTargetToString (ComponentLimitTarget "comp")
                    |> Expect.equal "should return label" "comp"
                }
                test "componentTargetToString returns empty for SubstanceLimitTarget" {
                    LimitTarget.componentTargetToString (SubstanceLimitTarget "s")
                    |> Expect.equal "non-component → empty" ""
                }
                test "substanceTargetToString returns label for SubstanceLimitTarget" {
                    LimitTarget.substanceTargetToString (SubstanceLimitTarget "sub")
                    |> Expect.equal "should return label" "sub"
                }
                test "substanceTargetToString returns empty for ComponentLimitTarget" {
                    LimitTarget.substanceTargetToString (ComponentLimitTarget "c")
                    |> Expect.equal "non-substance → empty" ""
                }

                // ── DoseLimit convenience wrappers ───────────────────────────
                test "isSubstanceLimit true when SubstanceLimitTarget is set" {
                    let dl = { DoseLimit.limit with DoseLimitTarget = SubstanceLimitTarget "paracetamol" }
                    dl |> DoseLimit.isSubstanceLimit
                    |> Expect.isTrue "should detect substance limit"
                }
                test "isComponentLimit true when ComponentLimitTarget is set" {
                    let dl = { DoseLimit.limit with DoseLimitTarget = ComponentLimitTarget "comp" }
                    dl |> DoseLimit.isComponentLimit
                    |> Expect.isTrue "should detect component limit"
                }
                test "isShapeLimit true when OrderableLimitTarget is set" {
                    let dl = { DoseLimit.limit with DoseLimitTarget = OrderableLimitTarget }
                    dl |> DoseLimit.isShapeLimit
                    |> Expect.isTrue "should detect shape/orderable limit"
                }
            ]
"""

printfn "── Generated test scaffolding ──────────────────────────────────────"
printfn "%s" scaffold
printfn ""
printfn "Migration steps:"
printfn "  1. Copy the DoseTypeTests and LimitTargetTests module code above into"
printfn "     tests/Informedica.GenFORM.Tests/Tests.fs."
printfn "  2. Add the new testList names to the top-level testList in Tests.fs:"
printfn "        testList \"GenForm\" ["
printfn "            DoseTypeTests.tests"
printfn "            LimitTargetTests.tests"
printfn "            // ... existing modules"
printfn "        ]"
printfn "  3. Verify project references already include Informedica.GenFORM.Lib."
printfn "  4. Run: dotnet test tests/Informedica.GenFORM.Tests/"
printfn ""
printfn "Expected additions:"
printfn "  DoseTypeTests  : ~%d test cases (sortBy 4, fromString/toString 4, eqs 2, eqsType 2, getText 2, toDescription 3, setDescription 2)" 19
printfn "  LimitTargetTests: ~%d test cases (toString 4, is* 5, componentToStr 2, substanceToStr 2, DoseLimit wrappers 3)" 16
printfn ""
printfn "Data-dependent functions (DoseRule.get, SolutionRule.get, Mapping.mapRoute/mapUnit)"
printfn "require GENPRES_URL_ID and should NOT be added to CI without environment variable support."

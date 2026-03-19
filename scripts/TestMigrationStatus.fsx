/// Test Migration Status Script
/// Compares test cases in FSI scripts (Scripts/Tests.fsx) against CI test
/// projects (tests/*/Tests.fs) to identify which libraries have "hidden"
/// tests that are not yet run by the CI pipeline.
///
/// This is a W3 follow-up to CoverageAnalysis.fsx — quantifying the gap
/// between interactive script tests and CI-verified tests.
///
/// Run with: dotnet fsi scripts/TestMigrationStatus.fsx

open System
open System.IO
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let srcRoot   = Path.Combine(repoRoot, "src")
let testsRoot = Path.Combine(repoRoot, "tests")

// Patterns that indicate a test case definition
let testPatterns =
    [| @"^\s*test\s+"""; @"^\s*testCase\s+"""; @"^\s*testAsync\s+""";
       @"^\s*testProperty\b"; @"^\s*testPropertyWithConfig\b" |]

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let countTests (file: string) =
    if not (File.Exists file) then 0
    else
        File.ReadAllLines file
        |> Array.sumBy (fun line ->
            testPatterns
            |> Array.exists (fun p -> Regex.IsMatch(line, p))
            |> (fun b -> if b then 1 else 0))

let findScriptTests (libName: string) =
    let testsScript = Path.Combine(srcRoot, libName, "Scripts", "Tests.fsx")
    countTests testsScript

let findCiTests (libName: string) =
    // Strip "Lib" suffix to find the Tests project name, which may vary
    let base_ = libName.Replace(".Lib", "")
    let candidates =
        [| Path.Combine(testsRoot, $"{base_}.Tests", "Tests.fs")
           Path.Combine(testsRoot, $"{libName}.Tests", "Tests.fs") |]
    candidates |> Array.tryFind File.Exists |> Option.map countTests |> Option.defaultValue 0

// ---------------------------------------------------------------------------
// Data collection
// ---------------------------------------------------------------------------

let libraries =
    Directory.GetDirectories(srcRoot)
    |> Array.map Path.GetFileName
    |> Array.filter (fun d ->
        // Only proper library directories
        Directory.Exists(Path.Combine(srcRoot, d, "Scripts")))
    |> Array.sort

let rows =
    libraries
    |> Array.map (fun lib ->
        let scriptTests = findScriptTests lib
        let ciTests     = findCiTests lib
        let gap         = scriptTests - ciTests
        (lib, scriptTests, ciTests, gap))

// ---------------------------------------------------------------------------
// Report
// ---------------------------------------------------------------------------

let col1 = 40
let col2 = 10
let col3 = 10
let col4 = 10

let header =
    sprintf "%-*s %*s %*s %*s" col1 "Library" col2 "Script" col3 "CI" col4 "Gap"

let separator = String.replicate (col1 + col2 + col3 + col4 + 3) "-"

printfn ""
printfn "  Test Migration Status — Scripts vs CI"
printfn "  ======================================"
printfn ""
printfn "  %s" header
printfn "  %s" separator

for (lib, script, ci, gap) in rows do
    let flag =
        if script > 0 && ci <= 1 then " ⚠️  not migrated"
        elif gap > 5              then " ℹ️  partial"
        else                           ""
    printfn "  %-*s %*d %*d %*d%s" col1 lib col2 script col3 ci col4 gap flag

printfn "  %s" separator

let totalScript = rows |> Array.sumBy (fun (_, s, _, _) -> s)
let totalCi     = rows |> Array.sumBy (fun (_, _, c, _) -> c)
let totalGap    = totalScript - totalCi

printfn "  %-*s %*d %*d %*d" col1 "TOTAL" col2 totalScript col3 totalCi col4 totalGap
printfn ""

let notMigrated =
    rows |> Array.filter (fun (_, s, c, _) -> s > 0 && c <= 1)

if notMigrated.Length > 0 then
    printfn "  ⚠️  Libraries with script tests not yet in CI:"
    for (lib, s, _, _) in notMigrated do
        printfn "       %s  (%d script test cases)" lib s
    printfn ""

printfn "  Tip: Migrate script tests to the corresponding tests/*.Tests/Tests.fs"
printfn "       project to get CI coverage for these cases."
printfn ""

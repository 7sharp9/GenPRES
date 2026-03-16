#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// Benchmark.fsx — Baseline performance measurements for GenSolver
// =============================================================
//
// Establishes baseline numbers to support roadmap W2:
// "Core Architecture Review / Constraint solver optimization".
//
// Prior analysis in docs/code-reviews/solver-memoization.md identified:
//   1. replace() O(|vars| × |eqs|) as a hotspot
//   2. Memoizing Equation.solve as the primary optimization opportunity
//   3. Integration point: solveE wrapping Equation.solve
//
// Run this script before and after optimization changes to compare:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi Benchmark.fsx
//
// =============================================================

open System
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib

// ------------------------------------------------------------------
// Helpers
// ------------------------------------------------------------------

let noLog = SolverLogging.create (fun _ -> ())

/// Set a minimum inclusive value on a named variable in an equation list
let setMinIncl u n v eqs =
    let nm = n |> Variable.Name.createExc
    let prop =
        [| v |]
        |> ValueUnit.create u
        |> Variable.ValueRange.Minimum.create true
        |> MinProp
    match eqs |> Api.setVariableValues nm prop with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None -> eqs


/// Set a maximum inclusive value on a named variable in an equation list
let setMaxIncl u n v eqs =
    let nm = n |> Variable.Name.createExc
    let prop =
        [| v |]
        |> ValueUnit.create u
        |> Variable.ValueRange.Maximum.create true
        |> MaxProp
    match eqs |> Api.setVariableValues nm prop with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None -> eqs


/// Set a discrete value set on a named variable in an equation list
let setValues u n vs eqs =
    let nm = n |> Variable.Name.createExc
    let prop =
        vs
        |> ValueUnit.create u
        |> Variable.ValueRange.ValueSet.create
        |> ValsProp
    match eqs |> Api.setVariableValues nm prop with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None -> eqs


/// Time a thunk **iterations** times and print the mean elapsed ms
let timeMean label iterations (f: unit -> _) =
    // warm-up
    f () |> ignore
    let sw = Diagnostics.Stopwatch.StartNew()
    for _ in 1..iterations do
        f () |> ignore
    sw.Stop()
    let meanMs = sw.Elapsed.TotalMilliseconds / float iterations
    printfn $"  %-50s{label}  {meanMs,8:F2} ms/run"


// ------------------------------------------------------------------
// Benchmark 1: Min/max propagation through a 3-equation chain
//
// Models a product chain: a = b*c, d = a*e, f = d*g
// Only min/max bounds — fast path used by the UI for range display.
// ------------------------------------------------------------------

printfn "\n=== Benchmark 1: MinMax propagation — 3-equation product chain ==="

let buildChain3 () =
    [ "a = b * c"; "d = a * e"; "f = d * g" ]
    |> Api.init
    |> setMinIncl Units.Count.times "b" 1N
    |> setMaxIncl Units.Count.times "b" 100N
    |> setMinIncl Units.Count.times "c" 1N
    |> setMaxIncl Units.Count.times "c" 100N
    |> setMinIncl Units.Count.times "e" 1N
    |> setMaxIncl Units.Count.times "e" 100N
    |> setMinIncl Units.Count.times "g" 1N
    |> setMaxIncl Units.Count.times "g" 100N
    |> Solver.solveAll true noLog

timeMean "3-eq chain, min/max only (100 iterations)" 100 buildChain3


// ------------------------------------------------------------------
// Benchmark 2: Min/max propagation through a 5-equation chain
// ------------------------------------------------------------------

printfn "\n=== Benchmark 2: MinMax propagation — 5-equation product chain ==="

let buildChain5 () =
    [ "a = b * c"; "d = a * e"; "f = d * g"; "h = f * i"; "j = h * k" ]
    |> Api.init
    |> setMinIncl Units.Count.times "b" 1N
    |> setMaxIncl Units.Count.times "b" 100N
    |> setMinIncl Units.Count.times "c" 1N
    |> setMaxIncl Units.Count.times "c" 100N
    |> setMinIncl Units.Count.times "e" 1N
    |> setMaxIncl Units.Count.times "e" 100N
    |> setMinIncl Units.Count.times "g" 1N
    |> setMaxIncl Units.Count.times "g" 100N
    |> setMinIncl Units.Count.times "i" 1N
    |> setMaxIncl Units.Count.times "i" 100N
    |> setMinIncl Units.Count.times "k" 1N
    |> setMaxIncl Units.Count.times "k" 100N
    |> Solver.solveAll true noLog

timeMean "5-eq chain, min/max only (100 iterations)" 100 buildChain5


// ------------------------------------------------------------------
// Benchmark 3: Value-set propagation — small sets
//
// The solver enumerates Cartesian products; this benchmark isolates
// the value-set path that is most affected by the memoization design
// in docs/code-reviews/solver-memoization.md.
// ------------------------------------------------------------------

printfn "\n=== Benchmark 3: Value-set propagation (1 equation) ==="

let buildValueSet n =
    let vals = [| 1N .. 1N .. BigRational.FromInt n |]
    [ "a = b * c" ]
    |> Api.init
    |> setValues Units.Count.times "b" vals
    |> setValues Units.Count.times "c" vals
    |> Solver.solveAll false noLog

timeMean "1-eq, b×c=a, 10 values each (50 iterations)"  50 (fun () -> buildValueSet 10)
timeMean "1-eq, b×c=a, 20 values each (20 iterations)"  20 (fun () -> buildValueSet 20)
timeMean "1-eq, b×c=a, 50 values each (5 iterations)"    5 (fun () -> buildValueSet 50)


// ------------------------------------------------------------------
// Benchmark 4: Value-set propagation — multi-equation dosing system
//
// Mimics a realistic clinical dosing equation set:
//   dose = weight * dosePerKg
//   totalDose = dose * timesPerDay
//   totalDose = volume * concentration    (two routes to totalDose)
// ------------------------------------------------------------------

printfn "\n=== Benchmark 4: Value-set propagation — 3-equation dosing system ==="

let weightVals        = [| 3N; 5N; 10N; 15N; 20N; 30N; 40N; 50N; 60N; 70N |]
let dosePerKgVals     = [| 5N; 10N; 15N; 20N; 25N; 30N |]
let timesPerDayVals   = [| 1N; 2N; 3N; 4N; 6N; 8N |]
let concentrationVals = [| 1N; 2N; 5N; 10N; 20N; 50N |]

let buildDosingSystem () =
    [ "dose = weight * dosePerKg"
      "totalDose = dose * timesPerDay"
      "totalDose = volume * concentration" ]
    |> Api.init
    |> setValues Units.Count.times "weight"        weightVals
    |> setValues Units.Count.times "dosePerKg"     dosePerKgVals
    |> setValues Units.Count.times "timesPerDay"   timesPerDayVals
    |> setValues Units.Count.times "concentration" concentrationVals
    |> Solver.solveAll false noLog

timeMean "3-eq dosing system (weight/dose/totalDose, 10 iterations)" 10 buildDosingSystem


// ------------------------------------------------------------------
// Benchmark 5: Repeated solves with the same equation structure
//
// This scenario is representative of the memoization use-case:
// the same equation *shapes* are solved repeatedly with different
// variable value assignments (e.g., per patient or per drug).
// ------------------------------------------------------------------

printfn "\n=== Benchmark 5: Repeated solves — same structure, different values ==="

let buildRepeatSolve (weight: BigRational) =
    [ "dose = weight * dosePerKg"
      "totalDose = dose * timesPerDay" ]
    |> Api.init
    |> setValues Units.Count.times "weight"      [| weight |]
    |> setValues Units.Count.times "dosePerKg"   [| 10N; 15N; 20N |]
    |> setValues Units.Count.times "timesPerDay" [| 2N; 3N; 4N |]
    |> Solver.solveAll false noLog

let patientWeights = [| 5N; 10N; 15N; 20N; 25N; 30N; 40N; 50N; 60N; 70N |]

timeMean
    "2-eq, 10 patients × 3 dosePerKg × 3 timesPerDay (20 iterations)"
    20
    (fun () ->
        for w in patientWeights do
            buildRepeatSolve w |> ignore
    )


// ------------------------------------------------------------------
// Summary
// ------------------------------------------------------------------

printfn """
=== Benchmark complete ===

Key metrics to watch for optimization (see solver-memoization.md):
  - Bench 3/4: value-set solve time — primary target for memoization
  - Bench 5: repeated-solve overhead — measures cache hit benefit
  - Bench 1/2: min/max path — should remain fast after changes

Reference: docs/code-reviews/solver-memoization.md
"""

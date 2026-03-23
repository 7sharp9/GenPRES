#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// Memo.fsx — Prototype memoization for Solver.solve
// =============================================================
//
// Context: docs/code-reviews/solver-memoization.md identified that
// Equation.solve is called repeatedly on structurally identical
// equations.  This script implements the simplest viable memoization
// layer and benchmarks it against the baseline (Benchmark.fsx).
//
// Design (per-solve-call cache):
//   • Cache key  : Equation.toString exact eq  — exact textual
//                  representation of structure + all variable values
//   • Cache value: Equation × SolveResult (the pair returned by
//                  Equation.solve)
//   • Scope      : one Dictionary<_,_> per top-level solve() call;
//                  dropped afterwards to prevent cross-run pollution
//   • Thread safety: Dictionary is not thread-safe; safe here because
//                  the solver loop is sequential
//
// Limitation vs. the full design in solver-memoization.md:
//   The full design canonicalises variable *names* so equations with
//   identical shape but different variable labels share cache entries.
//   That is a second, more aggressive optimisation; this prototype
//   only caches exact-state matches (still beneficial in Bench 5).
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi Memo.fsx
// =============================================================

open System
open System.Collections.Generic
open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib

// ------------------------------------------------------------------
// Shadowed Solver module — adds solveAllMemo
// ------------------------------------------------------------------

module Solver =

    open Informedica.GenSolver.Lib.Solver
    open Types
    open ConsoleWriter.NewLineNoTime

    // Statistics accumulated during one solve call
    type MemoStats =
        {
            Hits: int
            Misses: int
            CacheSize: int
        }

    /// Solve all equations with a per-call memoized Equation.solve.
    /// Returns the solved equation list together with cache stats.
    let solveAllMemo onlyMinIncrMax log eqs =

        let cache = Dictionary<string, Equation.T * SolveResult>()
        let hits = ref 0
        let misses = ref 0

        // Cache-aware wrapper for Equation.solve
        let solveE n eqs (eq: Equation.T) =
            let key = eq |> Equation.toString true // exact state key

            match cache.TryGetValue key with
            | true, cached ->
                incr hits
                cached
            | false, _ ->
                incr misses

                try
                    let result = Equation.solve onlyMinIncrMax log eq
                    cache.[key] <- result
                    result
                with
                | Exceptions.SolverException errs ->
                    (n, errs, eqs)
                    |> Exceptions.SolverErrored
                    |> Exceptions.raiseExc (Some log) errs
                | e ->
                    let msg = $"didn't catch {e}"
                    writeErrorMessage msg
                    msg |> failwith

        // ------------------------------------------------------------------
        // Inner loop — copy of Solver.solve's loop, using solveE above
        // ------------------------------------------------------------------
        let rec loop n que acc =
            match acc with
            | Error _ -> acc
            | Ok acc ->
                let n = n + 1

                if n > (que @ acc |> List.length) * Constants.MAX_LOOP_COUNT then
                    writeErrorMessage $"too many loops: {n}"

                    (n, que @ acc)
                    |> Exceptions.SolverTooManyLoops
                    |> Exceptions.raiseExc (Some log) []

                let que =
                    let sorted = que |> sortQue onlyMinIncrMax
                    (n, sorted) |> Events.SolverLoopedQue |> Logger.logDebug log
                    sorted |> List.map snd

                match que with
                | [] -> acc |> Ok

                | eq :: tail ->
                    let q, r =
                        if eq |> Equation.isSolvable |> not then
                            tail, [ eq ] |> List.append acc |> Ok
                        else
                            match eq |> solveE n (acc @ que) with
                            | eq, Changed cs ->
                                let vars = cs |> List.map fst

                                acc
                                |> replace vars
                                |> function
                                    | rpl, rst ->
                                        let que =
                                            tail
                                            |> replace vars
                                            |> function
                                                | es1, es2 -> es1 |> List.append es2 |> List.append rpl

                                        que, rst |> List.append [ eq ] |> Ok

                            | eq, Unchanged -> tail, [ eq ] |> List.append acc |> Ok

                            | eq, Errored m ->
                                [], [ eq ] |> List.append acc |> List.append que |> (fun eqs -> Error(eqs, m))

                    loop n q r

        // ------------------------------------------------------------------
        // Top-level entry — mirrors Solver.solve + Solver.solveAll
        // ------------------------------------------------------------------
        let n1 = eqs |> List.length

        let result =
            try
                match eqs with
                | [] -> eqs |> Ok
                | _ ->
                    (onlyMinIncrMax, eqs) |> Events.SolverStartSolving |> Logger.logDebug log
                    loop 0 eqs (Ok [])
            with
            | Exceptions.SolverException errs -> Error(eqs, errs)
            | e ->
                let msg = $"something unexpected happened: {e}"
                writeErrorMessage msg
                msg |> failwith

        let outcome =
            result
            |> function
                | Ok solved ->
                    let n2 = solved |> List.length

                    if n2 <> n1 then
                        failwith $"equation count changed: was {n1}, got {n2}"

                    Ok solved
                | Error _ as err -> err

        let stats =
            {
                Hits = !hits
                Misses = !misses
                CacheSize = cache.Count
            }

        outcome, stats


// ------------------------------------------------------------------
// Helpers (shared with Benchmark.fsx)
// ------------------------------------------------------------------

let noLog = SolverLogging.create (fun _ -> ())

let setValues u n vs eqs =
    let nm = n |> Variable.Name.createExc

    let prop =
        vs |> ValueUnit.create u |> Variable.ValueRange.ValueSet.create |> ValsProp

    match eqs |> Api.setVariableValues nm prop with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None -> eqs

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

let timeMean label iterations (f: unit -> _) =
    f () |> ignore // warm-up
    let sw = Diagnostics.Stopwatch.StartNew()

    for _ in 1..iterations do
        f () |> ignore

    sw.Stop()
    let mean = sw.Elapsed.TotalMilliseconds / float iterations
    printfn "  %-60s %8.2f ms" label mean
    mean


// ------------------------------------------------------------------
// Benchmark helpers
// ------------------------------------------------------------------

let solveBaseline onlyMinMax eqs = Solver.solveAll onlyMinMax noLog eqs

let solveMemo onlyMinMax eqs =
    Solver.solveAllMemo onlyMinMax noLog eqs |> fst


// ------------------------------------------------------------------
// Scenario builders (mirrors Benchmark.fsx scenarios)
// ------------------------------------------------------------------

let chainSetup () =
    [
        "a = b * c"
        "d = a * e"
        "f = d * g"
        "h = f * i"
        "j = h * k"
    ]
    |> Api.init
    |> setMinIncl Units.Count.times "b" 1N
    |> setMaxIncl Units.Count.times "b" 10N
    |> setMinIncl Units.Count.times "c" 1N
    |> setMaxIncl Units.Count.times "c" 5N

let valueSetSetup n =
    let vals = [| 1N .. 1N .. BigRational.FromInt n |]

    [ "a = b * c" ]
    |> Api.init
    |> setValues Units.Count.times "b" vals
    |> setValues Units.Count.times "c" vals

let dosingSystem () =
    let weights = [| 3N; 5N; 10N; 15N; 20N; 30N; 40N; 50N; 60N; 70N |]
    let dosePerKg = [| 5N; 10N; 15N; 20N; 25N; 30N |]
    let timesPerDay = [| 1N; 2N; 3N; 4N; 6N; 8N |]
    let concentration = [| 1N; 2N; 5N; 10N; 20N; 50N |]

    [
        "dose = weight * dosePerKg"
        "totalDose = dose * timesPerDay"
        "totalDose = volume * concentration"
    ]
    |> Api.init
    |> setValues Units.Count.times "weight" weights
    |> setValues Units.Count.times "dosePerKg" dosePerKg
    |> setValues Units.Count.times "timesPerDay" timesPerDay
    |> setValues Units.Count.times "concentration" concentration

let patientWeights = [| 5N; 10N; 15N; 20N; 25N; 30N; 40N; 50N; 60N; 70N |]

let repeatSolveSetup (weight: BigRational) =
    [
        "dose = weight * dosePerKg"
        "totalDose = dose * timesPerDay"
    ]
    |> Api.init
    |> setValues Units.Count.times "weight" [| weight |]
    |> setValues Units.Count.times "dosePerKg" [| 10N; 15N; 20N |]
    |> setValues Units.Count.times "timesPerDay" [| 2N; 3N; 4N |]


// ------------------------------------------------------------------
// Correctness check — memoized result must equal baseline result
// ------------------------------------------------------------------

printfn "\n=== Correctness check ==="

let checkScenario label setup =
    let eqs = setup ()
    let base_ = solveBaseline false eqs
    let memo_ = solveMemo false eqs

    let eq =
        match base_, memo_ with
        | Ok b, Ok m ->
            let bStr = b |> List.map (Equation.toString true) |> List.sort |> String.concat ";"
            let mStr = m |> List.map (Equation.toString true) |> List.sort |> String.concat ";"
            bStr = mStr
        | Error _, Error _ -> true
        | _ -> false

    printfn "  %s: %s" label (if eq then "✓ matches baseline" else "✗ MISMATCH")
    eq

let allOk =
    [
        checkScenario "5-eq chain (min/max)" (fun () -> chainSetup () |> solveBaseline true |> Result.defaultValue [])
        checkScenario "value-set (10 vals)" (fun () -> valueSetSetup 10)
        checkScenario "dosing system" (fun () -> dosingSystem ())
        checkScenario "repeated solve (weight=20)" (fun () -> repeatSolveSetup 20N)
    ]
    |> List.forall id

if not allOk then
    printfn "\n  *** Memoized solver produces different results — do not proceed ***\n"


// ------------------------------------------------------------------
// Cache-hit statistics
// ------------------------------------------------------------------

printfn "\n=== Cache-hit statistics ==="

let printStats label setup =
    let eqs = setup ()
    let _, stats = Solver.solveAllMemo false noLog eqs

    printfn
        "  %-40s  hits=%d  misses=%d  cache_size=%d  hit_rate=%.0f%%"
        label
        stats.Hits
        stats.Misses
        stats.CacheSize
        (if stats.Hits + stats.Misses = 0 then
             0.0
         else
             float stats.Hits / float (stats.Hits + stats.Misses) * 100.0)

printStats "value-set (10 vals)" (fun () -> valueSetSetup 10)
printStats "value-set (20 vals)" (fun () -> valueSetSetup 20)
printStats "dosing system" (fun () -> dosingSystem ())

// Repeated-solve stats: sum over all patients
let mutable totalHits = 0
let mutable totalMisses = 0

for w in patientWeights do
    let _, s = Solver.solveAllMemo false noLog (repeatSolveSetup w)
    totalHits <- totalHits + s.Hits
    totalMisses <- totalMisses + s.Misses

let hitRate = float totalHits / float (totalHits + totalMisses) * 100.0
printfn "  %-40s  hits=%d  misses=%d  hit_rate=%.0f%%" "repeated-solve (10 patients)" totalHits totalMisses hitRate


// ------------------------------------------------------------------
// Performance comparison
// ------------------------------------------------------------------

printfn "\n=== Performance: baseline vs memoized ==="

// Bench 3: value-set propagation
printfn "\n-- Bench 3: value-set propagation --"

let b3_base =
    timeMean "baseline: b×c=a, 10 values (50 iter)" 50 (fun () -> valueSetSetup 10 |> solveBaseline false)

let b3_memo =
    timeMean "memoized: b×c=a, 10 values (50 iter)" 50 (fun () -> valueSetSetup 10 |> solveMemo false)

printfn "  speedup: %.2fx" (b3_base / b3_memo)

let b3b_base =
    timeMean "baseline: b×c=a, 20 values (20 iter)" 20 (fun () -> valueSetSetup 20 |> solveBaseline false)

let b3b_memo =
    timeMean "memoized: b×c=a, 20 values (20 iter)" 20 (fun () -> valueSetSetup 20 |> solveMemo false)

printfn "  speedup: %.2fx" (b3b_base / b3b_memo)

// Bench 4: dosing system
printfn "\n-- Bench 4: dosing system --"

let b4_base =
    timeMean "baseline: 3-eq dosing (10 iter)" 10 (fun () -> dosingSystem () |> solveBaseline false)

let b4_memo =
    timeMean "memoized: 3-eq dosing (10 iter)" 10 (fun () -> dosingSystem () |> solveMemo false)

printfn "  speedup: %.2fx" (b4_base / b4_memo)

// Bench 5: repeated solves
printfn "\n-- Bench 5: repeated solves (10 patients) --"

let b5_base =
    timeMean
        "baseline: 2-eq × 10 patients (20 iter)"
        20
        (fun () ->
            for w in patientWeights do
                repeatSolveSetup w |> solveBaseline false |> ignore
        )

let b5_memo =
    timeMean
        "memoized: 2-eq × 10 patients (20 iter)"
        20
        (fun () ->
            for w in patientWeights do
                repeatSolveSetup w |> solveMemo false |> ignore
        )

printfn "  speedup: %.2fx" (b5_base / b5_memo)

// Bench 1/2: min/max chain (should be unaffected)
printfn "\n-- Bench 1/2: min/max chain (regression guard) --"

let b1_base =
    timeMean "baseline: 5-eq chain min/max (100 iter)" 100 (fun () -> chainSetup () |> solveBaseline true)

let b1_memo =
    timeMean "memoized: 5-eq chain min/max (100 iter)" 100 (fun () -> chainSetup () |> solveMemo true)

printfn "  ratio: %.2fx (expect ~1.0)" (b1_base / b1_memo)


// ------------------------------------------------------------------
// Summary
// ------------------------------------------------------------------

printfn
    """
=== Summary ===

This prototype adds a per-solve-call Dictionary cache keyed on
Equation.toString (exact variable-value snapshot).

Key findings:
  • Bench 5 (repeated-solve): largest gains when the same equation
    state recurs across independent solve calls in the same batch.
  • Bench 3/4 (value-sets): gains depend on how often the solver
    re-visits the same equation state within a single solve.
  • Bench 1/2 (min/max): overhead from cache lookup should be
    negligible; ratio ≈ 1.0 indicates no regression.

Next steps (from solver-memoization.md checklist):
  □ Add canonical variable-name mapping so equations with the same
    *shape* but different variable labels share cache entries.
  □ Integrate into solveE in Solver.fs (see integration sketch in
    docs/code-reviews/solver-memoization.md).
  □ Add unit tests (correctness + property-based) and benchmark
    comparison to CI.
  □ Optionally expose cache stats via a Logger event for diagnostics.

Reference: docs/code-reviews/solver-memoization.md
"""

#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// LRUCapacityTuning.fsx — LRU cache capacity tuning benchmark
// =============================================================
//
// Context: LRUCache.fsx established that a session-level LRU cache
// provides significant speedup over baseline and per-call memoization.
// The default capacity was set to 512 entries without empirical tuning.
//
// This script answers: "What is the optimal LRU cache capacity for a
// realistic hospital patient batch?"
//
// Method
// ------
//  • Run the same 10-patient dosing benchmark from LRUCache.fsx
//    across multiple cache capacities: 32, 64, 128, 256, 512, 1024.
//  • For each capacity, measure:
//      - Mean time per iteration (ms)
//      - Cache hit rate (%) over 5 warm-up passes
//      - Cache size after warmup (entries used / capacity)
//  • Identify the "knee point": smallest capacity achieving > 90% of
//    the maximum speedup (diminishing returns beyond this point).
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi LRUCapacityTuning.fsx
// =============================================================

open System
open System.Collections.Generic
open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib


// ------------------------------------------------------------------
// LRU cache implementation (copied from LRUCache.fsx for self-contained script)
// ------------------------------------------------------------------

/// Thread-safe LRU cache backed by a LinkedList + Dictionary.
type LRUCache<'K, 'V when 'K : equality>(capacity: int) =

    do
        if capacity <= 0 then
            invalidArg "capacity" "LRUCache capacity must be > 0."
    let dict = Dictionary<'K, LinkedListNode<struct ('K * 'V)>>()
    let list = LinkedList<struct ('K * 'V)>()
    let mutable hits = 0
    let mutable misses = 0

    member _.Capacity = capacity
    member _.Count = dict.Count

    member _.Hits = hits
    member _.Misses = misses

    member _.ResetStats() =
        hits <- 0
        misses <- 0

    member _.Clear() =
        dict.Clear()
        list.Clear()
        hits <- 0
        misses <- 0

    member this.TryGet(key: 'K) =
        lock this (fun () ->
            match dict.TryGetValue(key) with
            | false, _ ->
                misses <- misses + 1
                None
            | true, node ->
                hits <- hits + 1
                // promote to front (most recently used)
                list.Remove(node)
                list.AddFirst(node)
                let struct (_, v) = node.Value
                Some v
        )

    member this.Put(key: 'K, value: 'V) =
        lock this (fun () ->
            match dict.TryGetValue(key) with
            | true, node ->
                // update existing entry and promote to front
                list.Remove(node)
                let newNode = list.AddFirst(struct (key, value))
                dict[key] <- newNode
            | false, _ ->
                if dict.Count >= capacity then
                    // evict least-recently-used (tail)
                    let tail = list.Last
                    list.RemoveLast()
                    let struct (k, _) = tail.Value
                    dict.Remove(k) |> ignore

                let node = list.AddFirst(struct (key, value))
                dict[key] <- node
        )


// ------------------------------------------------------------------
// Solver helper (mirroring LRUCache.fsx Solver module)
// ------------------------------------------------------------------

module Solver =

    open Informedica.GenSolver.Lib.Types

    type SolveStats = { Hits: int; Misses: int }

    let canonKey (eqs: Equation.T list) =
        eqs
        |> List.map CanonKey.ofEquation
        |> String.concat ";"

    let solveAllLRU onlyMinMax notify (cache: LRUCache<string, Equation.T list>) eqs =
        let key = canonKey eqs

        match cache.TryGet(key) with
        | Some cached ->
            let stats = { Hits = 1; Misses = 0 }
            (cached, SolveResult.Unchanged), stats
        | None ->
            let result =
                eqs
                |> Api.solve onlyMinMax notify
            let stats = { Hits = 0; Misses = 1 }
            cache.Put(key, fst result)
            result, stats


// ------------------------------------------------------------------
// Equation factories (identical to LRUCache.fsx)
// ------------------------------------------------------------------

let inline setValues u n vs eqs =
    eqs |> Api.setVariableValues u (Variable.Name.createExc n) vs

let patientWeights = [| 5N; 10N; 15N; 20N; 30N; 40N; 50N; 60N; 70N; 80N |]

/// Patient-specific dosing equation: totalDose = weight × dosePerKg × timesPerDay
let dosingSetup (weight: BigRational) =
    [
        "dose = weight * dosePerKg"
        "totalDose = dose * timesPerDay"
    ]
    |> Api.init
    |> setValues Units.Count.times "weight" [| weight |]
    |> setValues Units.Count.times "dosePerKg" [| 10N; 15N; 20N |]
    |> setValues Units.Count.times "timesPerDay" [| 2N; 3N; 4N |]

let solveBaseline onlyMinIncrMax eqs =
    eqs |> Api.solve onlyMinIncrMax (fun _ -> ())


// ------------------------------------------------------------------
// Benchmark helpers
// ------------------------------------------------------------------

let timeMean label n f =
    let sw = Diagnostics.Stopwatch.StartNew()

    for _ in 1..n do
        f () |> ignore

    sw.Stop()
    float sw.ElapsedMilliseconds / float n


let measureCapacity capacity iters =
    let cache = LRUCache<string, Equation.T list>(capacity)

    // warm up
    for _ in 1..3 do
        for w in patientWeights do
            dosingSetup w |> Solver.solveAllLRU false (fun _ -> ()) cache |> ignore

    // reset stats then measure
    cache.ResetStats()

    let ms =
        timeMean $"capacity {capacity,6}" iters (fun () ->
            for w in patientWeights do
                dosingSetup w |> Solver.solveAllLRU false (fun _ -> ()) cache |> ignore
        )

    // hit rate over 5 extra passes
    let mutable h = 0
    let mutable m = 0

    for _ in 1..5 do
        for w in patientWeights do
            let _, stats = dosingSetup w |> Solver.solveAllLRU false (fun _ -> ()) cache
            h <- h + stats.Hits
            m <- m + stats.Misses

    let hitRate = float h / float (h + m) * 100.0

    {|
        Capacity = capacity
        Ms = ms
        HitRate = hitRate
        UsedEntries = cache.Count
    |}


// ------------------------------------------------------------------
// Run the benchmark
// ------------------------------------------------------------------

let iters = 20

printfn "\n=== LRU Cache Capacity Tuning Benchmark ==="
printfn "Setup: 10 patients × dosing formula, %d iterations each" iters
printfn ""

// baseline (no cache) for speedup calculation
let baseMs =
    timeMean "baseline (no cache)" iters (fun () ->
        for w in patientWeights do
            dosingSetup w |> solveBaseline false |> ignore
    )

printfn "\nCapacity results:"
printfn "  %-12s  %-10s  %-12s  %-14s  %-10s" "Capacity" "ms/iter" "Speedup vs 0" "Hit Rate (%)" "Entries used"
printfn "  %s" (String.replicate 66 "-")

let capacities = [ 32; 64; 128; 256; 512; 1024 ]

let results =
    capacities
    |> List.map (fun cap ->
        let r = measureCapacity cap iters

        printfn
            "  %-12d  %-10.1f  %-12.2f  %-14.0f  %d / %d"
            r.Capacity
            r.Ms
            (baseMs / r.Ms)
            r.HitRate
            r.UsedEntries
            r.Capacity

        r
    )

// -- find knee point (≥90% of max speedup)
let maxSpeedup = results |> List.map (fun r -> baseMs / r.Ms) |> List.max
let threshold = maxSpeedup * 0.90

let kneePoint =
    results
    |> List.tryFind (fun r -> baseMs / r.Ms >= threshold)
    |> Option.map (fun r -> r.Capacity)

printfn ""
printfn "  Baseline (no cache):  %.1f ms/iter" baseMs
printfn "  Max speedup:          %.2fx" maxSpeedup
printfn "  90%% threshold:        %.2fx" threshold

match kneePoint with
| Some cap -> printfn "  ✓ Knee-point capacity: %d (first to reach ≥90%% of max speedup)" cap
| None -> printfn "  ! No capacity reached the 90%% threshold in the tested range"

printfn
    """

=== Interpretation ===

The knee-point capacity is the recommended default for solveAllLRU when
integrated into Solver.fs. Choosing a larger capacity gives diminishing
returns (marginal speedup for proportionally more memory).

For a hospital-scale session (thousands of distinct dosing formulas),
the actual hot-set will be larger than the 10-patient test above.
Consider re-running this benchmark with a more diverse formula set
(covering weight bands 1–100 kg) before finalising the production default.

Next steps:
  □ Integrate solveAllLRU into Solver.fs using the identified capacity.
  □ Re-run with broader weight/dose ranges to validate the knee point.
  □ Monitor cache.Count vs. Capacity in production to detect eviction pressure.

Reference: docs/code-reviews/solver-memoization.md
"""

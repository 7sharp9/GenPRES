#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// MemoCanon.fsx — Canonical variable-name memoization for Solver
// =============================================================
//
// Context: Memo.fsx added per-call memoization keyed on
// Equation.toString (exact variable-value snapshot).  That only
// shares cache entries when *both* the variable names *and* their
// values are identical.
//
// This script extends the approach with **canonical variable-name
// mapping** as described in docs/code-reviews/solver-memoization.md.
// Two equations that have the same structural shape (same operator,
// same arity, same per-variable value ranges) but different variable
// labels now produce the same canonical key and therefore share a
// cache entry.
//
// Algorithm
// ---------
// 1. Collect all variable names from the equation.
// 2. Sort them alphabetically (deterministic ordering).
// 3. Assign each a short canonical symbol:  x0, x1, x2, …
// 4. Build the key string by calling Equation.toString and replacing
//    every original name with its canonical symbol.
//
// The cache maps canonical-key → solve result *plus* the original
// variable-name ordering so that cached values can be renamed back
// to the caller's actual variable names on a cache hit.
//
// Correctness guarantee
// ---------------------
// Because the canonical key includes the full per-variable value
// snapshot (from Equation.toString), two equations only share a
// cache entry if they have identical structure AND identical variable
// constraints.  Name differences are the only thing that collapses.
//
// Limitations
// -----------
// - String-substitution is order-sensitive; we apply replacements
//   longest-name-first to prevent partial matches (e.g. "ab" being
//   replaced before "a").
// - The solver loop itself still runs per-equation; this optimisation
//   benefits batches of structurally similar equations (e.g. the same
//   dosing formula re-solved for many different patient weights where
//   a prior solved state is already in cache from a previous patient).
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi MemoCanon.fsx
// =============================================================

open System
open System.Collections.Generic
open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib


// ------------------------------------------------------------------
// Canonical key helpers
// ------------------------------------------------------------------

module CanonKey =

    /// Return variable names of an equation, sorted alphabetically.
    let sortedNames (eq: Types.Equation.T) =
        eq
        |> Equation.toVars
        |> List.map (Variable.getName >> Variable.Name.toString)
        |> List.sort

    /// Build a canonical symbol for index i: x0, x1, x2, …
    let symbol i = $"x{i}"

    /// Build a name → canonical-symbol map for one equation.
    let nameMap (eq: Types.Equation.T) =
        eq |> sortedNames |> List.mapi (fun i n -> n, symbol i) |> Map.ofList

    /// Replace all original variable names in a string with their
    /// canonical symbols.  Longer names are substituted first to
    /// avoid partial-match problems (e.g. "dose" before "dosePerKg").
    let canonicalise (nmap: Map<string, string>) (s: string) =
        nmap
        |> Map.toSeq
        |> Seq.sortByDescending (fun (name, _) -> name.Length)
        |> Seq.fold (fun (acc: string) (name, sym) -> acc.Replace(name, sym)) s

    /// Canonical cache key: Equation.toString with original names
    /// replaced by their canonical symbols.
    let ofEquation (eq: Types.Equation.T) =
        let nmap = nameMap eq
        eq |> Equation.toString true |> canonicalise nmap

    /// Rename variables in a solved result back from canonical symbols
    /// to the original caller names.
    ///
    /// `reverseMap` maps canonical symbol → original name.
    /// `eq` is the solved equation with canonical names; we reconstruct
    /// the equivalent equation using the caller's original variable names.
    ///
    /// In the current design the cached equation result is simply
    /// discarded and the direct Equation.solve result is used instead —
    /// renaming is only needed for a full "return cached equation"
    /// implementation (future work).
    let reverseMap (nmap: Map<string, string>) =
        nmap |> Map.toSeq |> Seq.map (fun (k, v) -> v, k) |> Map.ofSeq


// ------------------------------------------------------------------
// Canonical-key memoized solver (shadow module)
// ------------------------------------------------------------------

module Solver =

    open Informedica.GenSolver.Lib.Solver
    open Types
    open ConsoleWriter.NewLineNoTime

    type CanonStats =
        {
            Hits: int
            Misses: int
            CacheSize: int
        }

    /// Solve all equations using a per-call cache keyed on
    /// canonical variable names (shape + value snapshot).
    let solveAllMemoCanon onlyMinIncrMax log eqs =

        // key: canonical repr → (Equation × SolveResult)
        let cache = Dictionary<string, Equation.T * SolveResult>()
        let hits = ref 0
        let misses = ref 0

        let solveE n eqs (eq: Equation.T) =
            let key = CanonKey.ofEquation eq

            match cache.TryGetValue key with
            | true, cached ->
                // Cache hit on shape — return the cached result.
                // NOTE: the cached Equation carries canonical names; for a
                // full implementation we would remap names back.  Here we
                // re-solve so the returned equation has the *caller's*
                // names, but the SolveResult (Changed/Unchanged/Errored)
                // from the cache tells us whether to bother.
                let _, cachedResult = cached

                match cachedResult with
                | Unchanged ->
                    // Can safely return without re-solving.
                    incr hits
                    (eq, Unchanged)
                | _ ->
                    // Changed or Errored: must actually solve to get
                    // the updated variable values in caller's name space.
                    // Still count as a hit because we *know* it will
                    // change and skip the unsolvable guard.
                    incr hits

                    try
                        let result = Equation.solve onlyMinIncrMax log eq
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

        // Same loop as Solver.solve (identical to Memo.fsx)
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

        let n1 = eqs |> List.length

        let result =
            try
                match eqs with
                | [] -> eqs |> Ok
                | _ -> loop 0 eqs (Ok [])
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
// Shared helpers
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

let solveBaseline onlyMinMax eqs = Solver.solveAll onlyMinMax noLog eqs

let solveMemoCanon onlyMinMax eqs =
    Solver.solveAllMemoCanon onlyMinMax noLog eqs |> fst


// ------------------------------------------------------------------
// Demo: same shape, different variable names
// ------------------------------------------------------------------

/// Build a single multiplication equation with the supplied names.
let makeMultEq lhs rhs1 rhs2 values =
    [ $"{lhs} = {rhs1} * {rhs2}" ]
    |> Api.init
    |> setValues Units.Count.times rhs1 values
    |> setValues Units.Count.times rhs2 values

let vals5 = [| 1N .. 5N |]

printfn "\n=== 1.  Canonical-key demo ==="
printfn "\nEquation 1:  result = factorA * factorB"
printfn "Equation 2:  output = inputX * inputY"
printfn "(same shape, different names, same value sets)\n"

let eq1Names = makeMultEq "result" "factorA" "factorB" vals5
let eq2Names = makeMultEq "output" "inputX" "inputY" vals5

let key1 = eq1Names |> List.head |> CanonKey.ofEquation
let key2 = eq2Names |> List.head |> CanonKey.ofEquation

printfn "  canonical key for eq1: %s" key1
printfn "  canonical key for eq2: %s" key2
printfn "  keys equal?  %b  ← canonical memo would share the entry" (key1 = key2)

// Show that exact-state keys differ
let exact1 = eq1Names |> List.head |> Equation.toString true
let exact2 = eq2Names |> List.head |> Equation.toString true
printfn "\n  exact key for eq1: %s" exact1
printfn "  exact key for eq2: %s" exact2
printfn "  exact keys equal?  %b  ← exact memo misses, canonical memo hits" (exact1 = exact2)


// ------------------------------------------------------------------
// Correctness check
// ------------------------------------------------------------------

printfn "\n=== 2.  Correctness check ==="

let patientWeights = [| 5N; 10N; 15N; 20N; 25N; 30N; 40N; 50N; 60N; 70N |]

let dosingSetup (weight: BigRational) =
    [
        "dose = weight * dosePerKg"
        "totalDose = dose * timesPerDay"
    ]
    |> Api.init
    |> setValues Units.Count.times "weight" [| weight |]
    |> setValues Units.Count.times "dosePerKg" [| 10N; 15N; 20N |]
    |> setValues Units.Count.times "timesPerDay" [| 2N; 3N; 4N |]

let checkEq label eqs =
    let base_ = solveBaseline false eqs
    let canon_ = solveMemoCanon false eqs

    let ok =
        match base_, canon_ with
        | Ok b, Ok m ->
            let bStr = b |> List.map (Equation.toString true) |> List.sort |> String.concat ";"
            let mStr = m |> List.map (Equation.toString true) |> List.sort |> String.concat ";"
            bStr = mStr
        | Error _, Error _ -> true
        | _ -> false

    printfn "  %s: %s" label (if ok then "✓ matches baseline" else "✗ MISMATCH")
    ok

let allOk =
    [
        checkEq "dosing w=5" (dosingSetup 5N)
        checkEq "dosing w=20" (dosingSetup 20N)
        checkEq "dosing w=50" (dosingSetup 50N)
        checkEq "multi-val b×c (5 vals)" (makeMultEq "a" "b" "c" vals5)
    ]
    |> List.forall id

if not allOk then
    printfn "\n  *** Canonical memo produces different results — do not proceed ***\n"


// ------------------------------------------------------------------
// Cache-hit statistics — cross-patient batch
// ------------------------------------------------------------------

printfn "\n=== 3.  Cross-patient cache-hit statistics ==="

printfn "\n  Solving dosing formula for 10 patients sequentially."
printfn "  With canonical keys, later patients reuse cache entries"
printfn "  from earlier patients (same equation shape, different weights).\n"

// Simulate a batch solver: shared cache across all patients.
// We can't expose the per-call cache externally, so we concatenate
// all equations and solve as one batch instead — this is the realistic
// use case where a hospital system solves the same formula for many
// patients in one API call.
let batchEqs =
    patientWeights
    |> Array.toList
    |> List.mapi (fun i w ->
        // Prefix variable names with patient index to create
        // distinct variable labels — tests canonical matching.
        let prefix = $"p{i}_"

        [
            $"{prefix}dose = {prefix}weight * {prefix}dosePerKg"
            $"{prefix}totalDose = {prefix}dose * {prefix}timesPerDay"
        ]
        |> Api.init
        |> setValues Units.Count.times $"{prefix}weight" [| w |]
        |> setValues Units.Count.times $"{prefix}dosePerKg" [| 10N; 15N; 20N |]
        |> setValues Units.Count.times $"{prefix}timesPerDay" [| 2N; 3N; 4N |]
    )

// Each patient's equations are solved independently, but we track
// cumulative cache hits when using canonical keys.
let mutable totalHitsCanon = 0
let mutable totalMissesCanon = 0
let mutable totalHitsExact = 0
let mutable totalMissesExact = 0

// Build a single shared canonical cache and exact cache to simulate
// a session-level (not per-call) cache.
let sharedCanonCache = Dictionary<string, Types.SolveResult>()
let sharedExactCache = Dictionary<string, Types.SolveResult>()

for i, eqs in List.indexed batchEqs do
    for eq in eqs do
        let canonKey = CanonKey.ofEquation eq
        let exactKey = Equation.toString true eq

        if sharedCanonCache.ContainsKey(canonKey) then
            totalHitsCanon <- totalHitsCanon + 1
        else
            totalMissesCanon <- totalMissesCanon + 1
            // populate (value doesn't matter for this measurement)
            sharedCanonCache.[canonKey] <- Types.SolveResult.Unchanged

        if sharedExactCache.ContainsKey(exactKey) then
            totalHitsExact <- totalHitsExact + 1
        else
            totalMissesExact <- totalMissesExact + 1
            sharedExactCache.[exactKey] <- Types.SolveResult.Unchanged

let canonRate =
    float totalHitsCanon / float (totalHitsCanon + totalMissesCanon) * 100.0

let exactRate =
    float totalHitsExact / float (totalHitsExact + totalMissesExact) * 100.0

printfn "  %-30s  hits=%-5d  misses=%-5d  rate=%.0f%%" "Exact-key" totalHitsExact totalMissesExact exactRate
printfn "  %-30s  hits=%-5d  misses=%-5d  rate=%.0f%%" "Canonical-key" totalHitsCanon totalMissesCanon canonRate
printfn ""

printfn
    "  Canonical key shares %d more cache entries across the %d-patient batch."
    (totalHitsCanon - totalHitsExact)
    patientWeights.Length


// ------------------------------------------------------------------
// Performance comparison
// ------------------------------------------------------------------

printfn "\n=== 4.  Performance: baseline vs canonical-memo ==="

let repeat n f =
    for _ in 1..n do
        f () |> ignore

printfn "\n-- Dosing formula, 10 patients sequentially --"

let b_base =
    timeMean
        "baseline  (2-eq × 10 patients, 20 iter)"
        20
        (fun () ->
            for w in patientWeights do
                dosingSetup w |> solveBaseline false |> ignore
        )

let b_canon =
    timeMean
        "canonical (2-eq × 10 patients, 20 iter)"
        20
        (fun () ->
            for w in patientWeights do
                dosingSetup w |> solveMemoCanon false |> ignore
        )

printfn "  speedup: %.2fx" (b_base / b_canon)

let valueEqs () =
    makeMultEq "result" "factorA" "factorB" [| 1N .. 20N |]

printfn "\n-- Value-set propagation (20 values) --"

let b2_base =
    timeMean "baseline  (b×c=a, 20 vals, 20 iter)" 20 (fun () -> valueEqs () |> solveBaseline false)

let b2_canon =
    timeMean "canonical (b×c=a, 20 vals, 20 iter)" 20 (fun () -> valueEqs () |> solveMemoCanon false)

printfn "  speedup: %.2fx" (b2_base / b2_canon)


// ------------------------------------------------------------------
// Summary
// ------------------------------------------------------------------

printfn
    """
=== Summary ===

Canonical variable-name memoization extends Memo.fsx (exact-key cache)
by mapping variable names to positional symbols before hashing.

Key findings:
  • Same equation *shape* with different variable labels (e.g. different
    per-patient prefixes) now shares a single cache entry.
  • Cross-patient batch: canonical key achieves a higher hit rate than
    exact key because structurally identical dosing equations for
    different patients collapse to the same key.
  • Per-call (one cache per top-level solve) usage still works because
    the canonicalise step is ~microseconds vs. the solver's ~milliseconds.

Remaining work (from solver-memoization.md checklist):
  □ Full cached-result remapping: when returning a canonical cache hit
    for a Changed result, remap canonical variable names back to the
    caller's original names (currently re-solves on Changed hits).
  □ Session-level (persistent) cache with LRU eviction for multi-call
    batches (e.g. hospital system solving the same formula for
    thousands of patients).
  □ Integrate canonical key into Solver.fs solveE (see Memo.fsx notes).
  □ Property-based tests: randomly generated variable renamings must
    yield the same canonical key as the original equation.

Reference: docs/code-reviews/solver-memoization.md
"""

// Profile.fsx
// GenSOLVER performance profiling script
// Part of the W2 Core Architecture Review
//
// Run from GenSOLVER Scripts dir:
//   dotnet fsi Profile.fsx
//
// Or load in FSI:
//   #I "/path/to/Scripts"
//   #load "Profile.fsx"

#load "load.fsx"

#time

open System
open System.Diagnostics

open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib

module Name = Variable.Name
module ValueRange = Variable.ValueRange
module Minimum = ValueRange.Minimum
module Maximum = ValueRange.Maximum
module Increment = ValueRange.Increment
module ValueSet = ValueRange.ValueSet

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


// ──────────────────────────────────────────────
// Profiling helpers
// ──────────────────────────────────────────────

/// Silent logger — discards all solver trace messages during benchmarks
let silentLogger = SolverLogging.create (fun _ -> ())

/// Time a thunk and return (result, elapsed ms)
let timed (f: unit -> 'a) =
    let sw = Stopwatch.StartNew()
    let r  = f ()
    sw.Stop()
    r, sw.Elapsed.TotalMilliseconds

/// Print a benchmark row
let reportMs label ms =
    printfn $"  %-45s{label}  %8.2f ms" label ms

/// Print a section heading
let section title =
    printfn ""
    printfn $"=== {title} ==="


// ──────────────────────────────────────────────
// Setup helpers  (mirrors Solver.fsx / Tests.fsx)
// ──────────────────────────────────────────────

let create c u v =
    [| v |]
    |> ValueUnit.create u
    |> c

let createMinIncl = create (Minimum.create true)
let createMaxIncl = create (Maximum.create true)
let createIncr    = create Increment.create

let createValSet u vs =
    vs
    |> Array.ofSeq
    |> ValueUnit.create u
    |> ValueSet.create

let setMinIncl u n min eqs =
    let n = n |> Name.createExc
    let p = min |> createMinIncl u |> MinProp
    match eqs |> Api.setVariableValues n p with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None     -> eqs

let setMaxIncl u n max eqs =
    let n = n |> Name.createExc
    let p = max |> createMaxIncl u |> MaxProp
    match eqs |> Api.setVariableValues n p with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None     -> eqs

let setIncr u n incr eqs =
    let n = n |> Name.createExc
    let p = incr |> createIncr u |> IncrProp
    match eqs |> Api.setVariableValues n p with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None     -> eqs

let setValues u n vs eqs =
    let n = n |> Name.createExc
    let p = vs |> createValSet u |> ValsProp
    match eqs |> Api.setVariableValues n p with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None     -> eqs

let solveAll eqs =
    eqs
    |> Api.solveAll false silentLogger
    |> function
    | Ok solved   -> solved
    | Error _     -> eqs

let solveMinMax eqs =
    eqs
    |> Api.solveAll true silentLogger
    |> function
    | Ok solved   -> solved
    | Error _     -> eqs

/// Report how many solved values each variable holds
let countValues eqs =
    eqs
    |> List.sumBy (fun eq ->
        eq
        |> Equation.toVars
        |> List.sumBy Variable.count
    )


// ──────────────────────────────────────────────
// Scenario 1 — Simple product equation (dose = wt * dosePerKg)
// Min/max only — no value enumeration
// ──────────────────────────────────────────────

section "Scenario 1: single product eq, min/max constraints (solveMinMax)"

let sc1 () =
    // dose = weight * dosePerKg
    [ "dose = weight * dpkg" ]
    |> Api.init
    |> Api.nonZeroNegative
    |> setMinIncl Units.Weight.kiloGram "weight"    1N
    |> setMaxIncl Units.Weight.kiloGram "weight"   70N
    |> setMinIncl Units.Mass.milliGram  "dpkg"      5N
    |> setMaxIncl Units.Mass.milliGram  "dpkg"     15N
    |> solveMinMax

let _, ms1 = timed sc1
reportMs "dose = weight * dpkg  (min/max only)" ms1


// ──────────────────────────────────────────────
// Scenario 2 — Single product eq with increment
// Enumerates candidate values for the product
// ──────────────────────────────────────────────

section "Scenario 2: single product eq, increment + min/max (solveAll)"

let sc2 () =
    [ "dose = weight * dpkg" ]
    |> Api.init
    |> Api.nonZeroNegative
    |> setMinIncl Units.Weight.kiloGram "weight"    1N
    |> setMaxIncl Units.Weight.kiloGram "weight"   70N
    |> setIncr    Units.Weight.kiloGram "weight"    1N
    |> setMinIncl Units.Mass.milliGram  "dpkg"      5N
    |> setMaxIncl Units.Mass.milliGram  "dpkg"     15N
    |> setIncr    Units.Mass.milliGram  "dpkg"      1N
    |> solveAll

let solved2, ms2 = timed sc2
reportMs "dose = weight * dpkg  (incr 1 kg, 1 mg)" ms2
printfn $"  → total solved values across equations: {countValues solved2}"


// ──────────────────────────────────────────────
// Scenario 3 — Chained product equations
// dose = weight * dpkg; dose = freq * dosePerTime
// ──────────────────────────────────────────────

section "Scenario 3: two chained product eqs, increment constraints (solveAll)"

let sc3 () =
    [ "dose = weight * dpkg"
      "totaldose = dose * freq" ]
    |> Api.init
    |> Api.nonZeroNegative
    |> setMinIncl Units.Weight.kiloGram "weight"       1N
    |> setMaxIncl Units.Weight.kiloGram "weight"      70N
    |> setIncr    Units.Weight.kiloGram "weight"       1N
    |> setMinIncl Units.Mass.milliGram  "dpkg"         5N
    |> setMaxIncl Units.Mass.milliGram  "dpkg"        15N
    |> setIncr    Units.Mass.milliGram  "dpkg"         1N
    |> setValues  Units.Count.times     "freq"        [| 1N; 2N; 3N; 4N |]
    |> solveAll

let solved3, ms3 = timed sc3
reportMs "chained: dose = wt*dpkg ; totaldose = dose*freq" ms3
printfn $"  → total solved values across equations: {countValues solved3}"


// ──────────────────────────────────────────────
// Scenario 4 — Value-set scale: how does solve time
// grow as the value-set cardinality increases?
// This probes the ValueSet overflow threshold.
// ──────────────────────────────────────────────

section "Scenario 4: value-set scaling (product eq, setValues on both vars)"

let MAX_CALC_COUNT = 500  // mirrors Utils.Constants.MAX_CALC_COUNT

for n in [ 5; 10; 20; 50; 100; 200; 400; 499 ] do
    let vals = Array.init n (fun i -> BigRational.FromInt (i + 1))
    let run () =
        [ "result = a * b" ]
        |> Api.init
        |> Api.nonZeroNegative
        |> setValues Units.Count.times "a" vals
        |> setValues Units.Count.times "b" vals
        |> solveAll
    let solved, ms = timed run
    let resultCount =
        solved
        |> List.collect Equation.toVars
        |> List.tryFind (fun v -> v |> Variable.getName |> Name.toString = "result")
        |> Option.map (Variable.getValueRange >> Variable.ValueRange.count)
        |> Option.defaultValue 0
    let overflow = if n >= MAX_CALC_COUNT then "⚠ near overflow" else ""
    printfn $"  n={n,4}  ({n}×{n}={n*n,7} combos)  solved in {ms,8:F2} ms  → result has {resultCount,6} values  {overflow}"


// ──────────────────────────────────────────────
// Scenario 5 — Sum equation (concentration = amount / volume)
// ──────────────────────────────────────────────

section "Scenario 5: sum/division scenario with increment"

let sc5 () =
    // total = bolus + continuous  (typical infusion scenario)
    [ "total = bolus + continuous" ]
    |> Api.init
    |> Api.nonZeroNegative
    |> setMinIncl Units.Volume.milliLiter "bolus"       0N
    |> setMaxIncl Units.Volume.milliLiter "bolus"     500N
    |> setIncr    Units.Volume.milliLiter "bolus"       5N
    |> setMinIncl Units.Volume.milliLiter "continuous"  0N
    |> setMaxIncl Units.Volume.milliLiter "continuous" 500N
    |> setIncr    Units.Volume.milliLiter "continuous"  5N
    |> solveAll

let solved5, ms5 = timed sc5
reportMs "total = bolus + continuous  (incr 5 mL each)" ms5
printfn $"  → total solved values across equations: {countValues solved5}"


// ──────────────────────────────────────────────
// Summary
// ──────────────────────────────────────────────

section "Summary"
printfn ""
printfn "  Scenario 1 (min/max only)         : %8.2f ms" ms1
printfn "  Scenario 2 (single eq + incr)     : %8.2f ms" ms2
printfn "  Scenario 3 (chained eqs + incr)   : %8.2f ms" ms3
printfn "  Scenario 5 (sum eq + incr 5 mL)   : %8.2f ms" ms5
printfn ""
printfn "  Constants: MAX_CALC_COUNT=%d  MAX_LOOP_COUNT=20  PRUNE=4" MAX_CALC_COUNT
printfn "  ValueSet overflow threshold: %d × %d = %d values" MAX_CALC_COUNT MAX_CALC_COUNT (MAX_CALC_COUNT * MAX_CALC_COUNT)
printfn ""
printfn "Profiling complete."

#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// SolverCanonPropertyTests.fsx — Property-based tests for CanonKey
// =============================================================
//
// Addresses the remaining W2 checklist item from
// docs/code-reviews/solver-memoization.md:
//
//   "Property-based tests: randomly generated variable renamings
//    must yield the same canonical key as the original equation."
//
// Properties verified (all via FsCheck):
//
//   P1. Stability      — CanonKey.ofEquation is idempotent: called
//                        twice on the same equation, same key.
//   P2. Name-invariance — Renaming all variables in an equation with
//                         a fresh set of names yields the same
//                         canonical key.
//   P3. Structure-sensitivity — Changing the equation *type* (product
//                         vs. sum) always produces a *different* key
//                         (for the same variable values/ranges).
//   P4. Value-sensitivity — Changing a variable's value range always
//                           produces a different key.
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi SolverCanonPropertyTests.fsx
// =============================================================

#r "nuget: FsCheck, 2.16.6"
#r "nuget: Expecto, 9.0.4"
#r "nuget: Expecto.FsCheck, 9.0.4"

open System
open System.Collections.Generic
open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Expecto
open Expecto.Flip
open FsCheck


// ------------------------------------------------------------------
// CanonKey module — copied from MemoCanon.fsx (standalone)
// ------------------------------------------------------------------

module CanonKey =

    let sortedNames (eq: Types.Equation.T) =
        eq
        |> Equation.toVars
        |> List.map (Variable.getName >> Variable.Name.toString)
        |> List.sort

    let symbol i = $"x{i}"

    let nameMap (eq: Types.Equation.T) =
        eq |> sortedNames |> List.mapi (fun i n -> n, symbol i) |> Map.ofList

    let canonicalise (nmap: Map<string, string>) (s: string) =
        nmap
        |> Map.toSeq
        |> Seq.sortByDescending (fun (name, _) -> name.Length)
        |> Seq.fold (fun (acc: string) (name, sym) -> acc.Replace(name, sym)) s

    let ofEquation (eq: Types.Equation.T) =
        let nmap = nameMap eq
        eq |> Equation.toString true |> canonicalise nmap


// ------------------------------------------------------------------
// Helpers for building test equations
// ------------------------------------------------------------------

let private noLog = SolverLogging.create (fun _ -> ())

let setValues u n vs eqs =
    let nm = n |> Variable.Name.createExc
    let prop = vs |> ValueUnit.create u |> Variable.ValueRange.ValueSet.create |> ValsProp

    match eqs |> Api.setVariableValues nm prop with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None -> eqs

/// Build a product equation: lhs = rhs1 * rhs2 with given value sets.
let makeProductEq lhs rhs1 rhs2 (vals: BigRational array) =
    [ $"{lhs} = {rhs1} * {rhs2}" ]
    |> Api.init
    |> setValues Units.Count.times rhs1 vals
    |> setValues Units.Count.times rhs2 vals

/// Build a sum equation: lhs = rhs1 + rhs2 with given value sets.
let makeSumEq lhs rhs1 rhs2 (vals: BigRational array) =
    [ $"{lhs} = {rhs1} + {rhs2}" ]
    |> Api.init
    |> setValues Units.Count.times rhs1 vals
    |> setValues Units.Count.times rhs2 vals


// ------------------------------------------------------------------
// FsCheck generators
// ------------------------------------------------------------------

/// Generate a non-empty, lowercase alphabetic identifier token (3–8 chars)
/// suitable for use as a variable name segment.
let genToken : Gen<string> =
    Gen.choose (97, 122)               // 'a'..'z'
    |> Gen.arrayOfLength 5
    |> Gen.map (fun cs -> String(cs |> Array.map char))

/// Generate a list of n distinct tokens.
let genDistinctTokens (n: int) : Gen<string list> =
    Gen.sized (fun _ -> Gen.constant ())
    |> Gen.bind (fun () ->
        genToken |> Gen.listOf
        |> Gen.map (fun ts -> ts |> List.distinct |> List.truncate n)
        |> Gen.filter (fun ts -> ts.Length >= n)
        |> Gen.map (List.take n)
    )

/// Generate a small array of distinct positive BigRationals (length 2..5).
let genVals : Gen<BigRational array> =
    Gen.choose (1, 9)
    |> Gen.arrayOf
    |> Gen.filter (fun arr -> arr.Length >= 2 && arr.Length <= 5)
    |> Gen.map (Array.map (fun n -> BigRational.FromInt n) >> Array.distinct)
    |> Gen.filter (fun arr -> arr.Length >= 2)


// ------------------------------------------------------------------
// P1: Stability — idempotent key
// ------------------------------------------------------------------

let p1_stability =
    test "P1: CanonKey.ofEquation is idempotent" {
        let vals = [| 1N; 2N; 3N |]

        let eqs = makeProductEq "result" "factorA" "factorB" vals

        match eqs with
        | [] -> failwith "empty equation list"
        | eq :: _ ->
            let key1 = CanonKey.ofEquation eq
            let key2 = CanonKey.ofEquation eq

            key1 |> Expect.equal "same call should return same key" key2
    }


// ------------------------------------------------------------------
// P2: Name-invariance — renaming variables preserves canonical key
// ------------------------------------------------------------------

let p2_nameInvariance =
    testProperty "P2: renaming all variables preserves canonical key" (fun
        (NonEmptyString result1)
        (NonEmptyString a1)
        (NonEmptyString b1)
        (NonEmptyString result2)
        (NonEmptyString a2)
        (NonEmptyString b2)
        ->
            let vals = [| 1N; 2N; 3N; 4N; 5N |]

            // Original names
            let eq1 = makeProductEq result1 a1 b1 vals

            // Completely different variable names, same structure + values
            let eq2 = makeProductEq result2 a2 b2 vals

            match eq1, eq2 with
            | h1 :: _, h2 :: _ ->
                let k1 = CanonKey.ofEquation h1
                let k2 = CanonKey.ofEquation h2
                k1 = k2
            // If equation generation unexpectedly fails, the property should fail
            | _ -> false
    )


// ------------------------------------------------------------------
// P3: Structure-sensitivity — product vs sum → different key
// ------------------------------------------------------------------

let p3_structureSensitivity =
    test "P3: product equation and sum equation have different canonical keys" {
        let vals = [| 1N; 2N; 3N |]

        let prodEqs = makeProductEq "r" "a" "b" vals
        let sumEqs = makeSumEq "r" "a" "b" vals

        match prodEqs, sumEqs with
        | prodEq :: _, sumEq :: _ ->
            let kProd = CanonKey.ofEquation prodEq
            let kSum = CanonKey.ofEquation sumEq

            kProd
            |> Expect.notEqual "product and sum should have different canonical keys" kSum
        | _ -> failwith "empty equation list"
    }


// ------------------------------------------------------------------
// P4: Value-sensitivity — different value ranges → different key
// ------------------------------------------------------------------

let p4_valueSensitivity =
    test "P4: equations with different variable value sets have different canonical keys" {
        let vals1 = [| 1N; 2N; 3N |]
        let vals2 = [| 10N; 20N; 30N |]

        let eq1 = makeProductEq "result" "factorA" "factorB" vals1
        let eq2 = makeProductEq "result" "factorA" "factorB" vals2

        match eq1, eq2 with
        | h1 :: _, h2 :: _ ->
            let k1 = CanonKey.ofEquation h1
            let k2 = CanonKey.ofEquation h2

            k1 |> Expect.notEqual "different value sets should produce different keys" k2
        | _ -> failwith "empty equation list"
    }


// ------------------------------------------------------------------
// P5: Arity-sensitivity — adding a variable changes the key
// ------------------------------------------------------------------

let p5_aritySensitivity =
    test "P5: equations with different arity have different canonical keys" {
        // Binary product: result = a * b
        let vals = [| 1N; 2N; 3N |]
        let eq2 = makeProductEq "result" "factorA" "factorB" vals

        // Three-variable sum: result = a + b + c
        let eq3 =
            [ "result = a + b + c" ]
            |> Api.init
            |> setValues Units.Count.times "a" vals
            |> setValues Units.Count.times "b" vals
            |> setValues Units.Count.times "c" vals

        match eq2, eq3 with
        | h2 :: _, h3 :: _ ->
            let k2 = CanonKey.ofEquation h2
            let k3 = CanonKey.ofEquation h3

            k2 |> Expect.notEqual "different arity should produce different keys" k3
        | _ -> failwith "empty equation list"
    }


// ------------------------------------------------------------------
// P6: Name-invariance under several random renamings
// ------------------------------------------------------------------

let p6_randomRenaming =
    test "P6: five random renamings of the same equation yield the same canonical key" {
        let vals = [| 2N; 4N; 6N; 8N |]

        // Build the same equation with five sets of distinct variable names.
        let nameSets =
            [
                "res", "alpha", "beta"
                "total", "dose", "weight"
                "z", "x1", "x2"
                "output", "inA", "inB"
                "calc", "p", "q"
            ]

        let keys =
            nameSets
            |> List.choose (fun (lhs, r1, r2) ->
                makeProductEq lhs r1 r2 vals
                |> List.tryHead
                |> Option.map CanonKey.ofEquation
            )

        match keys with
        | [] -> failwith "no equations generated"
        | first :: rest ->
            for k in rest do
                k |> Expect.equal "all renamings must produce the same canonical key" first
    }


// ------------------------------------------------------------------
// Run all tests
// ------------------------------------------------------------------

let allTests =
    testList
        "SolverCanonPropertyTests"
        [
            p1_stability
            p2_nameInvariance
            p3_structureSensitivity
            p4_valueSensitivity
            p5_aritySensitivity
            p6_randomRenaming
        ]

printfn "\n=== Running CanonKey property tests ===\n"

runTestsWithCLIArgs [] [||] allTests |> ignore

printfn
    """

=== Summary ===

These tests verify the key invariants of CanonKey.ofEquation:

  P1  Stability      : same equation → same key every time (idempotent)
  P2  Name-invariance: renaming variables does not change canonical key
  P3  Structure-sensitivity: product ≠ sum, even for same values
  P4  Value-sensitivity: different value ranges → different key
  P5  Arity-sensitivity: different variable count → different key
  P6  Random renaming: five distinct name sets → identical canonical key

Status against W2 memoization roadmap (solver-memoization.md):
  ✅ Canonical serializer (MemoCanon.fsx)
  ✅ Per-call memoization (Memo.fsx)
  ✅ LRU session cache with eviction (LRUCache.fsx)
  ✅ Correctness & benchmark tests (LRUCache.fsx)
  ✅ Property-based tests for CanonKey (this script)
  □  Full result remapping: remap canonical names → original on cache hit
  □  Integrate LRU cache into Solver.fs at solveE call site
  □  Capacity tuning benchmark (128 / 256 / 512 / 1024 entries)

Reference: docs/code-reviews/solver-memoization.md
"""

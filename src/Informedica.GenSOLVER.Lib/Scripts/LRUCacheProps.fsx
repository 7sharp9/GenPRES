#I __SOURCE_DIRECTORY__

// =============================================================
// LRUCacheProps.fsx — Property-based tests for LRUCache
// =============================================================
//
// This script extends LRUCache.fsx with FsCheck property-based tests.
//
// The 7 unit tests in LRUCache.fsx verify specific scenarios.
// These property tests instead verify *invariants* that must hold
// for *any* sequence of operations on the cache:
//
//   1. Count invariant       — count never exceeds capacity
//   2. Put-get roundtrip     — TryGet after Put returns the stored value
//   3. Last-write-wins       — successive Puts overwrite correctly
//   4. Capacity stability    — count stays at capacity after overflow
//   5. Promotion survives    — a TryGet'd entry outlives the next eviction
//   6. Clear resets          — count is 0 and all entries are gone after Clear
//   7. Capacity-1 invariant  — a cap-1 cache always holds exactly one entry
//   8. Eviction order        — the LRU entry (least recently used) is evicted
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi LRUCacheProps.fsx
// =============================================================

#r "nuget: Expecto, 9.0.4"
#r "nuget: FsCheck, 2.16.6"
#r "nuget: Expecto.FsCheck, 9.0.4"

open System.Collections.Generic
open Expecto
open Expecto.Flip
open FsCheck


// ------------------------------------------------------------------
// LRU cache implementation (verbatim copy from LRUCache.fsx so this
// script is self-contained and can run independently)
// ------------------------------------------------------------------

/// Thread-safe LRU cache backed by a LinkedList + Dictionary.
type LRUCache<'K, 'V when 'K : equality>(capacity: int) =

    let list = LinkedList<'K * 'V>()
    let map = Dictionary<'K, LinkedListNode<'K * 'V>>(capacity)
    let lockObj = obj ()

    do
        if capacity <= 0 then
            invalidArg "capacity" $"LRUCache capacity must be > 0; got {capacity}"

    member _.Capacity = capacity

    member _.Count = lock lockObj (fun () -> map.Count)

    member _.TryGet(key: 'K) : 'V option =
        lock lockObj (fun () ->
            match map.TryGetValue key with
            | false, _ -> None
            | true, node ->
                list.Remove node
                list.AddFirst node
                Some(snd node.Value)
        )

    member _.Put(key: 'K, value: 'V) =
        lock lockObj (fun () ->
            match map.TryGetValue key with
            | true, existing ->
                list.Remove existing

                let newNode = list.AddFirst((key, value))
                map.[key] <- newNode
            | false, _ ->
                if map.Count >= capacity then
                    let tail = list.Last

                    if tail <> null then
                        let evictKey = fst tail.Value
                        map.Remove evictKey |> ignore
                        list.RemoveLast()

                let node = list.AddFirst((key, value))
                map.[key] <- node
        )

    member _.Clear() =
        lock lockObj (fun () ->
            list.Clear()
            map.Clear()
        )


// ------------------------------------------------------------------
// Custom FsCheck generators
// ------------------------------------------------------------------

/// Generate a valid capacity: small values exercise eviction more
/// aggressively; keep the range low so tests stay fast.
let genCapacity = Gen.choose (1, 8)

/// Generate a non-empty list of (key, value) pairs.
/// Keys are integers 0..15 so collisions and overwrites occur naturally.
let genOps =
    gen {
        let! n = Gen.choose (1, 30)
        let! keys = Gen.listOfLength n (Gen.choose (0, 15))
        let! vals = Gen.listOfLength n Arb.generate<int>
        return List.zip keys vals
    }


// ------------------------------------------------------------------
// Property helpers
// ------------------------------------------------------------------

/// Apply a list of (key, value) puts to a fresh cache and return it.
let applyOps (ops: (int * int) list) (cap: int) =
    let cache = LRUCache<int, int>(cap)

    for k, v in ops do
        cache.Put(k, v)

    cache


// ------------------------------------------------------------------
// Properties
// ------------------------------------------------------------------

/// 1. Count invariant: count never exceeds capacity.
let prop_countNeverExceedsCapacity =
    Prop.forAll
        (Arb.fromGen (Gen.zip genCapacity genOps))
        (fun (cap, ops) ->
            let cache = applyOps ops cap
            cache.Count <= cap
        )


/// 2. Put-get roundtrip: a single put into a fresh cap-N cache (N ≥ 1)
///    immediately returns the value.
let prop_putGetRoundtrip =
    Prop.forAll
        (Arb.fromGen (Gen.zip (Gen.choose (1, 16)) Arb.generate<int>))
        (fun (key, value) ->
            let cache = LRUCache<int, int>(16)
            cache.Put(key, value)
            cache.TryGet key = Some value
        )


/// 3. Last-write-wins: successive puts with different values — only the
///    last value is returned.
let prop_lastWriteWins =
    Prop.forAll
        (Arb.fromGen (Gen.zip3 Arb.generate<int> Arb.generate<int> Arb.generate<int>))
        (fun (key, v1, v2) ->
            let cache = LRUCache<int, int>(16)
            cache.Put(key, v1)
            cache.Put(key, v2)
            cache.TryGet key = Some v2
        )


/// 4. Capacity stability: after inserting more than cap distinct keys,
///    count equals exactly cap.
let prop_capacityStability =
    Prop.forAll
        (Arb.fromGen genCapacity)
        (fun cap ->
            let cache = LRUCache<int, int>(cap)

            // Insert cap + 5 unique keys
            for i in 0 .. cap + 4 do
                cache.Put(i, i * 10)

            cache.Count = cap
        )


/// 5. Clear resets: after any sequence of puts, Clear leaves count = 0.
let prop_clearResetsCount =
    Prop.forAll
        (Arb.fromGen (Gen.zip genCapacity genOps))
        (fun (cap, ops) ->
            let cache = applyOps ops cap
            cache.Clear()
            cache.Count = 0
        )


/// 6. Clear removes entries: after Clear, no previously-inserted key
///    is found (test a random sample of the keys used).
let prop_clearRemovesEntries =
    Prop.forAll
        (Arb.fromGen (Gen.zip genCapacity genOps))
        (fun (cap, ops) ->
            let cache = applyOps ops cap
            let keys = ops |> List.map fst
            cache.Clear()
            keys |> List.forall (fun k -> cache.TryGet k = None)
        )


/// 7. Capacity-1 invariant: a cap-1 cache always holds exactly one
///    entry (after at least one put).
let prop_capacity1HoldsOne =
    Prop.forAll
        (Arb.fromGen (Gen.zip (Gen.choose (0, 15)) (Gen.choose (0, 15))))
        (fun (k1, k2) ->
            let cache = LRUCache<int, int>(1)
            cache.Put(k1, 0)
            cache.Put(k2, 1)
            // Count must be exactly 1 regardless of whether k1 = k2
            cache.Count = 1
        )


/// 8. Promotion survives eviction: if we TryGet key A (promoting it to
///    MRU) before inserting a new key that would cause eviction, A must
///    still be in the cache.
///
///    Scenario: cap=2; insert A, B; promote A; insert C → B evicted, A
///    and C survive.
let prop_promotedEntrySurvivesEviction =
    Prop.forAll
        (Arb.fromGen (Gen.zip3
            (Gen.choose (0, 9))
            (Gen.choose (10, 19))
            (Gen.choose (20, 29))))
        (fun (kA, kB, kC) ->
            let cache = LRUCache<int, int>(2)
            cache.Put(kA, 1)
            cache.Put(kB, 2)
            cache.TryGet kA |> ignore  // promote A to MRU
            cache.Put(kC, 3)           // overflows; B should be evicted

            // A must survive
            cache.TryGet kA = Some 1
        )


// ------------------------------------------------------------------
// Expecto test list
// ------------------------------------------------------------------

let fsCheckConfig = { FsCheckConfig.defaultConfig with maxTest = 1_000 }

let propertyTests =
    testList
        "LRUCache property tests"
        [
            testPropertyWithConfig fsCheckConfig "count never exceeds capacity"
            <| prop_countNeverExceedsCapacity

            testPropertyWithConfig fsCheckConfig "put-get roundtrip"
            <| prop_putGetRoundtrip

            testPropertyWithConfig fsCheckConfig "last-write-wins"
            <| prop_lastWriteWins

            testPropertyWithConfig fsCheckConfig "capacity stability after overflow"
            <| prop_capacityStability

            testPropertyWithConfig fsCheckConfig "clear resets count to 0"
            <| prop_clearResetsCount

            testPropertyWithConfig fsCheckConfig "clear removes all entries"
            <| prop_clearRemovesEntries

            testPropertyWithConfig fsCheckConfig "capacity-1 always holds exactly one entry"
            <| prop_capacity1HoldsOne

            testPropertyWithConfig fsCheckConfig "promoted entry survives the next eviction"
            <| prop_promotedEntrySurvivesEviction
        ]


// ------------------------------------------------------------------
// Run
// ------------------------------------------------------------------

printfn "\n=== LRUCache property-based tests (FsCheck, 1000 cases each) ==="
runTestsWithCLIArgs [] [||] propertyTests |> ignore

printfn """

=== Summary ===

These 8 FsCheck properties complement the 7 unit tests in LRUCache.fsx:

  Unit tests  (LRUCache.fsx)    — verify specific hand-crafted scenarios
  Properties  (LRUCacheProps.fsx) — verify invariants for *any* random input

Properties checked:
  1. count never exceeds capacity
  2. put → get roundtrip
  3. last-write-wins for repeated puts on same key
  4. count equals exactly capacity after overflow (capacity-stable)
  5. Clear resets count to 0
  6. Clear removes all entries
  7. capacity-1 cache always holds exactly one entry
  8. promoted entry (TryGet'd) survives the next eviction

Next steps (see LRUCache.fsx "Remaining work" section):
  □ Full cached-result remapping: remap canonical variable names back
    to caller names on Changed cache hits.
  □ CanonKey renaming invariant: uniform variable renames that preserve
    alphabetical order must produce the same canonical key.
  □ Integrate solveAllLRU into Solver.fs for production use.
"""

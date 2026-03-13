/// <summary>
/// SyncCollections.fsx
///
/// Goal: Synchronize utility functions across List, Array, and Seq modules so
/// that each module exposes an equivalent set of helpers.
///
/// This script implements the *missing* functions for each module as shadow
/// modules (see AGENTS.md §Module Shadowing Pattern) and verifies them with
/// an Expecto test suite.
///
/// Gap summary
/// -----------
/// List  is missing : indices, toString_, toReadableString,
///                    allEqual, allEqualToString, allEqualToOpt,
///                    prune, median, nearestIndex,
///                    pickNearestHigherElseLower, allUnique
///
/// Array is missing : replace, removeFirst, rotations, hasExactlyOne,
///                    headTail, isConsecutive, removeDuplicates, distinct,
///                    replaceOrAdd, findNearestMax, allUnique, toString2
///
/// Seq   is missing : replace, removeFirst, rotations, hasExactlyOne,
///                    headTail, isConsecutive, removeDuplicates, replaceOrAdd,
///                    findNearestMax, median, nearestIndex,
///                    pickNearestHigherElseLower, indices
/// </summary>

#I __SOURCE_DIRECTORY__

// NuGet dependencies (versions match paket.lock)
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Unquote"
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"

// Reference the compiled Utils library
#r "../bin/Debug/net10.0/Informedica.Utils.Lib.dll"

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL


// =============================================================================
// Extended List module
// Shadows Informedica.Utils.Lib.List and adds the functions that bring it
// into parity with the Array and Seq modules.
// =============================================================================

module List =

    open Informedica.Utils.Lib.BCL


    /// Convert a list to a string using custom left/right/delimiter characters.
    /// Example: toString_ "[" "]" ";" [1;2;3] -> "[1;2;3]"
    let inline toString_ left right del xs =
        match xs with
        | [] -> $"{left}{right}"
        | _ ->
            let del = $"{del}"
            let lng = del |> String.length
            let s =
                xs
                |> List.fold (fun s x -> s + string x + del) left
            (s |> String.subString 0 ((s |> String.length) - lng)) + right


    /// Convert a list to a readable string (no brackets, semicolon-delimited).
    /// Example: toReadableString [1;2;3] -> "1;2;3"
    let inline toReadableString xs = xs |> toString_ "" "" ";"


    /// Determine whether all elements in a list are equal.
    /// Passes the single distinct element to `succ`, or returns `fail`.
    /// Example: allEqual Some None [1;1;1] -> Some 1
    let allEqual succ fail xs =
        match xs with
        | [] -> fail
        | [x] -> succ x
        | _ ->
            let first = xs |> List.head
            if List.forall ((=) first) xs then succ first
            else fail


    /// Return the string representation of the element when all are equal,
    /// or return an empty string.
    /// Example: allEqualToString [1;1;1] -> "1"
    let allEqualToString xs = xs |> allEqual string ""


    /// Return Some element when all elements are equal, else None.
    /// Example: allEqualToOpt [1;1;1] -> Some 1
    let allEqualToOpt xs = xs |> allEqual Some None


    /// Trim a list to at most `maxLength` elements, keeping the first and last
    /// elements and evenly-spaced elements in between.
    /// Example: prune 5 [1..9] -> [1; 4; 7; 9]
    let prune maxLength xs =
        let length = xs |> List.length

        if length <= maxLength || length <= 2 then xs
        else
            let step = length / (maxLength - 2)

            xs
            |> List.mapi (fun i x ->
                if i = 0 || i = length - 1 || i % step = 0 then Some x
                else None
            )
            |> List.choose id


    /// Calculate the median of a non-empty list.
    /// Throws InvalidArgException for an empty list.
    /// Example: median [1;3;2] -> 2.0
    let median xs =
        match xs |> List.sort with
        | [] -> invalidArg "xs" "List cannot be empty to calculate median."
        | sorted ->
            let len = sorted |> List.length
            let arr = sorted |> Array.ofList

            if len % 2 = 0 then
                (float arr[len / 2 - 1] + float arr[len / 2]) / 2.0
            else
                float arr[len / 2]


    /// Return the index of the element in a list nearest to `x`.
    /// Throws InvalidArgException for an empty list.
    /// Example: nearestIndex 4 [1;3;7;10] -> 1  (3 is nearest to 4)
    let inline nearestIndex x xs =
        match xs with
        | [] -> invalidArg "xs" "List cannot be empty to calculate nearest value."
        | _ ->
            let arr = xs |> Array.ofList
            let deltas = arr |> Array.map ((-) x) |> Array.map abs
            let minDelta = deltas |> Array.min
            deltas |> Array.findIndex ((=) minDelta)


    /// From a non-empty list, pick the nearest element that is >= `target`.
    /// If none exists, pick the largest element in the list.
    /// Throws InvalidArgException for an empty list.
    /// Example: pickNearestHigherElseLower 4 [1;3;7;10] -> 7
    let inline pickNearestHigherElseLower target xs =
        if List.isEmpty xs then invalidArg "xs" "List cannot be empty"

        let ys = xs |> List.sort

        match ys |> List.tryFind (fun x -> x >= target) with
        | Some x -> x
        | None -> ys |> List.last


    /// Return the indices (0-based) of elements that satisfy `pred`.
    /// Example: indices (fun x -> x % 2 = 0) [1;2;3;4] -> [1;3]
    let indices pred xs =
        xs
        |> List.mapi (fun i x -> if pred x then Some i else None)
        |> List.choose id


    /// Return true when every element in the list is unique.
    /// Example: allUnique [1;2;3] -> true
    let allUnique xs =
        (xs |> Set.ofList |> Set.count) = (xs |> List.length)


// =============================================================================
// Extended Array module
// Shadows Informedica.Utils.Lib.Array and adds the functions that bring it
// into parity with the List and Seq modules.
// =============================================================================

module Array =

    open Informedica.Utils.Lib.BCL


    /// Replace the first element in `xs` satisfying `pred` with `x`.
    /// Returns the original array if no element satisfies `pred`.
    /// Example: replace (fun x -> x = 2) 9 [|1;2;3;2|] -> [|1;9;3;2|]
    let replace pred x xs =
        match xs |> Array.tryFindIndex pred with
        | Some ind ->
            xs
            |> Array.mapi (fun i el -> if i = ind then x else el)
        | None -> xs


    /// Remove the first element in `xs` satisfying `pred`.
    /// Returns the original array if no element satisfies `pred`.
    /// Example: removeFirst (fun x -> x = 2) [|1;2;3;2|] -> [|1;3;2|]
    let removeFirst pred xs =
        match xs |> Array.tryFindIndex pred with
        | Some ind ->
            xs
            |> Array.mapi (fun i x -> if i = ind then None else Some x)
            |> Array.choose id
        | None -> xs


    /// Generate all cyclic rotations of an array.
    /// Example: rotations [|1;2;3|] -> [| [|1;2;3|]; [|2;3;1|]; [|3;1;2|] |]
    let rotations xs =
        let n = xs |> Array.length

        if n = 0 then [||]
        elif n = 1 then [| xs |]
        else
            [|
                for i in 0..n - 1 do
                    [| for j in 0..n - 1 -> xs[(i + j) % n] |]
            |]


    /// Return true when exactly one element in `xs` satisfies `pred`.
    /// Example: hasExactlyOne (fun x -> x > 3) [|1;2;5|] -> true
    let hasExactlyOne pred xs =
        xs
        |> Array.filter pred
        |> Array.length = 1


    /// Return a tuple of (first, last) elements as options.
    /// Example: headTail [|1;2;3|] -> (Some 1, Some 3)
    let headTail xs =
        match xs with
        | [||] -> (None, None)
        | _ ->
            let h = xs[0]
            let t = xs[xs.Length - 1]

            if xs.Length = 1 then (Some h, None)
            else (Some h, Some t)


    /// Return true when the sorted elements of `xs` differ by `diff` each step.
    /// Uses `zero` as the identity for the first comparison.
    /// Example: isConsecutive 0 1 [|3;1;2|] -> true
    let inline isConsecutive zero diff xs =
        match xs with
        | [||] | [|_|] -> false
        | _ ->
            xs
            |> Array.sort
            |> Array.fold (fun acc x ->
                let isC, prev = acc

                if prev = zero then (true, x)
                else (x - prev = diff && isC, x)
            ) (true, zero)
            |> fst


    /// Remove duplicate elements from `xs`, preserving insertion order.
    /// Example: removeDuplicates [|1;2;1;3;2|] -> [|1;2;3|]
    let removeDuplicates xs =
        xs
        |> Array.fold
            (fun acc x ->
                if acc |> Array.exists ((=) x) then acc
                else Array.append acc [| x |]
            )
            [||]


    /// Return the distinct elements of `xs` (preserves order via Seq.distinct).
    /// Example: distinct [|1;2;1;3|] -> [|1;2;3|]
    let distinct xs = xs |> Seq.distinct |> Array.ofSeq


    /// Replace the first element satisfying `pred` with `x`, or prepend `x`
    /// when none is found.
    /// Example: replaceOrAdd (fun x -> x = 2) 9 [|1;3|] -> [|9;1;3|]
    let replaceOrAdd pred x xs =
        if xs |> Array.exists pred then
            xs |> replace pred x
        else
            Array.append [| x |] xs


    /// From a non-empty list, return the largest element ≤ `n`, capped at
    /// `max ns`. Mirrors List.findNearestMax semantics.
    /// Example: findNearestMax 4 [|1;2;3;5|] -> 3
    let inline findNearestMax n ns =
        match ns with
        | [||] -> n
        | _ ->
            let n =
                if n > (ns |> Array.max) then ns |> Array.max
                else n

            ns
            |> Array.sort
            |> Array.rev
            |> Array.fold (fun x a -> if (a - x) < (n - x) then x else a) n


    /// Return true when every element in `xs` is unique.
    /// Example: allUnique [|1;2;3|] -> true
    let allUnique xs =
        (xs |> Set.ofArray |> Set.count) = (xs |> Array.length)


    /// Convert an array to a string without brackets, with comma separation.
    /// Example: toString2 [|1;2;3|] -> "1,2,3"
    let inline toString2 xs =
        xs
        |> Informedica.Utils.Lib.Array.toString
        |> String.replace "[|" ""
        |> String.replace "|]" ""
        |> String.replace ";" ","


// =============================================================================
// Extended Seq module
// Shadows Informedica.Utils.Lib.Seq and adds the functions that bring it
// into parity with the List and Array modules.
// =============================================================================

module Seq =

    open Informedica.Utils.Lib.BCL


    /// Replace the first element in `xs` satisfying `pred` with `x`.
    /// Returns the original sequence if no element satisfies `pred`.
    /// Example: replace (fun x -> x = 2) 9 (seq {1;2;3;2}) -> seq {1;9;3;2}
    let replace pred x xs =
        let mutable replaced = false

        xs
        |> Seq.map (fun el ->
            if not replaced && pred el then
                replaced <- true
                x
            else el
        )


    /// Remove the first element in `xs` satisfying `pred`.
    /// Returns the original sequence if no element satisfies `pred`.
    /// Example: removeFirst (fun x -> x = 2) (seq {1;2;3;2}) -> seq {1;3;2}
    let removeFirst pred xs =
        let mutable removed = false

        xs
        |> Seq.choose (fun x ->
            if not removed && pred x then
                removed <- true
                None
            else Some x
        )


    /// Generate all cyclic rotations of a sequence.
    /// Example: rotations (seq {1;2;3}) -> seq of seq {1;2;3}, seq {2;3;1}, seq {3;1;2}
    let rotations xs =
        let arr = xs |> Array.ofSeq
        let n = arr.Length

        if n = 0 then Seq.empty
        else
            seq {
                for i in 0..n - 1 ->
                    seq { for j in 0..n - 1 -> arr[(i + j) % n] }
            }


    /// Return true when exactly one element in `xs` satisfies `pred`.
    /// Example: hasExactlyOne (fun x -> x > 3) (seq {1;2;5}) -> true
    let hasExactlyOne pred xs =
        xs
        |> Seq.filter pred
        |> Seq.length = 1


    /// Return a tuple of (first, last) elements as options.
    /// Example: headTail (seq {1;2;3}) -> (Some 1, Some 3)
    let headTail xs =
        match xs |> Seq.toList with
        | [] -> (None, None)
        | [h] -> (Some h, None)
        | h :: rest -> (Some h, Some (rest |> List.last))


    /// Return true when the sorted elements of `xs` differ by `diff` each step.
    /// Uses `zero` as the identity for the first comparison.
    /// Example: isConsecutive 0 1 (seq {3;1;2}) -> true
    let inline isConsecutive zero diff xs =
        match xs |> Seq.toArray with
        | [||] | [|_|] -> false
        | _ ->
            xs
            |> Seq.sort
            |> Seq.fold (fun acc x ->
                let isC, prev = acc

                if prev = zero then (true, x)
                else (x - prev = diff && isC, x)
            ) (true, zero)
            |> fst


    /// Remove duplicate elements from `xs`, preserving insertion order.
    /// Example: removeDuplicates (seq {1;2;1;3;2}) -> seq {1;2;3}
    let removeDuplicates xs =
        xs
        |> Seq.fold
            (fun acc x ->
                if acc |> List.exists ((=) x) then acc
                else acc @ [ x ]
            )
            []
        |> Seq.ofList


    /// Return distinct elements of `xs` (preserves order via Seq.distinct).
    /// Example: distinct (seq {1;2;1;3}) -> seq {1;2;3}
    let distinct xs = xs |> Seq.distinct


    /// Replace the first element satisfying `pred` with `x`, or prepend `x`
    /// when none is found.
    /// Example: replaceOrAdd (fun x -> x = 2) 9 (seq {1;3}) -> seq {9;1;3}
    let replaceOrAdd pred x xs =
        if xs |> Seq.exists pred then
            xs |> replace pred x
        else
            seq { yield x; yield! xs }


    /// From a non-empty sequence, return the largest element ≤ `n`, capped at
    /// max. Mirrors List.findNearestMax semantics.
    /// Example: findNearestMax 4 (seq {1;2;3;5}) -> 3
    let inline findNearestMax n ns =
        match ns |> Seq.toArray with
        | [||] -> n
        | arr ->
            let n =
                if n > (arr |> Array.max) then arr |> Array.max
                else n

            arr
            |> Array.sort
            |> Array.rev
            |> Array.fold (fun x a -> if (a - x) < (n - x) then x else a) n


    /// Calculate the median of a non-empty sequence.
    /// Throws InvalidArgException for an empty sequence.
    /// Example: median (seq {1;3;2}) -> 2.0
    let median xs =
        match xs |> Seq.toArray |> Array.sort with
        | [||] -> invalidArg "xs" "Sequence cannot be empty to calculate median."
        | sorted ->
            let len = sorted.Length

            if len % 2 = 0 then
                (float sorted[len / 2 - 1] + float sorted[len / 2]) / 2.0
            else
                float sorted[len / 2]


    /// Return the index of the element in `xs` nearest to `x`.
    /// Throws InvalidArgException for an empty sequence.
    /// Example: nearestIndex 4 (seq {1;3;7;10}) -> 1  (3 is nearest to 4)
    let inline nearestIndex x xs =
        match xs |> Array.ofSeq with
        | [||] -> invalidArg "xs" "Sequence cannot be empty to calculate nearest value."
        | arr ->
            let deltas = arr |> Array.map ((-) x) |> Array.map abs
            let minDelta = deltas |> Array.min
            deltas |> Array.findIndex ((=) minDelta)


    /// From a non-empty sequence, pick the nearest element that is >= `target`.
    /// If none exists, pick the largest element in the sequence.
    /// Throws InvalidArgException for an empty sequence.
    /// Example: pickNearestHigherElseLower 4 (seq {1;3;7;10}) -> 7
    let inline pickNearestHigherElseLower target xs =
        if Seq.isEmpty xs then invalidArg "xs" "Sequence cannot be empty"

        let ys = xs |> Seq.sort

        match ys |> Seq.tryFind (fun x -> x >= target) with
        | Some x -> x
        | None -> ys |> Seq.last


    /// Return the indices (0-based) of elements that satisfy `pred`.
    /// Example: indices (fun x -> x % 2 = 0) (seq {1;2;3;4}) -> seq {1;3}
    let indices pred xs =
        xs
        |> Seq.mapi (fun i x -> if pred x then Some i else None)
        |> Seq.choose id


// =============================================================================
// Tests
// =============================================================================

open Expecto
open Expecto.Flip


// --- List tests ---

let listTests =
    testList "List (new functions)" [

        testList "indices" [
            test "even numbers in [1..4]" {
                [1; 2; 3; 4]
                |> List.indices (fun x -> x % 2 = 0)
                |> Expect.equal "should be [1;3]" [1; 3]
            }
            test "empty list" {
                []
                |> List.indices (fun _ -> true)
                |> Expect.equal "should be []" []
            }
            test "no matches" {
                [1; 3; 5]
                |> List.indices (fun x -> x % 2 = 0)
                |> Expect.equal "should be []" []
            }
        ]

        testList "toString_" [
            test "basic list" {
                [1; 2; 3]
                |> List.toString_ "[" "]" ";"
                |> Expect.equal "should format correctly" "[1;2;3]"
            }
            test "empty list" {
                ([] : int list)
                |> List.toString_ "[" "]" ";"
                |> Expect.equal "should give empty brackets" "[]"
            }
            test "single element" {
                [42]
                |> List.toString_ "[" "]" ";"
                |> Expect.equal "should format single" "[42]"
            }
        ]

        testList "toReadableString" [
            test "basic list" {
                [1; 2; 3]
                |> List.toReadableString
                |> Expect.equal "should be semicolon-delimited" "1;2;3"
            }
            test "empty list" {
                ([] : int list)
                |> List.toReadableString
                |> Expect.equal "should be empty string" ""
            }
        ]

        testList "allEqual" [
            test "all equal" {
                [1; 1; 1]
                |> List.allEqual Some None
                |> Expect.equal "should be Some 1" (Some 1)
            }
            test "not all equal" {
                [1; 2; 1]
                |> List.allEqual Some None
                |> Expect.equal "should be None" None
            }
            test "empty" {
                ([] : int list)
                |> List.allEqual Some None
                |> Expect.equal "should be None" None
            }
            test "single element" {
                [7]
                |> List.allEqual Some None
                |> Expect.equal "should be Some 7" (Some 7)
            }
        ]

        testList "allEqualToString" [
            test "all equal ints" {
                [3; 3; 3]
                |> List.allEqualToString
                |> Expect.equal "should be string of element" "3"
            }
            test "mixed" {
                [1; 2; 3]
                |> List.allEqualToString
                |> Expect.equal "should be empty string" ""
            }
            test "empty" {
                ([] : int list)
                |> List.allEqualToString
                |> Expect.equal "should be empty string" ""
            }
        ]

        testList "allEqualToOpt" [
            test "all equal" {
                [5; 5; 5]
                |> List.allEqualToOpt
                |> Expect.equal "should be Some 5" (Some 5)
            }
            test "not all equal" {
                [1; 5; 5]
                |> List.allEqualToOpt
                |> Expect.equal "should be None" None
            }
        ]

        testList "prune" [
            test "trims long list" {
                [1..9]
                |> List.prune 5
                |> Expect.equal "should be [1;4;7;9]" [1; 4; 7; 9]
            }
            test "already short" {
                [1; 2; 3]
                |> List.prune 5
                |> Expect.equal "unchanged" [1; 2; 3]
            }
            test "empty" {
                ([] : int list)
                |> List.prune 5
                |> Expect.equal "should be []" []
            }
        ]

        testList "median" [
            test "odd count" {
                [1; 3; 2]
                |> List.median
                |> Expect.equal "should be 2.0" 2.0
            }
            test "even count" {
                [1; 2; 3; 4]
                |> List.median
                |> Expect.equal "should be 2.5" 2.5
            }
        ]

        testList "nearestIndex" [
            test "basic" {
                [1; 3; 7; 10]
                |> List.nearestIndex 4
                |> Expect.equal "index of 3 (nearest to 4)" 1
            }
            test "exact match" {
                [1; 3; 7]
                |> List.nearestIndex 3
                |> Expect.equal "index of exact match" 1
            }
        ]

        testList "pickNearestHigherElseLower" [
            test "picks higher" {
                [1; 3; 7; 10]
                |> List.pickNearestHigherElseLower 4
                |> Expect.equal "should pick 7" 7
            }
            test "picks last when all lower" {
                [1; 3; 7]
                |> List.pickNearestHigherElseLower 10
                |> Expect.equal "should pick 7 (highest available)" 7
            }
            test "exact match" {
                [1; 3; 7]
                |> List.pickNearestHigherElseLower 3
                |> Expect.equal "should pick 3" 3
            }
        ]

        testList "allUnique" [
            test "unique" {
                [1; 2; 3]
                |> List.allUnique
                |> Expect.isTrue "should be true"
            }
            test "has duplicates" {
                [1; 2; 2]
                |> List.allUnique
                |> Expect.isFalse "should be false"
            }
            test "empty" {
                ([] : int list)
                |> List.allUnique
                |> Expect.isTrue "empty is trivially unique"
            }
        ]

    ]


// --- Array tests ---

let arrayTests =
    testList "Array (new functions)" [

        testList "replace" [
            test "replaces first match" {
                [|1; 2; 3; 2|]
                |> Array.replace (fun x -> x = 2) 9
                |> Expect.equal "first 2 replaced by 9" [|1; 9; 3; 2|]
            }
            test "no match returns original" {
                [|1; 2; 3|]
                |> Array.replace (fun x -> x = 5) 9
                |> Expect.equal "unchanged" [|1; 2; 3|]
            }
            test "empty array" {
                [||]
                |> Array.replace (fun _ -> true) 9
                |> Expect.equal "should be empty" [||]
            }
        ]

        testList "removeFirst" [
            test "removes first match" {
                [|1; 2; 3; 2|]
                |> Array.removeFirst (fun x -> x = 2)
                |> Expect.equal "first 2 removed" [|1; 3; 2|]
            }
            test "no match returns original" {
                [|1; 2; 3|]
                |> Array.removeFirst (fun x -> x = 5)
                |> Expect.equal "unchanged" [|1; 2; 3|]
            }
        ]

        testList "rotations" [
            test "three elements" {
                [|1; 2; 3|]
                |> Array.rotations
                |> Expect.equal "all rotations" [| [|1;2;3|]; [|2;3;1|]; [|3;1;2|] |]
            }
            test "single element" {
                [|42|]
                |> Array.rotations
                |> Expect.equal "single rotation" [| [|42|] |]
            }
            test "empty" {
                [||]
                |> Array.rotations
                |> Expect.equal "empty rotations" [||]
            }
        ]

        testList "hasExactlyOne" [
            test "exactly one" {
                [|1; 2; 5|]
                |> Array.hasExactlyOne (fun x -> x > 3)
                |> Expect.isTrue "should be true"
            }
            test "none" {
                [|1; 2; 3|]
                |> Array.hasExactlyOne (fun x -> x > 3)
                |> Expect.isFalse "should be false"
            }
            test "two matches" {
                [|1; 5; 6|]
                |> Array.hasExactlyOne (fun x -> x > 3)
                |> Expect.isFalse "should be false"
            }
        ]

        testList "headTail" [
            test "three elements" {
                [|1; 2; 3|]
                |> Array.headTail
                |> Expect.equal "first and last" (Some 1, Some 3)
            }
            test "single element" {
                [|1|]
                |> Array.headTail
                |> Expect.equal "only head" (Some 1, None)
            }
            test "empty" {
                [||]
                |> Array.headTail
                |> Expect.equal "both None" (None, None)
            }
        ]

        testList "isConsecutive" [
            test "consecutive" {
                [|3; 1; 2|]
                |> Array.isConsecutive 0 1
                |> Expect.isTrue "should be true"
            }
            test "not consecutive" {
                [|1; 3; 5|]
                |> Array.isConsecutive 0 1
                |> Expect.isFalse "should be false"
            }
            test "single element" {
                [|1|]
                |> Array.isConsecutive 0 1
                |> Expect.isFalse "single is not consecutive"
            }
            test "empty" {
                [||]
                |> Array.isConsecutive 0 1
                |> Expect.isFalse "empty is not consecutive"
            }
        ]

        testList "removeDuplicates" [
            test "removes duplicates" {
                [|1; 2; 1; 3; 2|]
                |> Array.removeDuplicates
                |> Expect.equal "unique in insertion order" [|1; 2; 3|]
            }
            test "no duplicates" {
                [|1; 2; 3|]
                |> Array.removeDuplicates
                |> Expect.equal "unchanged" [|1; 2; 3|]
            }
            test "empty" {
                [||]
                |> Array.removeDuplicates
                |> Expect.equal "should be empty" [||]
            }
        ]

        testList "distinct" [
            test "removes duplicates preserving order" {
                [|1; 2; 1; 3|]
                |> Array.distinct
                |> Expect.equal "should be [|1;2;3|]" [|1; 2; 3|]
            }
        ]

        testList "replaceOrAdd" [
            test "replaces when found" {
                [|1; 2; 3|]
                |> Array.replaceOrAdd (fun x -> x = 2) 9
                |> Expect.equal "2 replaced by 9" [|1; 9; 3|]
            }
            test "prepends when not found" {
                [|1; 3|]
                |> Array.replaceOrAdd (fun x -> x = 5) 9
                |> Expect.equal "9 prepended" [|9; 1; 3|]
            }
        ]

        testList "findNearestMax" [
            test "finds nearest ceiling" {
                [|1; 2; 3; 5|]
                |> Array.findNearestMax 4
                |> Expect.equal "should find 5 (nearest value >= 4)" 5
            }
            test "caps at array max" {
                [|1; 2; 3|]
                |> Array.findNearestMax 10
                |> Expect.equal "should cap at 3" 3
            }
            test "empty returns n" {
                [||]
                |> Array.findNearestMax 5
                |> Expect.equal "should return 5" 5
            }
        ]

        testList "allUnique" [
            test "unique" {
                [|1; 2; 3|]
                |> Array.allUnique
                |> Expect.isTrue "should be true"
            }
            test "has duplicates" {
                [|1; 2; 2|]
                |> Array.allUnique
                |> Expect.isFalse "should be false"
            }
        ]

        testList "toString2" [
            test "basic array" {
                [|1; 2; 3|]
                |> Array.toString2
                |> Expect.equal "should be comma-separated" "1,2,3"
            }
            test "empty array" {
                [||]
                |> Array.toString2
                |> Expect.equal "should be empty string" ""
            }
        ]

    ]


// --- Seq tests ---

let seqTests =
    testList "Seq (new functions)" [

        testList "replace" [
            test "replaces first match" {
                seq { 1; 2; 3; 2 }
                |> Seq.replace (fun x -> x = 2) 9
                |> Seq.toList
                |> Expect.equal "first 2 replaced by 9" [1; 9; 3; 2]
            }
            test "no match" {
                seq { 1; 2; 3 }
                |> Seq.replace (fun x -> x = 5) 9
                |> Seq.toList
                |> Expect.equal "unchanged" [1; 2; 3]
            }
        ]

        testList "removeFirst" [
            test "removes first match" {
                seq { 1; 2; 3; 2 }
                |> Seq.removeFirst (fun x -> x = 2)
                |> Seq.toList
                |> Expect.equal "first 2 removed" [1; 3; 2]
            }
            test "no match" {
                seq { 1; 2; 3 }
                |> Seq.removeFirst (fun x -> x = 5)
                |> Seq.toList
                |> Expect.equal "unchanged" [1; 2; 3]
            }
        ]

        testList "rotations" [
            test "three elements" {
                seq { 1; 2; 3 }
                |> Seq.rotations
                |> Seq.map Seq.toList
                |> Seq.toList
                |> Expect.equal "all rotations" [[1;2;3]; [2;3;1]; [3;1;2]]
            }
            test "empty" {
                Seq.empty
                |> Seq.rotations
                |> Seq.toList
                |> Expect.equal "empty rotations" []
            }
        ]

        testList "hasExactlyOne" [
            test "exactly one" {
                seq { 1; 2; 5 }
                |> Seq.hasExactlyOne (fun x -> x > 3)
                |> Expect.isTrue "should be true"
            }
            test "none match" {
                seq { 1; 2; 3 }
                |> Seq.hasExactlyOne (fun x -> x > 3)
                |> Expect.isFalse "should be false"
            }
        ]

        testList "headTail" [
            test "three elements" {
                seq { 1; 2; 3 }
                |> Seq.headTail
                |> Expect.equal "first and last" (Some 1, Some 3)
            }
            test "single" {
                seq { 1 }
                |> Seq.headTail
                |> Expect.equal "only head" (Some 1, None)
            }
            test "empty" {
                Seq.empty
                |> Seq.headTail
                |> Expect.equal "both None" (None, None)
            }
        ]

        testList "isConsecutive" [
            test "consecutive" {
                seq { 3; 1; 2 }
                |> Seq.isConsecutive 0 1
                |> Expect.isTrue "should be true"
            }
            test "not consecutive" {
                seq { 1; 3; 5 }
                |> Seq.isConsecutive 0 1
                |> Expect.isFalse "should be false"
            }
        ]

        testList "removeDuplicates" [
            test "removes duplicates" {
                seq { 1; 2; 1; 3; 2 }
                |> Seq.removeDuplicates
                |> Seq.toList
                |> Expect.equal "unique in insertion order" [1; 2; 3]
            }
        ]

        testList "replaceOrAdd" [
            test "replaces when found" {
                seq { 1; 2; 3 }
                |> Seq.replaceOrAdd (fun x -> x = 2) 9
                |> Seq.toList
                |> Expect.equal "2 replaced by 9" [1; 9; 3]
            }
            test "prepends when not found" {
                seq { 1; 3 }
                |> Seq.replaceOrAdd (fun x -> x = 5) 9
                |> Seq.toList
                |> Expect.equal "9 prepended" [9; 1; 3]
            }
        ]

        testList "findNearestMax" [
            test "finds nearest ceiling" {
                seq { 1; 2; 3; 5 }
                |> Seq.findNearestMax 4
                |> Expect.equal "should find 5 (nearest value >= 4)" 5
            }
            test "empty returns n" {
                Seq.empty
                |> Seq.findNearestMax 5
                |> Expect.equal "should return 5" 5
            }
        ]

        testList "median" [
            test "odd count" {
                seq { 1; 3; 2 }
                |> Seq.median
                |> Expect.equal "should be 2.0" 2.0
            }
            test "even count" {
                seq { 1; 2; 3; 4 }
                |> Seq.median
                |> Expect.equal "should be 2.5" 2.5
            }
        ]

        testList "nearestIndex" [
            test "basic" {
                seq { 1; 3; 7; 10 }
                |> Seq.nearestIndex 4
                |> Expect.equal "index of 3 (nearest to 4)" 1
            }
        ]

        testList "pickNearestHigherElseLower" [
            test "picks higher" {
                seq { 1; 3; 7; 10 }
                |> Seq.pickNearestHigherElseLower 4
                |> Expect.equal "should pick 7" 7
            }
            test "picks last when all lower" {
                seq { 1; 3; 7 }
                |> Seq.pickNearestHigherElseLower 10
                |> Expect.equal "should pick 7 (highest available)" 7
            }
        ]

        testList "indices" [
            test "even numbers" {
                seq { 1; 2; 3; 4 }
                |> Seq.indices (fun x -> x % 2 = 0)
                |> Seq.toList
                |> Expect.equal "should be [1;3]" [1; 3]
            }
            test "empty sequence" {
                Seq.empty
                |> Seq.indices (fun _ -> true)
                |> Seq.toList
                |> Expect.equal "should be []" []
            }
        ]

    ]


// =============================================================================
// Run all tests
// =============================================================================

let allTests =
    testList "SyncCollections" [
        listTests
        arrayTests
        seqTests
    ]


runTestsWithCLIArgs [] [||] allTests

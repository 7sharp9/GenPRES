#I __SOURCE_DIRECTORY__
// load.fsx is stale (it never loads Types.fs/Core.fs, so `open Core` in
// ValueUnit.fs misresolves to Microsoft.FSharp.Core → FS0893). Reference the
// freshly built compiled DLL instead so toToken is exercised as shipped.
#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Expecto, 10.2.1"

open MathNet.Numerics

open Expecto
open Expecto.Flip

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib


let run = runTestsWithCLIArgs [] [||]


// Convenience constructors for the units used in these tests.
let mg v = ValueUnit.singleWithUnit Units.Mass.milliGram v
let g v = ValueUnit.singleWithUnit Units.Mass.gram v
let mL v = ValueUnit.singleWithUnit Units.Volume.milliLiter v
let mgWith vs = vs |> ValueUnit.withUnit Units.Mass.milliGram
let mgPerML v = ValueUnit.singleWithUnit (ValueUnit.per Units.Volume.milliLiter Units.Mass.milliGram) v


let tests =
    testList "ValueUnit.toToken" [

        test "is deterministic: the same ValueUnit yields the same token" {
            let vu = mg 250N

            vu |> ValueUnit.toToken
            |> Expect.equal "two calls on the same vu match" (vu |> ValueUnit.toToken)
        }

        test "two structurally equal ValueUnits yield the same token" {
            let exp = mg 250N |> ValueUnit.toToken

            mg 250N |> ValueUnit.toToken
            |> Expect.equal $"should equal token of an equal vu {exp}" exp
        }

        test "different values in the same unit yield different tokens" {
            let other = mg 500N |> ValueUnit.toToken

            mg 250N |> ValueUnit.toToken
            |> Expect.notEqual $"250 mg token must differ from 500 mg token {other}" other
        }

        test "different units with the same value yield different tokens" {
            let asMass = mg 5N |> ValueUnit.toToken

            mL 5N |> ValueUnit.toToken
            |> Expect.notEqual $"5 mL token must differ from 5 mg token {asMass}" asMass
        }

        // toToken is canonical across unit conversion: it pairs the base-
        // normalised value with the unit's GROUP (Group.unitToGroup), so equal
        // quantities expressed in different units of the same group collapse to
        // one token — exactly what a memoisation/cache key needs.
        test "equal ValueUnits in different units yield the SAME token" {
            ValueUnit.eqs (mg 1000N) (g 1N)
            |> Expect.isTrue "precondition: 1000 mg and 1 g are equal quantities"

            let asGram = g 1N |> ValueUnit.toToken

            mg 1000N |> ValueUnit.toToken
            |> Expect.equal $"1000 mg must share the token of 1 g {asGram}" asGram
        }

        test "multi-value arrays are encoded into the token" {
            let single = mgWith [| 1N |] |> ValueUnit.toToken

            mgWith [| 1N; 2N; 3N |] |> ValueUnit.toToken
            |> Expect.notEqual $"[1;2;3] mg must differ from [1] mg {single}" single
        }

        test "value order does not affect the token (values are canonicalised by sort)" {
            let ascending = mgWith [| 1N; 2N; 3N |] |> ValueUnit.toToken

            mgWith [| 3N; 2N; 1N |] |> ValueUnit.toToken
            |> Expect.equal $"a reordered array must yield the same token {ascending}" ascending
        }

        test "a set of distinct quantities produces a set of distinct tokens" {
            // Each entry is a distinct quantity: a different base value and/or a
            // different group. (Quantities equal up to unit conversion or value
            // ordering would collapse — those are covered by the tests above.)
            let vus =
                [
                    mg 1N           // 0.001 in Mass base
                    mg 2N
                    mg 250N
                    mg 500N
                    g 1N            // 1 in Mass base — distinct from all mg above
                    mL 1N           // Volume group
                    mL 5N
                    mgPerML 10N     // combined Mass/Volume group
                    mgWith [| 1N; 2N |]
                    mgWith [| 1N; 3N |]
                ]

            let tokens = vus |> List.map ValueUnit.toToken

            tokens
            |> List.distinct
            |> List.length
            |> Expect.equal "every distinct quantity must map to a distinct token" (vus |> List.length)
        }
    ]


tests |> run

let stuk = Units.General.general "stuk"
let mgPerStuk = 115N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> ValueUnit.per stuk)
let mcgPerStuk = 400N |> ValueUnit.singleWithUnit (Units.Mass.microGram |> ValueUnit.per stuk)

ValueUnit.collect

#r "nuget: Expecto"


// this first to FSI
#load "../Types.fs"
#load "../Localization.fs"
#load "../Utils.fs"
#load "../Models.fs"

open System
open System.IO
open System.Net
open Shared


System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


printfn $"{Terms.``Continuous Medication Advice``}"





let createUrl sheet id =
    $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&sheet={sheet}"

//https://docs.google.com/spreadsheets/d/1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM/edit?usp=sharing
[<Literal>]
let dataUrlId = "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


let download url =
    async {
        let req = WebRequest.Create(Uri(url))
        use! resp = req.AsyncGetResponse()
        use stream = resp.GetResponseStream()
        use reader = new StreamReader(stream)
        return reader.ReadToEnd()
    }


open Models

createUrl "emergencylist" dataUrlId
|> download
|> Async.RunSynchronously
|> Csv.parseCSV
|> EmergencyTreatment.parse
|> EmergencyTreatment.calculate (Some 1.) (Some 10.)
|> List.length

createUrl "continuousmeds" dataUrlId
|> download
|> Async.RunSynchronously
|> Csv.parseCSV
|> ContinuousMedication.parse
|> ContinuousMedication.calculate 10.


open Expecto
open Expecto.Flip


let emptyVar =
    Order.Variable.create "test" false None false None None false None

let vu vals =
    Order.ValueUnit.create (vals |> Array.map (fun v -> (string v, v))) "mg" "mass" true "nl" ""

let ovar =
    Order.OrderVariable.create "testOrderVar" emptyVar emptyVar emptyVar

open Types

let tests =
    testList "OrderVariable active pattern" [

        test "no incr and no vals returns NonNavigable" {
            let ovar = Order.OrderVariable.create "test" emptyVar emptyVar emptyVar IsNormal
            match ovar with
            | Order.OrderVariable.NonNavigable -> ()
            | _ -> failtest "expected NonNavigable"
        }

        test "no incr with vals None returns NonNavigable" {
            let varWithMin =
                Order.Variable.create "test" false (vu [| 0m |] |> Some) false None None false None 
            let ovar = Order.OrderVariable.create "test" emptyVar emptyVar varWithMin IsNormal
            match ovar with
            | Order.OrderVariable.NonNavigable -> ()
            | _ -> failtest "expected NonNavigable"
        }

        test "multiple vals returns Selectable" {
            let varWithVals =
                Order.Variable.create "test" false None false None None false (vu [| 1m; 2m; 3m |] |> Some)
            let ovar = Order.OrderVariable.create "test" emptyVar emptyVar varWithVals IsNormal
            match ovar with
            | Order.OrderVariable.Selectable -> ()
            | _ -> failtest "expected Selectable"
        }

        test "two vals returns Selectable" {
            let varWithVals =
                Order.Variable.create "test" false None false None None false (vu [| 5m; 10m |] |> Some)
            let ovar = Order.OrderVariable.create "test" emptyVar emptyVar varWithVals IsNormal
            match ovar with
            | Order.OrderVariable.Selectable -> ()
            | _ -> failtest "expected Selectable"
        }

        test "one val with defined incr returns Stepable" {
            let varWithOneVal =
                Order.Variable.create "test" false None false None None false (vu [| 1m |] |> Some)
            let defWithIncr =
                Order.Variable.create "test" false None false (vu [| 0.5m |] |> Some) None false None
            let ovar = Order.OrderVariable.create "test" defWithIncr emptyVar varWithOneVal IsNormal
            match ovar with
            | Order.OrderVariable.Stepable -> ()
            | _ -> failtest "expected Stepable"
        }

        test "one val without defined incr returns NonNavigable" {
            let varWithOneVal =
                Order.Variable.create "test" false None false None None false (vu [| 1m |] |> Some)
            let ovar = Order.OrderVariable.create "test" emptyVar emptyVar varWithOneVal IsNormal
            match ovar with
            | Order.OrderVariable.NonNavigable -> ()
            | _ -> failtest "expected NonNavigable"
        }

        test "defined incr with min and max returns Navigable" {
            let varWithMinMax =
                Order.Variable.create "test" false (vu [| 0m |] |> Some) false None (vu [| 100m |] |> Some) false None
            let defWithIncr =
                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None
            let ovar = Order.OrderVariable.create "test" defWithIncr emptyVar varWithMinMax IsNormal
            match ovar with
            | Order.OrderVariable.Navigable -> ()
            | _ -> failtest "expected Navigable"
        }

        test "defined incr with min but no max returns NonNavigable" {
            let varWithMinOnly =
                Order.Variable.create "test" false (vu [| 0m |] |> Some) false None None false None
            let defWithIncr =
                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None
            let ovar = Order.OrderVariable.create "test" defWithIncr emptyVar varWithMinOnly IsNormal
            match ovar with
            | Order.OrderVariable.NonNavigable -> ()
            | _ -> failtest "expected NonNavigable"
        }

        test "defined incr with max but no min returns NonNavigable" {
            let varWithMaxOnly =
                Order.Variable.create "test" false None false None (vu [| 100m |] |> Some) false None
            let defWithIncr =
                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None
            let ovar = Order.OrderVariable.create "test" defWithIncr emptyVar varWithMaxOnly IsNormal
            match ovar with
            | Order.OrderVariable.NonNavigable -> ()
            | _ -> failtest "expected NonNavigable"
        }

        test "multiple vals takes priority over incr with min/max" {
            let varWithValsAndMinMax =
                Order.Variable.create "test" false (vu [| 0m |] |> Some) false None (vu [| 100m |] |> Some) false (vu [| 1m; 2m |] |> Some)
            let defWithIncr =
                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None
            let ovar = Order.OrderVariable.create "test" defWithIncr emptyVar varWithValsAndMinMax IsNormal
            match ovar with
            | Order.OrderVariable.Selectable -> ()
            | _ -> failtest "expected Selectable, multiple vals should take priority"
        }
    ]

runTestsWithCLIArgs [] [||] tests

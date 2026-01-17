
#time

#r "nuget: expecto"

// load demo or product cache

#load "load.fsx"

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module HelperFunctions =


    let print sl = sl |> List.iter (printfn "%s")


    let inline printOrderTable order =
        order
        |> Result.iter (Order.printTable ConsoleTables.Format.Minimal)

        order


    let solveOrder order =
        match order with
        | Error e -> $"Error solving order: {e}" |> failwith
        | Ok o ->
            o
            |> Order.solveMinMax true OrderLogging.noOp


    let run logger med cmds =
        let logger, usePrintTable = logger |> Option.defaultValue OrderLogging.noOp, logger.IsNone
        let rec loop cmds ord =
            match cmds with
            | [] ->
                ord
                |> fun ord -> if usePrintTable then ord |> printOrderTable else ord

            | cmd::rest ->
                match ord with
                | Error (_, msgs) ->
                    failwith $"Errors occured: {msgs}"
                | Ok ord ->
                    ord
                    |> cmd
                    |> OrderProcessor.processPipeline logger None
                    |> loop rest


        med
        |> Medication.toOrderDto
        |> Order.Dto.fromDto
        |> function
          | Error msg -> failwith $"{msg}"
          | Ok ord ->
              ord
              |> Ok
              |> fun ord -> if usePrintTable then ord |> printOrderTable else ord
              |> loop cmds




module GenFormResult = Utils.GenFormResult
open HelperFunctions


let logger = OrderLogging.createConsoleLogger ()


let tests =
    let normalizeWords (s: string) =
        s.Split([| ' '; '\t'; '\n'; '\r' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> String.concat " "

    testList "medication" [
        test "pcm supp to string" {
            let actual =
                Scenarios.pcmSupp
                |> Medication.toString
                |> String.concat "\n"
                |> normalizeWords

            let expected =
                Scenarios.pcmSuppText
                |> normalizeWords

            actual
            |> Expect.equal "should be" expected
        }
    ]

runTestsWithCLIArgs [] [||] tests

Scenarios.amfo
|> Medication.toString
|> print


[
    CalcMinMax
    CalcValues
    fun ord ->
        (ord, SetMedianComponentQuantity Scenarios.amfo.Components[0].Name)
        |> OrderCommand.ChangeProperty
    fun ord ->
        (ord, SetMedianComponentQuantity Scenarios.amfo.Components[1].Name)
        |> OrderCommand.ChangeProperty
]
|> run None Scenarios.amfo
//|> printOrderTable
|> ignore


Scenarios.morfCont
|> Medication.toString
|> print


[
    CalcMinMax
    CalcValues
    fun ord ->
        (ord, SetMedianComponentQuantity Scenarios.morfCont.Components[0].Name)
        |> OrderCommand.ChangeProperty
    (*
    fun ord ->
        (ord, SetMedianComponentQuantity morfCont.Components[1].Name)
        |> OrderCommand.ChangeProperty
    *)
]
|> run None Scenarios.morfCont
//|> printOrderTable
|> ignore


open Types

Scenarios.pcmDrink
|> Medication.toString
|> print



Scenarios.cotrim
|> Medication.toString
|> print


Scenarios.tpn
|> Medication.toString
|> print


let tpnConstraints =
    [
        OrderAdjust OrderVariable.Quantity.applyConstraints

        ScheduleFrequency OrderVariable.Frequency.applyConstraints
        ScheduleTime OrderVariable.Time.applyConstraints

        OrderableQuantity OrderVariable.Quantity.applyConstraints
        OrderableDoseCount OrderVariable.Count.applyConstraints
        OrderableDose Order.Orderable.Dose.applyConstraints

        ComponentOrderableQuantity ("", OrderVariable.Quantity.applyConstraints)

        ItemComponentConcentration ("", "", OrderVariable.Concentration.applyConstraints)
        ItemOrderableConcentration ("", "", OrderVariable.Concentration.applyConstraints)
    ]



let applyPropChange msg propChange ord =
    printfn $"=== Apply PropChange {msg} ==="
    let ord =
        ord
        |> Order.OrderPropertyChange.proc propChange
    ord
    |> Order.solveMinMax true Logging.noOp
    |> function
        | Ok ord -> ord
        | _ ->
            printfn $"=== ERROR {msg} ==="
            ord
    |> fun ord ->
        ord
        |> Order.printTable ConsoleTables.Format.Minimal

        ord


let run
    proteinPerc
    potassiumPerc
    sodiumPerc
    glucPerc
    tpn =

    tpn
    |> Medication.toOrderDto
    |> Order.Dto.fromDto
    |> Result.map (fun ord ->
        let ord =
            ord
            |> Order.OrderPropertyChange.proc tpnConstraints
    //        |> Order.applyConstraints

        ord
        |> Order.printTable ConsoleTables.Format.Minimal

        let ord =
            ord
            |> Order.solveMinMax true Logging.noOp //logger
            //|> Result.bind (Order.solveMinMax true logger)

        ord
        |> Result.iter (Order.printTable ConsoleTables.Format.Minimal)

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "Samenstelling C"
                    [
                        ComponentOrderableQuantity ("Samenstelling C", OrderVariable.Quantity.setPercValue proteinPerc)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "KCl 7,4%"
                    [
                        ComponentOrderableQuantity ("KCl 7,4%", OrderVariable.Quantity.setPercValue potassiumPerc)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "NaCl 3%"
                    [
                        ComponentOrderableQuantity ("NaCl 3%", OrderVariable.Quantity.setPercValue sodiumPerc)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "gluc 10%"
                    [
                        ComponentOrderableQuantity ("gluc 10%", OrderVariable.Quantity.setPercValue glucPerc)
                    ]
            )

        ord
    )


Scenarios.tpn
|> run 50 0 5 0
|> ignore

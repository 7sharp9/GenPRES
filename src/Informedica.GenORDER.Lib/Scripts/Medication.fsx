
#time

#r "nuget: expecto"

// load demo or product cache

#load "load.fsx"


module HelperFunctions =

    open Informedica.GenOrder.Lib


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
            |> Order.solveMinMax "Solve Order" true OrderLogging.noOp


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
                    |> OrderProcessor.processPipeline logger
                    |> loop rest

        med
        |> Informedica.GenOrder.Lib.Medication.toOrderDto
        |> Order.Dto.fromDto
        |> function
          | Error msg -> failwith $"{msg}"
          | Ok ord ->
              ord
              |> Ok
              |> fun ord -> if usePrintTable then ord |> printOrderTable else ord
              |> loop cmds



open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip



module GenFormResult = Utils.GenFormResult
open HelperFunctions


let logger = OrderLogging.createConsoleLogger ()


let tests =
    let normalizeWords (s: string) =
        s.Split([| ' '; '\t'; '\n'; '\r' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> String.concat " "

    testList "medication" [

        // Test labeled DoseLimit.toString
        testList "DoseLimit with field labels" [
            test "Quantity field gets [qty] label" {
                let dl = { DoseLimit.limit with
                            Quantity = 10N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> MinMax.createExact
                         }
                let str = dl |> DoseLimit.toString |> String.concat ""
                str |> Expect.stringContains "should contain [qty]" "[qty]"
            }

            test "QuantityAdjust field gets [qty-adj] label" {
                let dl = { DoseLimit.limit with
                            QuantityAdjust =
                                MinMax.createInclIncl
                                    (10N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram))
                                    (20N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram))
                         }
                let str = dl |> DoseLimit.toString |> String.concat ""
                str |> Expect.stringContains "should contain [qty-adj]" "[qty-adj]"
            }

            test "PerTimeAdjust field gets [per-time-adj] label" {
                let dl = { DoseLimit.limit with
                            PerTimeAdjust =
                                MinMax.createInclIncl
                                    (10N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram |> Units.per Units.Time.day))
                                    (20N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram |> Units.per Units.Time.day))
                         }
                let str = dl |> DoseLimit.toString |> String.concat ""
                str |> Expect.stringContains "should contain [per-time-adj]" "[per-time-adj]"
            }

            test "RateAdjust field gets [rate-adj] label" {
                let dl = { DoseLimit.limit with
                            RateAdjust =
                                MinMax.createInclIncl
                                    (10N |> ValueUnit.singleWithUnit (Units.Mass.microGram |> Units.per Units.Weight.kiloGram |> Units.per Units.Time.hour))
                                    (40N |> ValueUnit.singleWithUnit (Units.Mass.microGram |> Units.per Units.Weight.kiloGram |> Units.per Units.Time.hour))
                         }
                let str = dl |> DoseLimit.toString |> String.concat ""
                str |> Expect.stringContains "should contain [rate-adj]" "[rate-adj]"
            }
        ]

        // Unit validation tests
        testList "Unit validation" [
            test "hasAdjustUnit detects kg" {
                let unit = Units.Mass.milliGram |> Units.per Units.Weight.kiloGram
                UnitValidation.hasAdjustUnit unit
                |> Expect.isTrue "should detect kg as adjust unit"
            }

            test "hasAdjustUnit detects m2" {
                let unit = Units.Mass.milliGram |> Units.per Units.BSA.m2
                UnitValidation.hasAdjustUnit unit
                |> Expect.isTrue "should detect m2 as adjust unit"
            }

            test "hasTimeUnit detects day" {
                let unit = Units.Mass.milliGram |> Units.per Units.Time.day
                UnitValidation.hasTimeUnit unit
                |> Expect.isTrue "should detect day as time unit"
            }

            test "hasTimeUnit detects hour" {
                let unit = Units.Volume.milliLiter |> Units.per Units.Time.hour
                UnitValidation.hasTimeUnit unit
                |> Expect.isTrue "should detect hour as time unit"
            }

            test "complex unit mg/kg/dag has both adjust and time" {
                let unit = Units.Mass.milliGram |> Units.per Units.Weight.kiloGram |> Units.per Units.Time.day
                UnitValidation.hasAdjustUnit unit |> Expect.isTrue "should have adjust unit"
                UnitValidation.hasTimeUnit unit |> Expect.isTrue "should have time unit"
            }
        ]

        // Roundtrip tests
        test "pcmSupp roundtrip - basic fields" {
            let original = Scenarios.pcmSupp
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                    let errMsg = errs |> String.concat "; "
                    failwith $"Parse failed: {errMsg}"
            | Ok med ->
                med.Id |> Expect.equal "Id" original.Id
                med.Name |> Expect.equal "Name" original.Name
                med.Route |> Expect.equal "Route" original.Route
                med.OrderType |> Expect.equal "OrderType" original.OrderType
                med.Components.Length |> Expect.equal "Components count" original.Components.Length
        }

        test "pcmSupp roundtrip - component details" {
            let original = Scenarios.pcmSupp
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                    let errMsg = errs |> String.concat "; "
                    failwith $"Parse failed: {errMsg}"
            | Ok med ->
                let origCmp = original.Components |> List.head
                let parsedCmp = med.Components |> List.head
                parsedCmp.Name |> Expect.equal "Component Name" origCmp.Name
                parsedCmp.Form |> Expect.equal "Component Form" origCmp.Form
                parsedCmp.Substances.Length |> Expect.equal "Substances count" origCmp.Substances.Length
        }

        test "pcmSupp roundtrip - full roundtrip" {
            let original = Scenarios.pcmSupp
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                    let errMsg = errs |> String.concat "; "
                    failwith $"Parse failed: {errMsg}"
            | Ok med ->
                med
                |> Medication.toString
                |> String.concat "\n"
                |> Expect.equal "should resemble the original text" text
        }

        test "amfo roundtrip - PerTimeAdjust field" {
            let original = Scenarios.amfo
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                let errMsg = errs |> String.concat "; "
                failwith $"Parse failed: {errMsg}"
            | Ok med ->
                med.Id |> Expect.equal "Id" original.Id
                med.Name |> Expect.equal "Name" original.Name
                med.OrderType |> Expect.equal "OrderType" original.OrderType

                // Check the substance has PerTimeAdjust
                let origSubstance =
                    original.Components
                    |> List.head
                    |> _.Substances
                    |> List.find (fun s -> s.Name = "amfotericine b liposomaal")

                let parsedSubstance =
                    med.Components
                    |> List.head
                    |> _.Substances
                    |> List.find (fun s -> s.Name = "amfotericine b liposomaal")

                parsedSubstance.Dose.IsSome |> Expect.isTrue "Dose should be Some"
                parsedSubstance.Dose.Value.PerTimeAdjust
                |> Expect.notEqual "PerTimeAdjust should not be empty" MinMax.empty
        }

        test "morfCont roundtrip - RateAdjust field" {
            let original = Scenarios.morfCont
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                let errMsg = errs |> String.concat "; "
                failwith $"Parse failed: {errMsg}"
            | Ok med ->
                med.Id |> Expect.equal "Id" original.Id
                med.OrderType |> Expect.equal "OrderType" original.OrderType

                // Check the substance has RateAdjust
                let parsedSubstance =
                    med.Components
                    |> List.head
                    |> _.Substances
                    |> List.find (fun s -> s.Name = "morfin")

                parsedSubstance.Dose.IsSome |> Expect.isTrue "Dose should be Some"
                parsedSubstance.Dose.Value.RateAdjust
                |> Expect.notEqual "RateAdjust should not be empty" MinMax.empty
        }

        test "cotrim roundtrip - QuantityAdjust field" {
            let original = Scenarios.cotrim
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                let errMsg = errs |> String.concat "; "
                failwith $"Parse failed: {errMsg}"
            | Ok med ->
                med.Id |> Expect.equal "Id" original.Id
                med.OrderType |> Expect.equal "OrderType" original.OrderType

                // Check the substances have QuantityAdjust
                let parsedSubstances =
                    med.Components
                    |> List.head
                    |> _.Substances

                for sub in parsedSubstances do
                    sub.Dose.IsSome |> Expect.isTrue $"Dose for {sub.Name} should be Some"
                    sub.Dose.Value.QuantityAdjust
                    |> Expect.notEqual $"QuantityAdjust for {sub.Name} should not be empty" MinMax.empty
        }

        test "tpn roundtrip - complex multi-component" {
            let original = Scenarios.tpn
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                let errMsg = errs |> String.concat "; "
                failwith $"Parse failed: {errMsg}"
            | Ok med ->
                med.Id |> Expect.equal "Id" original.Id
                med.Components.Length |> Expect.equal "Components count" original.Components.Length

                // Check component doses with QuantityAdjust
                for i, origCmp in original.Components |> List.indexed do
                    let parsedCmp = med.Components[i]
                    parsedCmp.Name |> Expect.equal $"Component {i} name" origCmp.Name
                    if origCmp.Dose.IsSome then
                        parsedCmp.Dose.IsSome |> Expect.isTrue $"Component {i} Dose should be Some"
        }

        test "fullMedication roundtrip - all fields" {
            let original = Scenarios.fullMedication
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs ->
                    let errMsg = errs |> String.concat "; "
                    failwith $"Parse failed: {errMsg}"
            | Ok med ->
                med.Id |> Expect.equal "Id" original.Id
                med.Name |> Expect.equal "Name" original.Name
                med.Route |> Expect.equal "Route" original.Route
                med.OrderType |> Expect.equal "OrderType" original.OrderType
                med.Components.Length |> Expect.equal "Components count" original.Components.Length
                // Check Div field
                med.Div.IsSome |> Expect.equal "Div is Some" original.Div.IsSome
        }

        test "fromString returns error for invalid OrderType" {
            let invalidText = """
Id: test-id
Name: test
Route: test
OrderType: InvalidType
Components:
"""
            match invalidText |> Medication.fromString with
            | Error errs ->
                errs |> List.exists _.Contains("Unknown OrderType")
                |> Expect.isTrue "should contain OrderType error"
            | Ok _ ->
                failwith "Expected error for invalid OrderType"
        }

        // Test that labels enable deterministic parsing
        test "labeled parsing is deterministic for QuantityAdjust vs PerTimeAdjust" {
            // This tests that with labels, we can distinguish fields that have similar units
            let dlWithQtyAdj = { DoseLimit.limit with
                                    DoseLimitTarget = "test" |> SubstanceLimitTarget
                                    QuantityAdjust =
                                        MinMax.createInclIncl
                                            (10N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram))
                                            (20N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram))
                               }

            let str = dlWithQtyAdj |> DoseLimit.toString |> String.concat ""
            str |> Expect.stringContains "should have [qty-adj] label" "[qty-adj]"
            str |> Expect.stringContains "should NOT have [per-time-adj] label" |> ignore
            (str.Contains("[per-time-adj]") |> not) |> Expect.isTrue "should NOT have [per-time-adj]"
        }
    ]


runTestsWithCLIArgs [] [||] tests

"3.0;4.0 x[Count]/day[Time]"
|> ValueUnit.fromString

// Demo: Show the new labeled output format
printfn "\n=== Demo: Labeled DoseLimit output ==="
Scenarios.amfo
|> Medication.toString
|> print


printfn "\n=== Demo: morfCont (RateAdjust) ==="
Scenarios.morfCont
|> Medication.toString
|> print


// Verify orders still work correctly
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
|> ignore


[
    CalcMinMax
    CalcValues
    fun ord ->
        (ord, SetMedianComponentQuantity Scenarios.morfCont.Components[0].Name)
        |> OrderCommand.ChangeProperty
]
|> run None Scenarios.morfCont
|> ignore


printfn "\n=== Demo: pcmDrink ==="
Scenarios.pcmDrink
|> Medication.toString
|> print


printfn "\n=== Demo: cotrim ==="
Scenarios.cotrim
|> Medication.toString
|> print


printfn "\n=== Demo: tpn ==="
Scenarios.tpn
|> Medication.toString
|> print

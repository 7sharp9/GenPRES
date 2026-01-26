
#time

#r "nuget: expecto"
#r "nuget: FSharp.Data"

#load "load.fsx"

// load demo or product cache

open System

Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM")

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


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


module UnitValidation = Medication.UnitValidation


let tests =


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

        // Indentation handling tests - verify both tabs and spaces work
        testList "parseLine handles different indentation" [
            test "parseLine handles tab indentation" {
                let line = "\t\tName: test"
                match Medication.Parser.parseLine line with
                | Some (indent, key, value) ->
                    indent |> Expect.equal "should be indent 2" 2
                    key |> Expect.equal "key" "Name"
                    value |> Expect.equal "value" "test"
                | None -> failwith "Expected successful parse"
            }

            test "parseLine handles space indentation (4 spaces = 1 indent)" {
                let line = "        Name: test"  // 8 spaces = indent 2
                match Medication.Parser.parseLine line with
                | Some (indent, key, value) ->
                    indent |> Expect.equal "should be indent 2" 2
                    key |> Expect.equal "key" "Name"
                    value |> Expect.equal "value" "test"
                | None -> failwith "Expected successful parse"
            }

            test "fromString works with space-indented input" {
                // This simulates what VS Code FSI might send
                let spaceIndented = """
Id: test-id
Name: test-med
Route: ORAAL
OrderType: OnceOrder
Components:

    Name: comp1
    Form: tablet
    Substances:

        Name: subst1
        Concentrations: 10 mg/stuk
"""
                match spaceIndented |> Medication.fromString with
                | Error errs ->
                    let errMsg = errs |> String.concat "; "
                    failwith $"Parse with spaces failed: {errMsg}"
                | Ok med ->
                    med.Components.Length |> Expect.equal "should have 1 component" 1
                    let comp = med.Components |> List.head
                    comp.Name |> Expect.equal "component name" "comp1"
                    comp.Substances.Length |> Expect.equal "should have 1 substance" 1
            }
        ]
    ]


runTestsWithCLIArgs [] [||] tests


module MedicationTexts =


    let onceSingleComponentSingleItemNoDose = """
Id: 4b73efb9-97b3-44b1-af51-bfad909a9371
Name: chloorhexidine
Quantity:
Quantities:
Route: CUTAAN
OrderType: OnceOrder
Adjust: 11 kg
Frequencies:
Time:
Dose:
Div:
DoseCount: 1 x
Components:

	Name: chloorhexidine
	Form: creme
	Quantities: 1 g
	Divisible:
	Dose: chloorhexidine, [dun] x
	Solution:
	Substances:

		Name: was
		Concentrations: 150 mg/g
		Dose:
		Solution:

		Name: decyloleaat
		Concentrations: 200 mg/g
		Dose:
		Solution:

		Name: sorbitol
		Concentrations: 28 mg/g
		Dose:
		Solution:

		Name: chloorhexidine
		Concentrations: 10 mg/g
		Dose:
		Solution:
"""


    // Once single component single item scenario
    let onceSingleComponentSingleItem = """
Id: 93e8c175-99a1-48d8-b2f4-90005fdb8ada
Name: paracetamol
Quantity:
Quantities:
Route: RECTAAL
OrderType: OnceOrder
Adjust: 10 kg
Frequencies:
Time:
Dose: [dun], [qty] 1 stuk/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: zetpil
	Quantities: 1 stuk
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 120;240;500;1000;125;250;60;30;360;90;750;180 mg/stuk
		Dose: paracetamol, [dun] mg, [qty-adj] 40 mg/kg/dosis, [qty] max 1000 mg/dosis
		Solution:
"""

    // OnceTimed single component single item scenario
    let onceTimedSingleComponentSingleItem = """
Id: 95c44266-84c5-4969-a815-9fbf2c9ed693
Name: paracetamol
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: OnceTimedOrder
Adjust: 10 kg
Frequencies:
Time: 15 min - 20 min
Dose: [dun], [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: infusievloeistof
	Quantities: 100;50 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 10 mg/ml
		Dose: paracetamol, [dun] mg, [qty-adj] 20 mg/kg/dosis, [qty] max 1000 mg/dosis
		Solution:
"""

    // Discontinuous single component single item scenario
    let discontinuousSingleComponentSingleItem = """
d: e043a880-70ec-4600-8e5a-109e5ef39108
Name: paracetamol
Quantity:
Quantities:
Route: RECTAAL
OrderType: DiscontinuousOrder
Adjust: 10 kg
Frequencies: 3;4 x/day
Time:
Dose: [dun], [qty] 1 stuk/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: zetpil
	Quantities: 1 stuk
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 120;240;500;1000;125;250;60;30;360;90;750;180 mg/stuk
		Dose: paracetamol, [dun] mg, [qty-adj] 10 mg/kg - 20 mg/kg/dosis
		Solution:
"""

    // Timed single component single item scenario
    let timedSingleComponentSingleItem = """
Id: a9e18942-f879-4df1-bc21-6375c3291ed7
Name: paracetamol
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 10 kg
Frequencies: 4 x/day
Time: 15 min - 20 min
Dose: [dun], [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: infusievloeistof
	Quantities: 100;50 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 10 mg/ml
		Dose: paracetamol, [dun] mg, [per-time-adj] 60 mg/kg/day, [per-time] max 4000 mg/day, [qty] max 1000 mg/dosis
		Solution:
"""


module MedicationScenarios =


    let run () =

        MedicationTexts.onceSingleComponentSingleItem
        |> Medication.fromString
        |> Result.map (fun med ->
            [
                OrderCommand.CalcMinMax
            ]
            |> run (Some logger) med
        )
        |> ignore


        MedicationTexts.discontinuousSingleComponentSingleItem
        |> Medication.fromString
        |> Result.map (fun med ->
            [
                OrderCommand.CalcMinMax
            ]
            |> run (Some logger) med
        )
        |> ignore


        MedicationTexts.onceTimedSingleComponentSingleItem
        |> Medication.fromString
        |> Result.map (fun med ->
            [
                OrderCommand.CalcMinMax
            ]
            |> run (Some logger) med
        )
        |> ignore


        MedicationTexts.timedSingleComponentSingleItem
        |> Medication.fromString
        |> Result.map (fun med ->
            [
                OrderCommand.CalcMinMax
            ]
            |> run (Some logger) med
        )
        |> ignore


        MedicationTexts.onceSingleComponentSingleItemNoDose
        |> Medication.fromString
        |> Result.bind (fun med ->
            [
                OrderCommand.CalcMinMax
            ]
            |> run (Some logger) med
            |> Result.mapError (fun _ -> [])
        )
        |> function
            | Ok ord -> ord |> Order.Print.printOrderToTableFormat false true [||]
            | Error _ -> "cannot run order" |> failwith
        |> ignore



"""
Id: f5cfab2b-9395-4a24-a10b-9601b6127c1c
Name: chloorhexidine
Quantity:
Quantities:
Route: CUTAAN
OrderType: OnceOrder
Adjust: 11 kg
Frequencies:
Time:
Dose:
Div:
DoseCount: 1 x
Components:

	Name: chloorhexidine
	Form: oplossing voor cutaan gebruik
	Quantities: 1;100;50 ml
	Divisible: 1
	Dose: chloorhexidine, [dun] dosis, [qty] 1 dosis/dosis
	Solution:
	Substances:

		Name: ethanol, gedenatureerd
		Concentrations: 539 mg/ml
		Dose:
		Solution:

		Name: chloorhexidine
		Concentrations: 5;10;40 mg/ml
		Dose:
		Solution:
"""
|> Medication.fromString
(*
|> Result.bind (fun med ->
    [
        OrderCommand.CalcMinMax
    ]
    |> run (Some logger) med
    |> Result.mapError (fun _ -> [])
)
|> function
    | Ok ord -> ord |> Order.Print.printOrderToTableFormat false true [||]
    | Error _ -> "cannot run order" |> failwith
|> ignore
*)

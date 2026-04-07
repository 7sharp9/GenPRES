#I __SOURCE_DIRECTORY__
#time

#load "load.fsx"


open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open ConsoleWriter.NewLineNoTime
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Order
open Informedica.GenOrder.Lib.Types


let path = Environment.CurrentDirectory |> Path.combineWith "staged_expansion.log"
let fileLogger = OrderLogging.createFileLogger path


// === Shadow minIncrMaxToValues with skipRate parameter ===

module Order =

    open Informedica.GenOrder.Lib.Order

    module Quantity = OrderVariable.Quantity
    module Rate = OrderVariable.Rate
    module Schedule = Order.Schedule
    module Variable = Informedica.GenSolver.Lib.Variable


    /// Modified minIncrMaxToValues that accepts a skipRate flag.
    /// When skipRate = true, the orderable dose rate variable is
    /// NOT expanded from min/incr/max to discrete values.
    /// This prevents combinatorial explosion for OnceTimed/Timed
    /// orders where dos_qty = dos_rte * sch_tme.
    let minIncrMaxToValues useMaxNumberOfValues minTime (skipRate: bool) logger ord =
        let mutable isSolved = false

        let rec loop ord =
            let mutable flag = false

            let ovars =
                [
                    yield!
                        ord.Orderable.Components
                        |> List.map (_.OrderableQuantity >> Quantity.toOrdVar)
                    ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                    if not skipRate then
                        ord.Orderable.Dose.Rate |> Rate.toOrdVar
                ]
                |> List.map (fun ovar ->
                    if
                        flag
                        || ovar.DefinedConstraints.Incr |> Option.isNone
                        || ovar.Variable |> Variable.isMinIncrMax |> not
                    then
                        ovar
                    else
                        flag <- true

                        let n =
                            match ord.Schedule with
                            | Continuous _ -> if useMaxNumberOfValues then 1_000 else 100
                            | Once
                            | Discontinuous _ ->
                                if useMaxNumberOfValues then
                                    if ord.Orderable.Components |> List.length > 1 then
                                        20
                                    else
                                        100
                                else
                                    10
                            | OnceTimed _
                            | Timed _ ->
                                if useMaxNumberOfValues then
                                    if ord.Orderable.Components |> List.length > 2 then
                                        5
                                    else
                                        10
                                else if ord.Orderable.Components |> List.length > 2 then
                                    5
                                else
                                    20
                            |> Some

                        ovar
                        |> OrderVariable.minIncrMaxToValues n
                        |> fun ovar ->
                            Events.MinIncrMaxToValues ovar
                            |> Logging.logInfo logger

                            ovar
                )

            if not flag then
                ord
            else
                ord
                |> fromOrdVars ovars
                |> solveOrder "Min Incr Max to Values" true logger
                |> function
                    | Ok ord ->
                        isSolved <- true
                        loop ord
                    | Error err ->
                        err
                        |> snd
                        |> List.map (sprintf "%A")
                        |> String.concat "\n"
                        |> writeErrorMessage

                        ord

        if minTime then
            ord |> Informedica.GenOrder.Lib.Order.minimizeTime logger
        else
            ord
        |> loop
        |> fun ord ->
            if not isSolved then
                ord
                |> solveOrder "Min Incr Max to Values final Loop" true logger
                |> Result.defaultValue ord
            else
                ord


// === Shadow OrderProcessor pipeline with staged expansion ===

module OrderProcessor =

    open Informedica.GenOrder.Lib.OrderProcessor
    open Informedica.GenOrder.Lib.Order

    module Schedule = Order.Schedule


    /// Modified processPipeline that uses staged value expansion
    /// for OnceTimed/Timed orders.
    let processPipelineStaged logger cmd =

        let runStep (step: Step) (ord: Informedica.GenOrder.Lib.Types.Order) =
            if step.Guard(classify ord) then
                $"\n=== PIPELINE START {step.Name} ===\n"
                |> Events.OrderScenario
                |> Logging.logInfo logger

                step.Run ord
                |> function
                    | Ok ord -> Ok ord
                    | Error(ord, msgs) ->
                        $"Error in {step.Name}"
                        |> Events.OrderScenario
                        |> Logging.logInfo logger

                        Error(ord, msgs)
                |> fun res ->
                    $"\n=== PIPELINE END {step.Name} ===\n"
                    |> Events.OrderScenario
                    |> Logging.logInfo logger

                    res
            else
                Ok ord

        let runPipeline (ord: Informedica.GenOrder.Lib.Types.Order) (steps: Step list) =
            (Ok ord, steps)
            ||> List.fold (fun acc step -> acc |> Result.bind (runStep step))

        let calcMinMaxStep = calcMinMax logger

        let setCalculatedConstraintsStep =
            setCalculatedConstraints
            >> fun ord ->
                ord
                |> toConsoleTableString
                |> Events.OrderScenario
                |> Logging.logInfo logger

                ord
            >> Ok

        let increaseIncrementStep ord = ord |> increaseIncrements logger 10 10

        // Modified: accepts skipRate parameter
        let calcValuesStep useMax skipRate ord =
            ord
            |> Order.minIncrMaxToValues useMax true skipRate logger
            |> Ok

        let reCalcValuesStep useMax skipRate ord =
            ord
            |> Order.minIncrMaxToValues useMax false skipRate logger
            |> Ok

        let solveStep ord = solveOrder "Solve Order Step" true logger ord

        let calcNormDoseStep = solveNormDose logger

        let processClearedStep ord =
            match processClearedOrder logger ord with
            | Ok o -> Ok o
            | Error _ -> solveOrder "Error in Cleared Step Solve again" true logger ord

        let applyConstraintsStep ord =
            ord
            |> applyConstraints
            |> fun ord ->
                ord
                |> toConsoleTableString
                |> Events.OrderScenario
                |> Logging.logInfo logger

                ord
            |> Ok

        let pickNearestHigherElseLowerComponentQuantityStep ord =
            ord |> pickNearestHigherElseLowerComponentQuantity logger

        match cmd with
        | CalcMinMax ord ->
            let hasTimeNotContinuous =
                ord.Schedule |> Schedule.hasTime
                && ord.Schedule |> Schedule.isContinuous |> not

            [
                {
                    Name = "calc-minmax: apply-constraints"
                    Guard = (fun _ -> true)
                    Run = applyConstraintsStep
                }
                {
                    Name = "calc-minmax: calc-minmax"
                    Guard = (fun _ -> true)
                    Run = calcMinMaxStep
                }
                {
                    Name = "calc-minmax: increase-increments"
                    Guard = (fun _ -> true)
                    Run = increaseIncrementStep
                }
                {
                    Name = "calc-minmax: set-calculated-constraints"
                    Guard = (fun _ -> true)
                    Run = setCalculatedConstraintsStep
                }
                if ord |> hasNormDose && ord.Orderable.Components |> List.length <= 2 then
                    {
                        Name = "solve-order: ensure-dose-values-1"
                        Guard = _.CanSetNormDose >> not
                        // skipRate=true for OnceTimed/Timed to avoid explosion
                        Run = calcValuesStep (ord.Orderable.Components |> List.length <= 2) hasTimeNotContinuous
                    }

                    {
                        Name = "calc-minmax: set-normdose"
                        Guard = (fun _ -> true)
                        Run = calcNormDoseStep
                    }
            ]
            |> runPipeline ord

        | CalcValues ord ->
            let guard (os: OrderState) =
                ord.Orderable.Components |> List.length <= 2
                && os.DoseIsSolved |> not
                && os.OrderIsSolved |> not

            let hasTimeNotContinuous =
                ord.Schedule |> Schedule.hasTime
                && ord.Schedule |> Schedule.isContinuous |> not

            [
                {
                    // Phase 1: expand quantities only (skip rate for timed orders)
                    Name = "calc-values: calc-qty-values"
                    Guard = guard
                    Run = calcValuesStep false hasTimeNotContinuous
                }
                if hasTimeNotContinuous then
                    {
                        // Phase 2: now expand rate too (quantity is already constrained)
                        Name = "calc-values: calc-rate-values"
                        Guard = guard
                        Run = calcValuesStep false false
                    }
            ]
            |> runPipeline ord

        | SolveOrder ord ->
            [
                {
                    Name = "solve-order: process-cleared"
                    Guard = (fun os -> os.DoseIsSolved && os.IsCleared)
                    Run = processClearedStep
                }
                {
                    Name = "solve-order: ensure-values-1"
                    Guard = (_.HasValues >> not)
                    Run = calcValuesStep (ord.Orderable.Components |> List.length <= 2) false
                }
                {
                    Name = "solve-order: final-solve"
                    Guard = (_.OrderIsSolved >> not)
                    Run = solveStep
                }
                {
                    Name = "solve-order: pick-cmp-qty"
                    Guard = (fun _ -> true)
                    Run = pickNearestHigherElseLowerComponentQuantityStep
                }
            ]
            |> runPipeline ord

        | ReCalcValues ord ->
            let useMax = ord.Orderable.Components |> List.length <= 2

            let hasTimeNotContinuous =
                ord.Schedule |> Schedule.hasTime
                && ord.Schedule |> Schedule.isContinuous |> not

            [
                {
                    Name = "recalc-values: apply-calculated-constraints"
                    Guard = (fun _ -> true)
                    Run = applyCalculatedConstraints >> Ok
                }
                {
                    Name = "recalc-values: calc-qty-values"
                    Guard = (fun _ -> true)
                    Run = reCalcValuesStep useMax hasTimeNotContinuous
                }
                if hasTimeNotContinuous then
                    {
                        Name = "recalc-values: calc-rate-values"
                        Guard = (fun _ -> true)
                        Run = reCalcValuesStep useMax false
                    }
            ]
            |> runPipeline ord

        | IncreaseIncrements ord ->
            [
                {
                    Name = "increase-increment: increase-increment"
                    Guard = (fun _ -> true)
                    Run = increaseIncrementStep
                }
            ]
            |> runPipeline ord

        | ChangeProperty(ord, _) ->
            // Not used in this test; delegate to original
            Informedica.GenOrder.Lib.OrderProcessor.processPipeline logger cmd


// === Helper functions ===

module Helpers =

    let print sl = sl |> List.iter (printfn "%s")


    let inline printOrderTable order =
        order
        |> Result.iter (Informedica.GenOrder.Lib.Order.printTable ConsoleTables.Format.Minimal)

        order


    /// Run a medication through a series of pipeline commands
    /// using the STAGED pipeline
    let run logger med cmds =
        let logger, usePrintTable =
            logger |> Option.defaultValue OrderLogging.noOp, logger.IsNone

        let rec loop cmds ord =
            ord
            |> Result.iter (Informedica.GenOrder.Lib.OrderProcessor.classify >> printfn "%A")

            match cmds with
            | [] ->
                ord
                |> fun ord ->
                    if usePrintTable then ord |> printOrderTable else ord
            | cmd :: rest ->
                match ord with
                | Error(_, msgs) -> failwith $"Errors occured: {msgs}"
                | Ok ord ->
                    if usePrintTable then
                        ord |> Ok |> printOrderTable |> ignore

                    // Use our staged pipeline instead of the original
                    ord |> cmd |> OrderProcessor.processPipelineStaged logger |> loop rest

        med
        |> Informedica.GenOrder.Lib.Medication.toOrderDto
        |> Informedica.GenOrder.Lib.Order.Dto.fromDto
        |> function
            | Error msg -> failwith $"{msg}"
            | Ok ord ->
                ord
                |> Ok
                |> fun ord ->
                    if usePrintTable then ord |> printOrderTable else ord
                |> loop cmds


// === Test Scenarios ===

module TestMedications =

    /// The failing kaliumchloride OnceTimed scenario
    /// from the log file (746839 values overflow)
    let kaliumchlorideOnceTimed =
        """
Id: c829b3ef-0dbe-4ed4-ac36-1260414d39b5
Name: kaliumchloride
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: OnceTimedOrder
Adjust: 31 kg
Frequencies:
Time: 60 min - 120 min
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: kaliumchloride
	Form: concentraat voor oplossing voor infusie
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: kalium
		Quantities:
		Concentrations: 1 mmol/ml
		Dose:
		Solution:

		Name: chloor
		Quantities:
		Concentrations: 1 mmol/ml
		Dose:
		Solution:

		Name: kaliumchloride
		Quantities:
		Concentrations: 1 mmol/ml
		Dose: kaliumchloride, [dun] mmol, [qty-adj] 0.5 mmol/kg/dosis
		Solution:  [conc] 0.5 mmol/ml

	Name: gluc 5%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: glucose
		Quantities:
		Concentrations: 0.05 g/ml
		Dose:
		Solution:

		Name: energie
		Quantities:
		Concentrations: 0.2 kCal/ml
		Dose:
		Solution:

		Name: koolhydraat
		Quantities:
		Concentrations: 0.05 g/ml
		Dose:
		Solution:
"""


    /// Existing paracetamol OnceTimed scenario (should still work)
    let paracetamolOnceTimed =
        """
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


    /// Existing paracetamol Once scenario (should be unaffected)
    let paracetamolOnce =
        """
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


// === Run Tests ===


printfn "\n\n=== TEST 1: Kaliumchloride OnceTimed (the failing scenario) ==="
printfn "This should now succeed without ValueSetOverflow\n"

let kaliumResult =
    TestMedications.kaliumchlorideOnceTimed
    |> Medication.fromString
    |> function
        | Error errs -> failwith $"Failed to parse medication: {errs}"
        | Ok med ->
            [ CalcMinMax ]
            |> Helpers.run (Some fileLogger) med

match kaliumResult with
| Error(ord, msgs) ->
    printfn "\n=== RESULT: CalcMinMax completed with errors ==="
    msgs |> List.iter (printfn "  Error: %A")
    ord |> Informedica.GenOrder.Lib.Order.printTable ConsoleTables.Format.MarkDown
| Ok ord ->
    printfn "\n=== RESULT: CalcMinMax succeeded ==="
    ord |> Informedica.GenOrder.Lib.Order.printTable ConsoleTables.Format.MarkDown


printfn "\n\n=== TEST 2: Paracetamol OnceTimed (existing scenario, regression check) ==="

let pcmTimedResult =
    TestMedications.paracetamolOnceTimed
    |> Medication.fromString
    |> function
        | Error errs -> failwith $"Failed to parse medication: {errs}"
        | Ok med ->
            [ CalcMinMax ]
            |> Helpers.run (Some fileLogger) med

match pcmTimedResult with
| Error(ord, msgs) ->
    printfn "\n=== RESULT: CalcMinMax completed with errors ==="
    msgs |> List.iter (printfn "  Error: %A")
| Ok ord ->
    printfn "\n=== RESULT: CalcMinMax succeeded ==="
    ord |> Informedica.GenOrder.Lib.Order.printTable ConsoleTables.Format.MarkDown


printfn "\n\n=== TEST 3: Paracetamol Once (non-timed order, should be unaffected) ==="

let pcmOnceResult =
    TestMedications.paracetamolOnce
    |> Medication.fromString
    |> function
        | Error errs -> failwith $"Failed to parse medication: {errs}"
        | Ok med ->
            [ CalcMinMax ]
            |> Helpers.run (Some fileLogger) med

match pcmOnceResult with
| Error(ord, msgs) ->
    printfn "\n=== RESULT: CalcMinMax completed with errors ==="
    msgs |> List.iter (printfn "  Error: %A")
| Ok ord ->
    printfn "\n=== RESULT: CalcMinMax succeeded ==="
    ord |> Informedica.GenOrder.Lib.Order.printTable ConsoleTables.Format.MarkDown


printfn "\n\n=== TEST 4: Kaliumchloride OnceTimed CalcMinMax then CalcValues (stepwise) ==="
printfn "This tests the two-phase expansion in CalcValues\n"

let kaliumFullResult =
    TestMedications.kaliumchlorideOnceTimed
    |> Medication.fromString
    |> function
        | Error errs -> failwith $"Failed to parse medication: {errs}"
        | Ok med ->
            // First run CalcMinMax (may error on set-normdose, that's expected)
            let calcMinMaxResult =
                [ CalcMinMax ]
                |> Helpers.run (Some fileLogger) med

            // Then run CalcValues on whatever order we have
            match calcMinMaxResult with
            | Error(ord, _) ->
                printfn "CalcMinMax had errors (expected for this scenario), continuing with CalcValues..."
                ord
                |> CalcValues
                |> OrderProcessor.processPipelineStaged fileLogger
            | Ok ord ->
                ord
                |> CalcValues
                |> OrderProcessor.processPipelineStaged fileLogger

match kaliumFullResult with
| Error(ord, msgs) ->
    printfn "\n=== RESULT: CalcValues completed with errors ==="
    msgs |> List.iter (printfn "  Error: %A")
    ord |> Informedica.GenOrder.Lib.Order.printTable ConsoleTables.Format.MarkDown
| Ok ord ->
    printfn "\n=== RESULT: CalcValues succeeded ==="
    ord |> Informedica.GenOrder.Lib.Order.printTable ConsoleTables.Format.MarkDown


printfn "\n\n=== ALL TESTS COMPLETED ==="


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


/// Parser module to parse Medication text back to Medication record
module Parser =

    open System


    /// Parse a line into (indentLevel, key, value)
    let private parseLine (line: string) =
        let indent = line |> Seq.takeWhile ((=) '\t') |> Seq.length
        let content = line.TrimStart('\t')
        match content.IndexOf(':') with
        | -1 -> None
        | i ->
            let key = content.[..i-1].Trim()
            let value = content.[i+1..].Trim()
            Some (indent, key, value)


    /// Parse OrderType from string
    let private parseOrderType (s: string) =
        match s with
        | "AnyOrder" -> Ok AnyOrder
        | "ProcessOrder" -> Ok ProcessOrder
        | "OnceOrder" -> Ok OnceOrder
        | "OnceTimedOrder" -> Ok OnceTimedOrder
        | "ContinuousOrder" -> Ok ContinuousOrder
        | "DiscontinuousOrder" -> Ok DiscontinuousOrder
        | "TimedOrder" -> Ok TimedOrder
        | _ -> Error $"Unknown OrderType: {s}"


    /// Parse BigRational option from string (e.g., "2" or "1,5")
    let private parseBigRationalOpt (s: string) =
        if s |> String.IsNullOrWhiteSpace then None
        else
            // Handle Dutch decimal format (comma)
            s.Replace(",", ".")
            |> Double.tryParse
            |> Option.bind BigRational.fromFloat


    /// Parse Dutch decimal value (comma as decimal separator, space as thousands separator)
    let private parseDutchDecimal (s: string) : BigRational option =
        let cleaned =
            s.Trim()
             .Replace(" ", "")  // Remove space thousands separator
             .Replace(",", ".") // Convert Dutch decimal to standard
        cleaned
        |> Double.tryParse
        |> Option.bind BigRational.fromFloat


    /// Parse ValueUnit from Dutch format string
    /// Handles: "3;4 x/dag", "1 000 mg", "0,5 mL", "1 stuk"
    let private parseValueUnit (s: string) : Result<ValueUnit, string> =
        if s |> String.IsNullOrWhiteSpace then Error "Empty ValueUnit string"
        else
            // Try the existing FParsec parser first
            match s |> ValueUnit.fromString with
            | FParsec.CharParsers.Success (vu, _, _) -> Ok vu
            | FParsec.CharParsers.Failure _ ->
                // Fall back to manual parsing for Dutch format
                // Split into values and unit parts
                // Format: "value1;value2 unit" or "value unit"
                let parts = s.Trim().Split(' ') |> Array.toList

                // Find where values end and unit begins
                // Values are numeric (may contain semicolons, commas, spaces)
                let rec splitValuesUnit (acc: string list) (remaining: string list) =
                    match remaining with
                    | [] -> (acc |> List.rev |> String.concat " ", "")
                    | [last] ->
                        // Check if last part is numeric
                        let test = last.Replace(";", "").Replace(",", ".").Replace(" ", "")
                        if test |> Double.tryParse |> Option.isSome then
                            ((last :: acc) |> List.rev |> String.concat " ", "")
                        else
                            (acc |> List.rev |> String.concat " ", last)
                    | head :: tail ->
                        // Check if this part looks like a value (contains digits)
                        if head |> Seq.exists Char.IsDigit then
                            splitValuesUnit (head :: acc) tail
                        else
                            // This and remaining parts are the unit
                            let unitStr = (head :: tail) |> String.concat " "
                            (acc |> List.rev |> String.concat " ", unitStr)

                let valuesPart, unitPart = splitValuesUnit [] parts

                if unitPart |> String.IsNullOrWhiteSpace then
                    Error $"Cannot parse unit from '{s}'"
                else
                    // Preprocess unit: convert "x" to "keer" for Count unit
                    let normalizedUnit =
                        if unitPart = "x" then "keer"
                        else
                            unitPart
                                .Replace("x/", "keer/")
                                .Replace(" x", " keer")

                    // Parse the unit
                    match normalizedUnit |> Units.fromString with
                    | None -> Error $"Unknown unit '{unitPart}' in '{s}'"
                    | Some unit ->
                        // Parse values (semicolon-separated)
                        let valueStrs =
                            valuesPart.Split(';')
                            |> Array.map (fun v -> v.Trim())
                            |> Array.filter (String.IsNullOrWhiteSpace >> not)

                        let values =
                            valueStrs
                            |> Array.choose parseDutchDecimal

                        if values.Length = 0 then
                            Error $"Cannot parse values from '{valuesPart}' in '{s}'"
                        elif values.Length <> valueStrs.Length then
                            Error $"Some values could not be parsed in '{s}'"
                        else
                            Ok (values |> ValueUnit.withUnit unit)


    /// Parse ValueUnit option (returns Ok None for empty string)
    let private parseValueUnitOpt (s: string) : Result<ValueUnit option, string> =
        if s |> String.IsNullOrWhiteSpace then Ok None
        else
            parseValueUnit s |> Result.map Some


    /// Parse MinMax from formatted string
    /// Handles: "" (empty), "10 mg" (exact), "10 - 20 mg" (range),
    ///          "min 10 mg" (min only), "max 10 mg" (max only)
    let private parseMinMax (s: string) : Result<MinMax, string> =
        if s |> String.IsNullOrWhiteSpace then Ok MinMax.empty
        else
            let s = s.Trim()

            // Check for "min X" pattern (min only)
            if s.StartsWith("min ") then
                let rest = s.[4..].Trim()
                parseValueUnit rest
                |> Result.map (fun vu ->
                    { MinMax.empty with Min = vu |> Limit.inclusive |> Some }
                )

            // Check for "max X" pattern (max only)
            elif s.StartsWith("max ") then
                let rest = s.[4..].Trim()
                parseValueUnit rest
                |> Result.map (fun vu ->
                    { MinMax.empty with Max = vu |> Limit.inclusive |> Some }
                )

            // Check for "X - Y" pattern (range)
            elif s.Contains(" - ") then
                let parts = s.Split([| " - " |], StringSplitOptions.None)
                if parts.Length <> 2 then Error $"Invalid MinMax range format: {s}"
                else
                    // The min part has no unit, max part has the unit
                    let minPart = parts.[0].Trim()
                    let maxPart = parts.[1].Trim()

                    // Parse the max part first to get the unit
                    parseValueUnit maxPart
                    |> Result.bind (fun maxVu ->
                        let unit = maxVu |> ValueUnit.getUnit
                        // Try to parse the min value and apply the same unit
                        let minValue =
                            minPart.Replace(",", ".").Replace(" ", "")
                            |> Double.tryParse
                            |> Option.bind BigRational.fromFloat

                        match minValue with
                        | Some minV ->
                            let minVu = minV |> ValueUnit.singleWithUnit unit
                            Ok (MinMax.createInclIncl minVu maxVu)
                        | None ->
                            Error $"Cannot parse min value: {minPart}"
                    )

            // Otherwise it's an exact value
            else
                parseValueUnit s
                |> Result.map MinMax.createExact


    /// Parse MinMax that may have /dosis suffix stripped
    let private parseMinMaxWithSuffix (s: string) (suffix: string) : Result<MinMax, string> =
        let s = s.Replace(suffix, "").Trim()
        parseMinMax s


    /// Parse DoseLimit from comma-separated constraint string
    /// Format: "targetName, constraint1, constraint2, ..."
    /// e.g., "paracetamol, 10 - 20 mg/kg/dosis"
    let private parseDoseLimitOpt (s: string) : Result<DoseLimit option, string> =
        if s |> String.IsNullOrWhiteSpace then Ok None
        else
            // Split by comma, first element may be target name
            let parts =
                s.Split(',')
                |> Array.map (fun p -> p.Trim())
                |> Array.filter (String.IsNullOrWhiteSpace >> not)
                |> Array.toList

            // Helper to check if a string looks like a value/constraint (contains digits)
            let looksLikeConstraint (p: string) =
                p |> Seq.exists Char.IsDigit

            match parts with
            | [] -> Ok None
            | parts ->
                // Determine target: first part is target if it doesn't look like a constraint
                // (doesn't contain digits, or all remaining parts contain digits)
                let target, constraints =
                    match parts with
                    | first :: rest when
                        not (looksLikeConstraint first) &&
                        (rest.IsEmpty || rest |> List.forall looksLikeConstraint) ->
                        SubstanceLimitTarget first, rest
                    | _ ->
                        NoLimitTarget, parts

                // Parse constraints into appropriate MinMax fields
                let mutable dl = { DoseLimit.limit with DoseLimitTarget = target }
                let mutable errors = []

                for constr in constraints do
                    let c = constr.Trim()
                    // Determine which field based on unit patterns
                    if c.Contains("/dosis") then
                        // Could be Quantity or QuantityAdjust
                        // If it contains /kg or /m2, it's QuantityAdjust
                        if c.Contains("/kg") || c.Contains("/m2") then
                            match parseMinMaxWithSuffix c "/dosis" with
                            | Ok mm -> dl <- { dl with QuantityAdjust = mm }
                            | Error e -> errors <- e :: errors
                        else
                            match parseMinMaxWithSuffix c "/dosis" with
                            | Ok mm -> dl <- { dl with Quantity = mm }
                            | Error e -> errors <- e :: errors
                    elif c.Contains("/uur") || c.Contains("/hour") || c.Contains("/hr") then
                        // Rate constraints
                        if c.Contains("/kg") || c.Contains("/m2") then
                            match parseMinMax c with
                            | Ok mm -> dl <- { dl with RateAdjust = mm }
                            | Error e -> errors <- e :: errors
                        else
                            match parseMinMax c with
                            | Ok mm -> dl <- { dl with Rate = mm }
                            | Error e -> errors <- e :: errors
                    elif c.Contains("/dag") || c.Contains("/day") then
                        // PerTime or PerTimeAdjust
                        if c.Contains("/kg") || c.Contains("/m2") then
                            match parseMinMax c with
                            | Ok mm -> dl <- { dl with PerTimeAdjust = mm }
                            | Error e -> errors <- e :: errors
                        else
                            match parseMinMax c with
                            | Ok mm -> dl <- { dl with PerTime = mm }
                            | Error e -> errors <- e :: errors
                    else
                        // Default: try as Quantity or QuantityAdjust
                        if c.Contains("/kg") || c.Contains("/m2") then
                            match parseMinMax c with
                            | Ok mm -> dl <- { dl with QuantityAdjust = mm }
                            | Error e -> errors <- e :: errors
                        else
                            match parseMinMax c with
                            | Ok mm -> dl <- { dl with Quantity = mm }
                            | Error e -> errors <- e :: errors

                if errors.IsEmpty then Ok (Some dl)
                else Error (errors |> String.concat "; ")


    /// Parse SolutionLimit from formatted string
    /// Note: Dutch decimal format uses comma as separator, so we need careful splitting
    let private parseSolutionLimitOpt (s: string) : Result<SolutionLimit option, string> =
        if s |> String.IsNullOrWhiteSpace then Ok None
        else
            // SolutionLimit.toString outputs: Quantity, QuantityAdj, Concentration
            // as comma-separated MinMax strings (with ", " as separator)
            // BUT Dutch decimals also use comma (e.g., "0,5 mg/mL")
            // Strategy: split on ", " (comma-space) but only when followed by a letter or digit
            // that looks like a new value (min/max pattern or number)
            let splitPattern = System.Text.RegularExpressions.Regex(", (?=\d|min |max )")
            let parts =
                splitPattern.Split(s)
                |> Array.map (fun p -> p.Trim())
                |> Array.filter (String.IsNullOrWhiteSpace >> not)
                |> Array.toList

            let mutable sl = SolutionLimit.limit
            let mutable errors = []

            for part in parts do
                let p = part.Trim()
                // Determine which field based on unit patterns
                if p.Contains("/kg") || p.Contains("/m2") then
                    match parseMinMax p with
                    | Ok mm -> sl <- { sl with QuantityAdj = mm }
                    | Error e -> errors <- e :: errors
                elif p.Contains("/mL") || p.Contains("/ml") then
                    match parseMinMax p with
                    | Ok mm -> sl <- { sl with Concentration = mm }
                    | Error e -> errors <- e :: errors
                else
                    match parseMinMax p with
                    | Ok mm -> sl <- { sl with Quantity = mm }
                    | Error e -> errors <- e :: errors

            if errors.IsEmpty then
                if sl = SolutionLimit.limit then Ok None
                else Ok (Some sl)
            else Error (errors |> String.concat "; ")


    /// Parse SubstanceItem from field map
    let private parseSubstanceItem (fields: Map<string, string>) : Result<SubstanceItem, string list> =
        let mutable errors = []

        let name =
            fields
            |> Map.tryFind "Name"
            |> Option.defaultValue ""

        let concentrations =
            match fields |> Map.tryFind "Concentrations" with
            | None | Some "" -> None
            | Some s ->
                match parseValueUnitOpt s with
                | Ok vu -> vu
                | Error e ->
                    errors <- e :: errors
                    None

        let dose =
            match fields |> Map.tryFind "Dose" with
            | None | Some "" -> None
            | Some s ->
                match parseDoseLimitOpt s with
                | Ok dl -> dl
                | Error e ->
                    errors <- e :: errors
                    None

        let solution =
            match fields |> Map.tryFind "Solution" with
            | None | Some "" -> None
            | Some s ->
                match parseSolutionLimitOpt s with
                | Ok sl -> sl
                | Error e ->
                    errors <- e :: errors
                    None

        if errors.IsEmpty then
            Ok {
                Name = name
                Concentrations = concentrations
                Dose = dose
                Solution = solution
            }
        else Error errors


    /// Parse ProductComponent from field map and nested substances
    let private parseProductComponent (fields: Map<string, string>) (substances: SubstanceItem list) : Result<ProductComponent, string list> =
        let mutable errors = []

        let name =
            fields
            |> Map.tryFind "Name"
            |> Option.defaultValue ""

        let form =
            fields
            |> Map.tryFind "Form"
            |> Option.defaultValue ""

        let quantities =
            match fields |> Map.tryFind "Quantities" with
            | None | Some "" -> None
            | Some s ->
                match parseValueUnitOpt s with
                | Ok vu -> vu
                | Error e ->
                    errors <- e :: errors
                    None

        let divisible =
            fields
            |> Map.tryFind "Divisible"
            |> Option.bind parseBigRationalOpt

        let dose =
            match fields |> Map.tryFind "Dose" with
            | None | Some "" -> None
            | Some s ->
                match parseDoseLimitOpt s with
                | Ok dl -> dl
                | Error e ->
                    errors <- e :: errors
                    None

        let solution =
            match fields |> Map.tryFind "Solution" with
            | None | Some "" -> None
            | Some s ->
                match parseSolutionLimitOpt s with
                | Ok sl -> sl
                | Error e ->
                    errors <- e :: errors
                    None

        if errors.IsEmpty then
            Ok {
                Name = name
                Form = form
                Quantities = quantities
                Divisible = divisible
                Dose = dose
                Solution = solution
                Substances = substances
            }
        else Error errors


    /// Main parsing function
    let fromString (s: string) : Result<Medication, string list> =
        let lines =
            s.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun line -> parseLine line)
            |> Array.choose id
            |> Array.toList

        // Separate top-level (indent 0), component (indent 1), substance (indent 2) fields
        let mutable topFields = Map.empty
        let mutable components = []
        let mutable currentComponent = None
        let mutable currentComponentFields = Map.empty
        let mutable currentSubstances = []
        let mutable currentSubstanceFields = Map.empty

        let mutable parseErrors : string list = []

        let finishSubstance () =
            if not currentSubstanceFields.IsEmpty then
                match parseSubstanceItem currentSubstanceFields with
                | Ok si -> currentSubstances <- si :: currentSubstances
                | Error errs ->
                    for e in errs do parseErrors <- e :: parseErrors
                currentSubstanceFields <- Map.empty

        let finishComponent () =
            finishSubstance ()
            if not currentComponentFields.IsEmpty then
                match parseProductComponent currentComponentFields (currentSubstances |> List.rev) with
                | Ok pc -> components <- pc :: components
                | Error errs ->
                    for e in errs do parseErrors <- e :: parseErrors
                currentComponentFields <- Map.empty
                currentSubstances <- []

        for (indent, key, value) in lines do
            match indent with
            | 0 ->
                if key = "Components" then
                    () // Marker, no value
                else
                    topFields <- topFields |> Map.add key value
            | 1 ->
                if key = "Name" && currentComponentFields.ContainsKey "Name" then
                    // Starting a new component
                    finishComponent ()
                if key = "Substances" then
                    () // Marker for substances section
                else
                    currentComponentFields <- currentComponentFields |> Map.add key value
            | 2 ->
                if key = "Name" && currentSubstanceFields.ContainsKey "Name" then
                    // Starting a new substance
                    finishSubstance ()
                currentSubstanceFields <- currentSubstanceFields |> Map.add key value
            | _ -> ()

        // Finish the last component/substance
        finishComponent ()

        // Parse top-level fields
        let mutable errors = []

        let id =
            topFields
            |> Map.tryFind "Id"
            |> Option.defaultValue ""

        let name =
            topFields
            |> Map.tryFind "Name"
            |> Option.defaultValue ""

        let route =
            topFields
            |> Map.tryFind "Route"
            |> Option.defaultValue ""

        let orderType =
            match topFields |> Map.tryFind "OrderType" with
            | None -> AnyOrder
            | Some s ->
                match parseOrderType s with
                | Ok ot -> ot
                | Error e ->
                    errors <- e :: errors
                    AnyOrder

        let quantity =
            match topFields |> Map.tryFind "Quantity" with
            | None | Some "" -> MinMax.empty
            | Some s ->
                match parseMinMax s with
                | Ok mm -> mm
                | Error e ->
                    errors <- e :: errors
                    MinMax.empty

        let quantities =
            match topFields |> Map.tryFind "Quantities" with
            | None | Some "" -> None
            | Some s ->
                match parseValueUnitOpt s with
                | Ok vu -> vu
                | Error e ->
                    errors <- e :: errors
                    None

        let adjust =
            match topFields |> Map.tryFind "Adjust" with
            | None | Some "" -> None
            | Some s ->
                match parseValueUnitOpt s with
                | Ok vu -> vu
                | Error e ->
                    errors <- e :: errors
                    None

        let frequencies =
            match topFields |> Map.tryFind "Frequencies" with
            | None | Some "" -> None
            | Some s ->
                match parseValueUnitOpt s with
                | Ok vu -> vu
                | Error e ->
                    errors <- e :: errors
                    None

        let time =
            match topFields |> Map.tryFind "Time" with
            | None | Some "" -> MinMax.empty
            | Some s ->
                match parseMinMax s with
                | Ok mm -> mm
                | Error e ->
                    errors <- e :: errors
                    MinMax.empty

        let dose =
            match topFields |> Map.tryFind "Dose" with
            | None | Some "" -> None
            | Some s ->
                match parseDoseLimitOpt s with
                | Ok dl -> dl
                | Error e ->
                    errors <- e :: errors
                    None

        let div =
            topFields
            |> Map.tryFind "Div"
            |> Option.bind parseBigRationalOpt

        let doseCount =
            match topFields |> Map.tryFind "DoseCount" with
            | None | Some "" -> MinMax.empty
            | Some s ->
                match parseMinMax s with
                | Ok mm -> mm
                | Error e ->
                    errors <- e :: errors
                    MinMax.empty

        // Combine all errors
        let allErrors = errors @ parseErrors

        if allErrors.IsEmpty then
            Ok {
                Id = id
                Name = name
                Components = components |> List.rev
                Quantity = quantity
                Quantities = quantities
                Route = route
                OrderType = orderType
                Frequencies = frequencies
                Time = time
                Dose = dose
                Div = div
                DoseCount = doseCount
                Adjust = adjust
            }
        else Error allErrors


/// Shadow the Medication module to add fromString while re-exporting existing functions
module Medication =

    open Informedica.GenOrder.Lib.Medication

    let fromString = Parser.fromString

    // now you can use the original medication module functions
    // for example
    let testTostring med = toString med


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
                |> Informedica.GenOrder.Lib.Medication.toString
                |> String.concat "\n"
                |> normalizeWords

            let expected =
                Scenarios.pcmSuppText
                |> normalizeWords

            actual
            |> Expect.equal "should be" expected
        }

        // Roundtrip tests
        test "pcmSupp roundtrip - basic fields" {
            let original = Scenarios.pcmSupp
            let text = original |> Informedica.GenOrder.Lib.Medication.toString |> String.concat "\n"
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
            let text = original |> Informedica.GenOrder.Lib.Medication.toString |> String.concat "\n"
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

        test "fullMedication roundtrip - all fields" {
            let original = Scenarios.fullMedication
            let text = original |> Informedica.GenOrder.Lib.Medication.toString |> String.concat "\n"
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
                errs |> List.exists (fun e -> e.Contains("Unknown OrderType"))
                |> Expect.isTrue "should contain OrderType error"
            | Ok _ ->
                failwith "Expected error for invalid OrderType"
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

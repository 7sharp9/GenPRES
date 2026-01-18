
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


/// Shadowed DoseLimit module with explicit field labels in toString output
module DoseLimit =

    open System


    /// Field labels for deterministic parsing
    module FieldLabels =
        let [<Literal>] Quantity = "[qty]"
        let [<Literal>] QuantityAdjust = "[qty-adj]"
        let [<Literal>] PerTime = "[per-time]"
        let [<Literal>] PerTimeAdjust = "[per-time-adj]"
        let [<Literal>] Rate = "[rate]"
        let [<Literal>] RateAdjust = "[rate-adj]"
        let [<Literal>] NormQuantityAdjust = "[norm-qty-adj]"
        let [<Literal>] NormPerTimeAdjust = "[norm-per-time-adj]"


    let printMinMaxDose perDose (minMax : Informedica.GenCore.Lib.Ranges.MinMax) =
        if minMax = MinMax.empty then ""
        else
            minMax
            |> MinMax.toString
                "min "
                "min "
                "max "
                "max "
            |> fun s ->
                $"{s}{perDose}"


    let printNormDose perDose vu =
        match vu with
        | None    -> ""
        | Some vu ->
            $"{vu |> Utils.ValueUnit.toString 3}{perDose}"


    /// Print a MinMax value with an explicit field label prefix
    let printMinMaxDoseWithLabel label perDose (minMax : Informedica.GenCore.Lib.Ranges.MinMax) =
        if minMax = MinMax.empty then ""
        else
            let value = printMinMaxDose perDose minMax
            $"{label} {value}"


    /// Print a norm dose value with an explicit field label prefix
    let printNormDoseWithLabel label perDose vu =
        match vu with
        | None -> ""
        | Some _ ->
            let value = printNormDose perDose vu
            $"{label} {value}"


    /// Convert a DoseLimit to a string list with explicit field labels
    let toString (dl: DoseLimit) =
        [
            let perDose = "/dosis"
            let emptyS = ""
            [
                $"{dl.DoseLimitTarget |> LimitTarget.toString}"

                // Rate fields
                dl.Rate |> printMinMaxDoseWithLabel FieldLabels.Rate emptyS
                dl.RateAdjust |> printMinMaxDoseWithLabel FieldLabels.RateAdjust emptyS

                // PerTime fields
                let normPerTimeAdj = dl.NormPerTimeAdjust |> printNormDoseWithLabel FieldLabels.NormPerTimeAdjust emptyS
                let perTimeAdj = dl.PerTimeAdjust |> printMinMaxDoseWithLabel FieldLabels.PerTimeAdjust emptyS
                if normPerTimeAdj <> "" && perTimeAdj <> "" then
                    $"{normPerTimeAdj} {perTimeAdj}"
                elif normPerTimeAdj <> "" then normPerTimeAdj
                elif perTimeAdj <> "" then perTimeAdj
                else ""

                dl.PerTime |> printMinMaxDoseWithLabel FieldLabels.PerTime emptyS

                // Quantity fields
                let normQtyAdj = dl.NormQuantityAdjust |> printNormDoseWithLabel FieldLabels.NormQuantityAdjust perDose
                let qtyAdj = dl.QuantityAdjust |> printMinMaxDoseWithLabel FieldLabels.QuantityAdjust perDose
                if normQtyAdj <> "" && qtyAdj <> "" then
                    $"{normQtyAdj} {qtyAdj}"
                elif normQtyAdj <> "" then normQtyAdj
                elif qtyAdj <> "" then qtyAdj
                else ""

                dl.Quantity |> printMinMaxDoseWithLabel FieldLabels.Quantity perDose
            ]
            |> List.map String.trim
            |> List.filter (String.IsNullOrEmpty >> not)
            |> String.concat ", "
        ]


/// Unit validation helpers for deterministic field parsing
module UnitValidation =

    open Informedica.GenUnits.Lib

    /// Check if a unit is an adjust unit (Weight - kg, or BSA - m2)
    let rec private isAdjustUnitType (u: Unit) =
        match u with
        | Weight _ -> true
        | BSA _ -> true
        | CombiUnit (ul, _, ur) ->
            isAdjustUnitType ul || isAdjustUnitType ur
        | _ -> false


    /// Check if a unit is a time unit
    let rec private isTimeUnitType (u: Unit) =
        match u with
        | Time _ -> true
        | CombiUnit (ul, _, ur) ->
            isTimeUnitType ul || isTimeUnitType ur
        | _ -> false


    /// Check if a unit has an adjust component in denominator (kg or m2)
    /// by checking if the right side of a Per operation contains Weight or BSA
    let rec hasAdjustUnit (u: Unit) =
        match u with
        | CombiUnit (ul, OpPer, ur) ->
            // Check if denominator (right side) contains adjust unit
            isAdjustUnitType ur || hasAdjustUnit ul
        | CombiUnit (ul, _, ur) ->
            hasAdjustUnit ul || hasAdjustUnit ur
        | _ -> false


    /// Check if a unit has a time component in denominator
    /// by checking if the right side of a Per operation contains Time
    let rec hasTimeUnit (u: Unit) =
        match u with
        | CombiUnit (ul, OpPer, ur) ->
            // Check if denominator (right side) contains time unit
            isTimeUnitType ur || hasTimeUnit ul
        | CombiUnit (ul, _, ur) ->
            hasTimeUnit ul || hasTimeUnit ur
        | _ -> false


    /// Validate that unit matches Quantity field pattern: no adjust, no time
    let validateQuantityUnit (u: Unit) =
        if hasAdjustUnit u then Error "Quantity cannot have adjust unit"
        elif hasTimeUnit u then Error "Quantity cannot have time unit"
        else Ok ()


    /// Validate that unit matches QuantityAdjust field pattern: has adjust, no time
    let validateQuantityAdjustUnit (u: Unit) =
        if not (hasAdjustUnit u) then Error "QuantityAdjust must have adjust unit (kg/m2)"
        elif hasTimeUnit u then Error "QuantityAdjust cannot have time unit"
        else Ok ()


    /// Validate that unit matches PerTime field pattern: no adjust, has time
    let validatePerTimeUnit (u: Unit) =
        if hasAdjustUnit u then Error "PerTime cannot have adjust unit"
        elif not (hasTimeUnit u) then Error "PerTime must have time unit"
        else Ok ()


    /// Validate that unit matches PerTimeAdjust field pattern: has adjust and time
    let validatePerTimeAdjustUnit (u: Unit) =
        if not (hasAdjustUnit u) then Error "PerTimeAdjust must have adjust unit (kg/m2)"
        elif not (hasTimeUnit u) then Error "PerTimeAdjust must have time unit"
        else Ok ()


    /// Validate that unit matches Rate field pattern: no adjust, has time
    let validateRateUnit (u: Unit) =
        if hasAdjustUnit u then Error "Rate cannot have adjust unit"
        elif not (hasTimeUnit u) then Error "Rate must have time unit"
        else Ok ()


    /// Validate that unit matches RateAdjust field pattern: has adjust and time
    let validateRateAdjustUnit (u: Unit) =
        if not (hasAdjustUnit u) then Error "RateAdjust must have adjust unit (kg/m2)"
        elif not (hasTimeUnit u) then Error "RateAdjust must have time unit"
        else Ok ()


/// Parser module to parse Medication text back to Medication record
module Parser =

    open System
    open UnitValidation


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
    /// Handles: "3;4 x/dag", "1, 10 mg/mL", "1 000 mg", "0,5 mL", "1 stuk"
    /// Note: comma-space ", " is value separator, comma without space is Dutch decimal
    let private parseValueUnit (s: string) : Result<ValueUnit, string> =
        if s |> String.IsNullOrWhiteSpace then Error "Empty ValueUnit string"
        else
            // Try the existing FParsec parser first
            match s |> ValueUnit.fromString with
            | FParsec.CharParsers.Success (vu, _, _) -> Ok vu
            | FParsec.CharParsers.Failure _ ->
                // Fall back to manual parsing for Dutch format
                // The format is: "value1, value2 unit" or "value1;value2 unit" or "value unit"
                // Important: ", " (comma-space) separates values, ",digit" is Dutch decimal
                let s = s.Trim()

                // Find the last space followed by non-numeric content (the unit)
                // Strategy: find all space positions, try to parse from each as potential unit boundary
                let rec findUnitBoundary (idx: int) =
                    let lastSpaceIdx = s.LastIndexOf(' ', idx - 1)
                    if lastSpaceIdx < 0 then
                        (s, "")  // No unit found - everything is values
                    else
                        let potentialUnit = s.Substring(lastSpaceIdx + 1)
                        let valPart = s.Substring(0, lastSpaceIdx)
                        // Check if what's left is purely numeric-ish (values)
                        let valueTest = valPart.Replace(",", "").Replace(";", "").Replace(" ", "")
                        if valueTest |> Seq.forall (fun c -> Char.IsDigit c || c = '.' || c = '-') then
                            (valPart, potentialUnit)
                        else
                            // The potential unit might be part of a multi-word unit
                            findUnitBoundary lastSpaceIdx

                let valuesPart, unitPart =
                    let lastSpaceIdx = s.LastIndexOf(' ')
                    if lastSpaceIdx < 0 then
                        (s, "")
                    else
                        let potentialUnit = s.Substring(lastSpaceIdx + 1)
                        let valPart = s.Substring(0, lastSpaceIdx)
                        (valPart.Trim(), potentialUnit.Trim())

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
                        // Parse values - try comma-space first (output format), then semicolon
                        // Important: ", " is value separator, ",digit" is decimal
                        let valueStrs =
                            if valuesPart.Contains(", ") then
                                // Comma-space separated (output format from Utils.ValueUnit.toString)
                                valuesPart.Split([| ", " |], StringSplitOptions.RemoveEmptyEntries)
                                |> Array.map (fun v -> v.Trim())
                            elif valuesPart.Contains(";") then
                                // Semicolon separated (alternative format)
                                valuesPart.Split(';')
                                |> Array.map (fun v -> v.Trim())
                            else
                                // Single value
                                [| valuesPart.Trim() |]
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


    /// Get the unit from a MinMax if available
    let private getMinMaxUnit (mm: MinMax) : Unit option =
        match mm.Min, mm.Max with
        | Some lim, _ ->
            lim |> Limit.getValueUnit |> ValueUnit.getUnit |> Some
        | _, Some lim ->
            lim |> Limit.getValueUnit |> ValueUnit.getUnit |> Some
        | None, None -> None


    /// Parse and validate a MinMax value for a specific field type
    let private parseMinMaxForField
        (validator: Unit -> Result<unit, string>)
        (s: string)
        (perDose: bool)
        : Result<MinMax, string> =
        let s = if perDose then s.Replace("/dosis", "").Trim() else s
        match parseMinMax s with
        | Error e -> Error e
        | Ok mm when mm = MinMax.empty -> Ok mm
        | Ok mm ->
            match getMinMaxUnit mm with
            | None -> Ok mm
            | Some unit ->
                match validator unit with
                | Error e -> Error e
                | Ok () -> Ok mm


    /// Parse a MinMax value without validation (for labeled fields where we trust the label)
    let private parseMinMaxNoValidation
        (s: string)
        (perDose: bool)
        : Result<MinMax, string> =
        let s = if perDose then s.Replace("/dosis", "").Trim() else s
        parseMinMax s


    /// Parse DoseLimit from comma-separated constraint string with explicit field labels
    /// Format: "targetName, [field-label] constraint1, [field-label] constraint2, ..."
    /// e.g., "paracetamol, [qty-adj] 10 - 20 mg/kg/dosis"
    /// Note: Cannot split naively by comma because Dutch decimals use comma (e.g., "5,4")
    let private parseDoseLimitOpt (s: string) : Result<DoseLimit option, string> =
        if s |> String.IsNullOrWhiteSpace then Ok None
        else
            // All known field labels
            let allLabels = [
                DoseLimit.FieldLabels.Quantity
                DoseLimit.FieldLabels.QuantityAdjust
                DoseLimit.FieldLabels.PerTime
                DoseLimit.FieldLabels.PerTimeAdjust
                DoseLimit.FieldLabels.Rate
                DoseLimit.FieldLabels.RateAdjust
                DoseLimit.FieldLabels.NormQuantityAdjust
                DoseLimit.FieldLabels.NormPerTimeAdjust
            ]

            // Find the position of the first field label to separate target from constraints
            let firstLabelPos =
                allLabels
                |> List.choose (fun label ->
                    let idx = s.IndexOf(label)
                    if idx >= 0 then Some idx else None)
                |> List.sort
                |> List.tryHead

            // Extract target name (everything before first label, trimmed of ", ")
            let target, constraintsStr =
                match firstLabelPos with
                | Some pos when pos > 0 ->
                    let targetStr = s.Substring(0, pos).Trim().TrimEnd(',').Trim()
                    let constraintsStr = s.Substring(pos)
                    if targetStr |> String.IsNullOrWhiteSpace then
                        NoLimitTarget, constraintsStr
                    else
                        SubstanceLimitTarget targetStr, constraintsStr
                | Some 0 ->
                    NoLimitTarget, s
                | None ->
                    // No labels found - try legacy parsing
                    // Split by ", " followed by a digit or min/max
                    let splitRegex = Text.RegularExpressions.Regex(", (?=\d|min |max |\[)")
                    let parts = splitRegex.Split(s) |> Array.map (fun p -> p.Trim()) |> Array.toList
                    match parts with
                    | [] -> NoLimitTarget, ""
                    | first :: rest when not (first |> Seq.exists Char.IsDigit) ->
                        SubstanceLimitTarget first, rest |> String.concat ", "
                    | _ ->
                        NoLimitTarget, s
                | _ -> $"{firstLabelPos} not valid" |> failwith

            // Split constraints by field labels using regex
            // Pattern matches: [label] value until next [label] or end
            let constraintRegex = Text.RegularExpressions.Regex(@"\[([^\]]+)\]\s*([^[]*)")
            let matches = constraintRegex.Matches(constraintsStr)

            // Parse constraints into appropriate MinMax fields
            let mutable dl = { DoseLimit.limit with DoseLimitTarget = target }
            let mutable errors = []

            // Field label parsers - when we have explicit labels, we trust them
            // and don't apply validation (validation is only for heuristic fallback)
            let fieldParsers : (string * (string -> Result<Informedica.GenCore.Lib.Ranges.MinMax, string>) * (DoseLimit -> Informedica.GenCore.Lib.Ranges.MinMax -> DoseLimit)) list = [
                DoseLimit.FieldLabels.Quantity,
                    (fun s -> parseMinMaxNoValidation s true),
                    (fun (dl: DoseLimit) mm -> { dl with Quantity = mm })

                DoseLimit.FieldLabels.QuantityAdjust,
                    (fun s -> parseMinMaxNoValidation s true),
                    (fun (dl: DoseLimit) mm -> { dl with QuantityAdjust = mm })

                DoseLimit.FieldLabels.PerTime,
                    (fun s -> parseMinMaxNoValidation s false),
                    (fun (dl: DoseLimit) mm -> { dl with PerTime = mm })

                DoseLimit.FieldLabels.PerTimeAdjust,
                    (fun s -> parseMinMaxNoValidation s false),
                    (fun (dl: DoseLimit) mm -> { dl with PerTimeAdjust = mm })

                DoseLimit.FieldLabels.Rate,
                    (fun s -> parseMinMaxNoValidation s false),
                    (fun (dl: DoseLimit) mm -> { dl with Rate = mm })

                DoseLimit.FieldLabels.RateAdjust,
                    (fun s -> parseMinMaxNoValidation s false),
                    (fun (dl: DoseLimit) mm -> { dl with RateAdjust = mm })
            ]

            // Process regex matches for labeled fields
            for m in matches do
                let labelContent = m.Groups.[1].Value  // e.g., "qty-adj"
                let valueStr = m.Groups.[2].Value.Trim().TrimEnd(',').Trim()
                let fullLabel = $"[{labelContent}]"

                let labelMatch =
                    fieldParsers
                    |> List.tryFind (fun (label, _, _) -> label = fullLabel)

                match labelMatch with
                | Some (label, parser, setter) ->
                    match parser valueStr with
                    | Ok mm -> dl <- setter dl mm
                    | Error e -> errors <- $"{label}: {e}" :: errors
                | None ->
                    errors <- $"Unknown field label: {fullLabel}" :: errors

            // If no labeled matches and we have constraintsStr, return error requiring labels
            if matches.Count = 0 && not (constraintsStr |> String.IsNullOrWhiteSpace) then
                errors <- "DoseLimit fields must use labels like [qty], [qty-adj], [per-time], etc. Unlabeled input is not supported." :: errors

            // Return result
            if errors.IsEmpty then Ok (Some dl)
            else Error (errors |> String.concat "; ")


    /// Parse SolutionLimit from formatted string using labeled fields
    /// Labels: [qty] for Quantity, [qty-adj] for QuantityAdj, [conc] for Concentration
    let private parseSolutionLimitOpt (s: string) : Result<SolutionLimit option, string> =
        if s |> String.IsNullOrWhiteSpace then Ok None
        else
            // Match labeled fields: [label] value
            let labeledFieldRegex = System.Text.RegularExpressions.Regex(@"\[([^\]]+)\]\s*([^[]*)")
            let matches = labeledFieldRegex.Matches(s)

            let mutable sl = SolutionLimit.limit
            let mutable errors = []

            for m in matches do
                let label = m.Groups.[1].Value.Trim().ToLowerInvariant()
                let valueStr = m.Groups.[2].Value.Trim()

                if not (valueStr |> String.IsNullOrWhiteSpace) then
                    match label with
                    | "qty" ->
                        match parseMinMax valueStr with
                        | Ok mm -> sl <- { sl with Quantity = mm }
                        | Error e -> errors <- $"[qty]: {e}" :: errors
                    | "qty-adj" ->
                        match parseMinMax valueStr with
                        | Ok mm -> sl <- { sl with QuantityAdj = mm }
                        | Error e -> errors <- $"[qty-adj]: {e}" :: errors
                    | "conc" ->
                        match parseMinMax valueStr with
                        | Ok mm -> sl <- { sl with Concentration = mm }
                        | Error e -> errors <- $"[conc]: {e}" :: errors
                    | _ ->
                        errors <- $"Unknown SolutionLimit label: [{label}]" :: errors

            // If no labeled matches and we have input, return error requiring labels
            if matches.Count = 0 then
                errors <- "SolutionLimit fields must use labels like [qty], [qty-adj], [conc]. Unlabeled input is not supported." :: errors

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

        for indent, key, value in lines do
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


/// Shadow the Medication module to add fromString and labeled toString
module Medication =

    open System
    open Informedica.GenOrder.Lib.Medication

    let fromString = Parser.fromString

    /// Helper to convert a DoseLimit option to string using new labeled format
    let private limitOptToString (dlOpt: DoseLimit option) =
        match dlOpt with
        | None -> ""
        | Some dl -> dl |> DoseLimit.toString |> String.concat ""


    /// Convert Medication to string list using labeled DoseLimit output
    let toString (med: Medication) : string list =
        let emptyStr = ""
        let optToStr f opt = opt |> Option.map f |> Option.defaultValue emptyStr
        /// Convert SolutionLimit to labeled string format
        /// Labels: [qty] for Quantity, [qty-adj] for QuantityAdj, [conc] for Concentration
        let slToStr (sl: SolutionLimit) =
            let minMaxStr mm =
                if mm = MinMax.empty then ""
                else mm |> MinMax.toString "min " "min " "max " "max "
            [
                let qty = sl.Quantity |> minMaxStr
                if not (String.IsNullOrWhiteSpace qty) then $"[qty] {qty}"
                let qtyAdj = sl.QuantityAdj |> minMaxStr
                if not (String.IsNullOrWhiteSpace qtyAdj) then $"[qty-adj] {qtyAdj}"
                let conc = sl.Concentration |> minMaxStr
                if not (String.IsNullOrWhiteSpace conc) then $"[conc] {conc}"
            ] |> String.concat " "
        [
            $"Id: {med.Id}"
            $"Name: {med.Name}"
            $"Quantity: {med.Quantity |> DoseLimit.printMinMaxDose emptyStr}"
            $"Quantities: {med.Quantities |> optToStr (Utils.ValueUnit.toString 3)}"
            $"Route: {med.Route}"
            $"OrderType: {med.OrderType}"
            $"Adjust: {med.Adjust |> optToStr (Utils.ValueUnit.toString 3)}"
            $"Frequencies: {med.Frequencies |> optToStr (Utils.ValueUnit.toString 3)}"
            $"Time: {med.Time |> DoseLimit.printMinMaxDose emptyStr}"
            $"Dose: {med.Dose |> limitOptToString}"
            $"Div: {med.Div |> optToStr BigRational.toStringNl}"
            $"DoseCount: {med.DoseCount |> DoseLimit.printMinMaxDose emptyStr}"
            "Components:"
            for cmp in med.Components do
                emptyStr
                $"\tName: {cmp.Name}"
                $"\tForm: {cmp.Form}"
                $"\tQuantities: {cmp.Quantities |> optToStr (Utils.ValueUnit.toString 3)}"
                $"\tDivisible: {cmp.Divisible |> optToStr BigRational.toStringNl}"
                $"\tDose: {cmp.Dose |> limitOptToString}"
                $"\tSolution: {cmp.Solution |> optToStr slToStr}"
                $"\tSubstances:"
                for sub in cmp.Substances do
                    emptyStr
                    $"\t\tName: {sub.Name}"
                    $"\t\tConcentrations: {sub.Concentrations |> optToStr (Utils.ValueUnit.toString 3)}"
                    $"\t\tDose: {sub.Dose |> limitOptToString}"
                    $"\t\tSolution: {sub.Solution |> optToStr slToStr}"
        ]


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
        |> Informedica.GenOrder.Lib.Medication.toOrderDto
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
                    |> fun c -> c.Substances
                    |> List.find (fun s -> s.Name = "amfotericine b liposomaal")

                let parsedSubstance =
                    med.Components
                    |> List.head
                    |> fun c -> c.Substances
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
                    |> fun c -> c.Substances
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
                    |> fun c -> c.Substances

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
                    let parsedCmp = med.Components.[i]
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
                errs |> List.exists (fun e -> e.Contains("Unknown OrderType"))
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


open Types

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

/// NLP dose rule extraction validation.
///
/// This script validates the structured DoseRuleExtracted output from DoseRuleExtraction.fsx
/// against the GenForm formulary.
///
/// Validation steps:
///   1. Generic name: must match a known generic in the formulary (case-insensitive)
///   2. Route: must match a known route mapping (Long or Short form)
///   3. AdjustUnit: must be "kg", "m2", or empty
///   4. DoseType: must be one of the five valid dose types
///   5. Unit structure: validates that compound units have the correct structure
///       - MinQtyAdj / MaxQtyAdj    : doseUnit / adjustUnit
///       - MinPerTime / MaxPerTime  : doseUnit / freqUnit
///       - MinPerTimeAdj / MaxPerTimeAdj : doseUnit / adjustUnit / freqUnit
///       - MinRate / MaxRate        : doseUnit / rateUnit
///       - MinRateAdj / MaxRateAdj  : doseUnit / adjustUnit / rateUnit
///
/// Usage:
///   Load this script in FSI, then:
///   let validation = extracted |> Validation.validate routeMappings genericNames
///   Validation.printResult validation
///
/// Note: DoseRuleExtracted type is defined here to mirror DoseRuleExtraction.fsx.
///   Keep both definitions in sync.

#r "nuget: FSharpPlus"
#r "nuget: Newtonsoft.Json"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"

// All GenFORM dependencies are co-located in the GenFORM bin output directory
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"

open System
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources


/// The structured extraction output from DoseRuleExtraction.fsx.
/// This type must be kept in sync with DoseRuleExtracted in DoseRuleExtraction.fsx.
type DoseRuleExtracted =
    {|
        source: string
        sourceText: string
        generic: string
        form: string
        brand: string
        gpks: string[]
        indication: string
        route: string
        department: string
        gender: string
        minAge: Nullable<float>
        maxAge: Nullable<float>
        minWeight: Nullable<float>
        maxWeight: Nullable<float>
        minBSA: Nullable<float>
        maxBSA: Nullable<float>
        minGestAge: Nullable<float>
        maxGestAge: Nullable<float>
        minPMAge: Nullable<float>
        maxPMAge: Nullable<float>
        doseType: string
        doseText: string
        scheduleText: string
        frequencies: int[]
        freqUnit: string
        minTime: Nullable<float>
        maxTime: Nullable<float>
        timeUnit: string
        minInterval: Nullable<float>
        maxInterval: Nullable<float>
        intervalUnit: string
        minDuration: Nullable<float>
        maxDuration: Nullable<float>
        durUnit: string
        ``component``: string
        substance: string
        doseUnit: string
        adjustUnit: string
        rateUnit: string
        minQty: Nullable<float>
        maxQty: Nullable<float>
        minQtyAdj: Nullable<float>
        maxQtyAdj: Nullable<float>
        minPerTime: Nullable<float>
        maxPerTime: Nullable<float>
        minPerTimeAdj: Nullable<float>
        maxPerTimeAdj: Nullable<float>
        minRate: Nullable<float>
        maxRate: Nullable<float>
        minRateAdj: Nullable<float>
        maxRateAdj: Nullable<float>
    |}


/// Represents the outcome of a single field validation.
type FieldValidation =
    | Valid of fieldName: string * value: string
    | Warning of fieldName: string * value: string * message: string
    | Invalid of fieldName: string * value: string * reason: string


/// Represents the full validation result for a DoseRuleExtracted record.
type ValidationResult =
    {
        Extracted: DoseRuleExtracted
        Fields: FieldValidation list
    }

    member this.IsValid =
        this.Fields
        |> List.forall (
            function
            | Invalid _ -> false
            | _ -> true
        )

    member this.Errors =
        this.Fields
        |> List.choose (
            function
            | Invalid(f, v, r) -> Some(f, v, r)
            | _ -> None
        )

    member this.Warnings =
        this.Fields
        |> List.choose (
            function
            | Warning(f, v, m) -> Some(f, v, m)
            | _ -> None
        )


module Validation =

    let private equalsCI (a: string) (b: string) =
        String.Compare(a, b, StringComparison.OrdinalIgnoreCase) = 0


    /// Validate the generic name against a list of known generic names.
    let validateGeneric (genericNames: string list) (extracted: DoseRuleExtracted) =
        if extracted.generic |> String.isNullOrWhiteSpace then
            Invalid("generic", extracted.generic, "Generic name is empty")
        elif genericNames |> List.exists (equalsCI extracted.generic) then
            Valid("generic", extracted.generic)
        else
            // Try a partial / fuzzy match for helpful warnings
            let partialMatch =
                genericNames
                |> List.tryFind (fun g ->
                    g |> String.containsCapsInsens extracted.generic
                    || extracted.generic |> String.containsCapsInsens g
                )

            match partialMatch with
            | Some candidate ->
                Warning("generic", extracted.generic, $"No exact match found; closest formulary entry is '{candidate}'")
            | None ->
                Invalid(
                    "generic",
                    extracted.generic,
                    $"'{extracted.generic}' not found in formulary; check spelling or add to formulary"
                )


    /// Validate the route against the formulary route mappings.
    let validateRoute (routeMappings: RouteMapping array) (extracted: DoseRuleExtracted) =
        if extracted.route |> String.isNullOrWhiteSpace then
            Warning("route", extracted.route, "Route is empty; a route is expected for most dose rules")
        else
            let matched =
                routeMappings
                |> Array.exists (fun rm -> equalsCI rm.Long extracted.route || equalsCI rm.Short extracted.route)

            if matched then
                Valid("route", extracted.route)
            else
                let knownRoutes = routeMappings |> Array.map _.Long |> String.concat ", "

                Invalid(
                    "route",
                    extracted.route,
                    $"Route '{extracted.route}' not found in route mappings; valid routes are: {knownRoutes}"
                )


    /// Validate the dose type.
    let validateDoseType (extracted: DoseRuleExtracted) =
        let valid =
            [
                "once"
                "onceTimed"
                "discontinuous"
                "timed"
                "continuous"
                ""
            ]

        if valid |> List.contains extracted.doseType then
            Valid("doseType", extracted.doseType)
        else
            let validList = valid |> List.filter ((<>) "") |> String.concat ", "

            Invalid(
                "doseType",
                extracted.doseType,
                $"'{extracted.doseType}' is not a valid DoseType; must be one of: {validList}"
            )


    /// Validate the adjust unit.
    let validateAdjustUnit (extracted: DoseRuleExtracted) =
        let valid = [ "kg"; "m2"; "" ]

        if valid |> List.contains extracted.adjustUnit then
            Valid("adjustUnit", extracted.adjustUnit)
        else
            Invalid(
                "adjustUnit",
                extracted.adjustUnit,
                $"'{extracted.adjustUnit}' is not a valid adjustUnit; must be 'kg', 'm2', or empty"
            )


    /// Validate that an age value (in days) is plausible.
    let validateAge (fieldName: string) (n: Nullable<float>) =
        if not n.HasValue then
            Valid(fieldName, "null")
        elif n.Value < 0.0 then
            Invalid(fieldName, string n.Value, $"{fieldName} cannot be negative")
        elif n.Value > 36500.0 then
            Warning(fieldName, string n.Value, $"{fieldName} = {n.Value} days seems very large (>100 years)")
        else
            Valid(fieldName, string n.Value)


    /// Validate that a weight value (in grams) is plausible.
    let validateWeight (fieldName: string) (n: Nullable<float>) =
        if not n.HasValue then
            Valid(fieldName, "null")
        elif n.Value < 0.0 then
            Invalid(fieldName, string n.Value, $"{fieldName} cannot be negative")
        elif n.Value > 200000.0 then
            Warning(fieldName, string n.Value, $"{fieldName} = {n.Value}g seems very large (>200 kg)")
        else
            Valid(fieldName, string n.Value)


    /// Validate that a BSA value (in m2) is plausible.
    let validateBSA (fieldName: string) (n: Nullable<float>) =
        if not n.HasValue then
            Valid(fieldName, "null")
        elif n.Value < 0.0 then
            Invalid(fieldName, string n.Value, $"{fieldName} cannot be negative")
        elif n.Value > 3.0 then
            Warning(fieldName, string n.Value, $"{fieldName} = {n.Value} m² seems very large (>3 m²)")
        else
            Valid(fieldName, string n.Value)


    /// Validate that the unit structure for a compound unit field is correct.
    /// For example, MinPerTimeAdj should be: doseUnit / adjustUnit / freqUnit
    let validateUnitStructure (fieldName: string) (expectedParts: string list) (extracted: DoseRuleExtracted) =
        let nonEmpty s = s |> String.isNullOrWhiteSpace |> not
        let nonEmptyParts = expectedParts |> List.filter nonEmpty

        if nonEmptyParts |> List.isEmpty then
            Valid(fieldName, "(unit parts all empty — skipped)")
        elif expectedParts |> List.forall nonEmpty then
            // All expected parts are present and non-empty
            let composed = expectedParts |> String.concat "/"
            Valid(fieldName, $"unit structure ok: {composed}")
        else
            // Some expected parts are empty strings — incomplete unit structure
            let missing =
                expectedParts |> List.filter (String.isNullOrWhiteSpace) |> List.length

            let parts = expectedParts |> String.concat "/"
            Warning(fieldName, "", $"Unit structure incomplete for {fieldName}: {parts}; missing: {missing} part(s)")


    /// Validate the unit structures for all dose limit fields.
    let validateDoseLimitUnits (extracted: DoseRuleExtracted) =
        [
            // QtyAdj: doseUnit / adjustUnit
            if extracted.minQtyAdj.HasValue || extracted.maxQtyAdj.HasValue then
                yield validateUnitStructure "QtyAdj units" [ extracted.doseUnit; extracted.adjustUnit ] extracted

            // PerTime: doseUnit / freqUnit
            if extracted.minPerTime.HasValue || extracted.maxPerTime.HasValue then
                yield validateUnitStructure "PerTime units" [ extracted.doseUnit; extracted.freqUnit ] extracted

            // PerTimeAdj: doseUnit / adjustUnit / freqUnit
            if extracted.minPerTimeAdj.HasValue || extracted.maxPerTimeAdj.HasValue then
                yield
                    validateUnitStructure
                        "PerTimeAdj units"
                        [
                            extracted.doseUnit
                            extracted.adjustUnit
                            extracted.freqUnit
                        ]
                        extracted

            // Rate: doseUnit / rateUnit
            if extracted.minRate.HasValue || extracted.maxRate.HasValue then
                yield validateUnitStructure "Rate units" [ extracted.doseUnit; extracted.rateUnit ] extracted

            // RateAdj: doseUnit / adjustUnit / rateUnit
            if extracted.minRateAdj.HasValue || extracted.maxRateAdj.HasValue then
                yield
                    validateUnitStructure
                        "RateAdj units"
                        [
                            extracted.doseUnit
                            extracted.adjustUnit
                            extracted.rateUnit
                        ]
                        extracted
        ]


    /// Validate min ≤ max for a numeric range pair.
    let validateMinMax (fieldBase: string) (mn: Nullable<float>) (mx: Nullable<float>) =
        match mn.HasValue, mx.HasValue with
        | false, false -> []
        | true, false -> [ Valid($"{fieldBase}Range", $"min={mn.Value} (no max)") ]
        | false, true -> [ Valid($"{fieldBase}Range", $"(no min) max={mx.Value}") ]
        | true, true ->
            if mn.Value <= mx.Value then
                [
                    Valid($"{fieldBase}Range", $"min={mn.Value} max={mx.Value}")
                ]
            else
                [
                    Invalid($"{fieldBase}Range", $"{mn.Value}–{mx.Value}", $"min ({mn.Value}) > max ({mx.Value})")
                ]


    /// Run all validations on an extracted dose rule.
    let validate
        (routeMappings: RouteMapping array)
        (genericNames: string list)
        (extracted: DoseRuleExtracted)
        : ValidationResult
        =

        let fields =
            [
                validateGeneric genericNames extracted
                validateRoute routeMappings extracted
                validateDoseType extracted
                validateAdjustUnit extracted

                // Patient dimensions
                validateAge "minAge" extracted.minAge
                validateAge "maxAge" extracted.maxAge
                yield! validateMinMax "age" extracted.minAge extracted.maxAge

                validateWeight "minWeight" extracted.minWeight
                validateWeight "maxWeight" extracted.maxWeight
                yield! validateMinMax "weight" extracted.minWeight extracted.maxWeight

                validateBSA "minBSA" extracted.minBSA
                validateBSA "maxBSA" extracted.maxBSA
                yield! validateMinMax "BSA" extracted.minBSA extracted.maxBSA

                validateAge "minGestAge" extracted.minGestAge
                validateAge "maxGestAge" extracted.maxGestAge
                yield! validateMinMax "gestAge" extracted.minGestAge extracted.maxGestAge

                validateAge "minPMAge" extracted.minPMAge
                validateAge "maxPMAge" extracted.maxPMAge
                yield! validateMinMax "PMAge" extracted.minPMAge extracted.maxPMAge

                // Dose ranges
                yield! validateMinMax "qty" extracted.minQty extracted.maxQty
                yield! validateMinMax "qtyAdj" extracted.minQtyAdj extracted.maxQtyAdj
                yield! validateMinMax "perTime" extracted.minPerTime extracted.maxPerTime
                yield! validateMinMax "perTimeAdj" extracted.minPerTimeAdj extracted.maxPerTimeAdj
                yield! validateMinMax "rate" extracted.minRate extracted.maxRate
                yield! validateMinMax "rateAdj" extracted.minRateAdj extracted.maxRateAdj
                yield! validateMinMax "time" extracted.minTime extracted.maxTime
                yield! validateMinMax "interval" extracted.minInterval extracted.maxInterval
                yield! validateMinMax "duration" extracted.minDuration extracted.maxDuration

                // Unit structure checks
                yield! validateDoseLimitUnits extracted
            ]

        {
            Extracted = extracted
            Fields = fields
        }


    /// Print the validation result to stdout.
    let printResult (result: ValidationResult) =
        let status = if result.IsValid then "✓ VALID" else "✗ INVALID"
        printfn $"\n## Validation Result: {status}\n"

        printfn "### Generic: %s | Route: %s" result.Extracted.generic result.Extracted.route

        if result.Errors |> List.isEmpty |> not then
            printfn "\n#### Errors:"

            for (f, v, r) in result.Errors do
                printfn "  ✗ [%s] '%s' → %s" f v r

        if result.Warnings |> List.isEmpty |> not then
            printfn "\n#### Warnings:"

            for (f, v, m) in result.Warnings do
                printfn "  ⚠ [%s] '%s' → %s" f v m

        if result.Errors |> List.isEmpty && result.Warnings |> List.isEmpty then
            printfn "  All checks passed."

        printfn ""


/// -------------------------------------------------------
/// Formulary loading helpers
/// -------------------------------------------------------

module Formulary =

    /// Load a formulary provider using the GENPRES_URL_ID environment variable.
    /// Set the environment variable before calling this function.
    let loadProvider () =
        Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
        let dataUrlId = System.Environment.GetEnvironmentVariable("GENPRES_URL_ID")

        Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


    /// Extract the sorted list of unique generic names from the formulary dose rules.
    let getGenericNames (provider: IResourceProvider) : string list =
        provider.GetDoseRules()
        |> Array.map _.Generic
        |> Array.distinct
        |> Array.sort
        |> Array.toList


    /// Get the route mappings from the formulary.
    let getRouteMappings (provider: IResourceProvider) : RouteMapping array = provider.GetRouteMappings()


/// -------------------------------------------------------
/// Example usage — comment out when not running interactively
/// -------------------------------------------------------

(*
open Informedica.Utils.Lib

// Set up environment
let dataPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
System.Environment.CurrentDirectory <- dataPath

// Load formulary
let provider = Formulary.loadProvider ()
let genericNames = Formulary.getGenericNames provider
let routeMappings = Formulary.getRouteMappings provider

printfn $"Loaded {genericNames |> List.length} generics and {routeMappings |> Array.length} route mappings"

// Example: validate a hypothetical extracted result
let exampleExtracted: DoseRuleExtracted =
    {|
        source = "Kinderformularium"
        sourceText = "acetylsalicylzuur 1 maand tot 18 jaar 30-50 mg/kg/dag in 3-4 doses"
        generic = "acetylsalicylzuur"
        form = "tablet"
        brand = ""
        gpks = [||]
        indication = ""
        route = "ORAAL"
        department = ""
        gender = ""
        minAge = Nullable(30.0)
        maxAge = Nullable(6570.0)
        minWeight = Nullable()
        maxWeight = Nullable()
        minBSA = Nullable()
        maxBSA = Nullable()
        minGestAge = Nullable()
        maxGestAge = Nullable()
        minPMAge = Nullable()
        maxPMAge = Nullable()
        doseType = "discontinuous"
        doseText = ""
        scheduleText = "30-50 mg/kg/dag in 3-4 doses; max 3000 mg/dag"
        frequencies = [| 3; 4 |]
        freqUnit = "dag"
        minTime = Nullable()
        maxTime = Nullable()
        timeUnit = ""
        minInterval = Nullable()
        maxInterval = Nullable()
        intervalUnit = ""
        minDuration = Nullable()
        maxDuration = Nullable()
        durUnit = ""
        ``component`` = ""
        substance = "acetylsalicylzuur"
        doseUnit = "mg"
        adjustUnit = "kg"
        rateUnit = ""
        minQty = Nullable()
        maxQty = Nullable()
        minQtyAdj = Nullable(30.0)
        maxQtyAdj = Nullable(50.0)
        minPerTime = Nullable()
        maxPerTime = Nullable(3000.0)
        minPerTimeAdj = Nullable()
        maxPerTimeAdj = Nullable()
        minRate = Nullable()
        maxRate = Nullable()
        minRateAdj = Nullable()
        maxRateAdj = Nullable()
    |}

let validationResult =
    exampleExtracted
    |> Validation.validate routeMappings genericNames

Validation.printResult validationResult
*)

/// Tests for the NLP dose rule extraction pipeline.
///
/// This script tests the extraction logic (validation, schema checking, unit structure)
/// without requiring an LLM API key.
///
/// Run with: dotnet fsi DoseRuleTests.fsx
/// Or interactively from: src/Informedica.NLP.Lib/Scripts/

#r "nuget: expecto"
#r "nuget: FSharpPlus"
#r "nuget: Newtonsoft.Json"
#r "nuget: NJsonSchema"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"

#load "../Types.fs"
#load "../Utils.fs"
#load "../Texts.fs"
#load "../Prompts.fs"
#load "../Message.fs"
#load "../Extraction.fs"
#load "../OpenAI.fs"
#load "../Fireworks.fs"
#load "../Ollama.fs"

open System
open Newtonsoft.Json
open Expecto
open Expecto.Flip
open Informedica.OpenAI.Lib


/// -------------------------------------------------------
/// Re-define the types and functions from DoseRuleExtraction
/// (inlined here so the tests can run standalone)
/// -------------------------------------------------------

type DoseRuleExtracted =
    {|
        source: string
        sourceText: string
        generic: string
        form: string
        brand: string
        gpks: string []
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
        frequencies: int []
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


let emptyExtracted: DoseRuleExtracted =
    {|
        source = ""
        sourceText = ""
        generic = ""
        form = ""
        brand = ""
        gpks = [||]
        indication = ""
        route = ""
        department = ""
        gender = ""
        minAge = Nullable()
        maxAge = Nullable()
        minWeight = Nullable()
        maxWeight = Nullable()
        minBSA = Nullable()
        maxBSA = Nullable()
        minGestAge = Nullable()
        maxGestAge = Nullable()
        minPMAge = Nullable()
        maxPMAge = Nullable()
        doseType = ""
        doseText = ""
        scheduleText = ""
        frequencies = [||]
        freqUnit = ""
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
        substance = ""
        doseUnit = ""
        adjustUnit = ""
        rateUnit = ""
        minQty = Nullable()
        maxQty = Nullable()
        minQtyAdj = Nullable()
        maxQtyAdj = Nullable()
        minPerTime = Nullable()
        maxPerTime = Nullable()
        minPerTimeAdj = Nullable()
        maxPerTimeAdj = Nullable()
        minRate = Nullable()
        maxRate = Nullable()
        minRateAdj = Nullable()
        maxRateAdj = Nullable()
    |}


/// -------------------------------------------------------
/// Validation logic (inlined from DoseRuleValidation.fsx
/// so the tests can run standalone)
/// -------------------------------------------------------

type FieldValidation =
    | Valid of fieldName: string * value: string
    | Warning of fieldName: string * value: string * message: string
    | Invalid of fieldName: string * value: string * reason: string


module Validation =

    open Informedica.Utils.Lib.BCL

    let private equalsCI (a: string) (b: string) =
        String.Compare(a, b, StringComparison.OrdinalIgnoreCase) = 0

    let validateGeneric (genericNames: string list) (extracted: DoseRuleExtracted) =
        if extracted.generic |> String.isNullOrWhiteSpace then
            Invalid("generic", extracted.generic, "Generic name is empty")
        elif genericNames |> List.exists (equalsCI extracted.generic) then
            Valid("generic", extracted.generic)
        else
            let partial =
                genericNames
                |> List.tryFind (fun g ->
                    g |> String.containsCapsInsens extracted.generic
                    || extracted.generic |> String.containsCapsInsens g)

            match partial with
            | Some c ->
                Warning("generic", extracted.generic, $"Closest match: '{c}'")
            | None ->
                Invalid("generic", extracted.generic, $"Not found in formulary: '{extracted.generic}'")

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
            Invalid("doseType", extracted.doseType, $"Not a valid DoseType: '{extracted.doseType}'")

    let validateAdjustUnit (extracted: DoseRuleExtracted) =
        let valid = [ "kg"; "m2"; "" ]

        if valid |> List.contains extracted.adjustUnit then
            Valid("adjustUnit", extracted.adjustUnit)
        else
            Invalid("adjustUnit", extracted.adjustUnit, $"Not valid: '{extracted.adjustUnit}'")

    let validateMinMax (fieldBase: string) (mn: Nullable<float>) (mx: Nullable<float>) =
        match mn.HasValue, mx.HasValue with
        | true, true when mn.Value > mx.Value ->
            [
                Invalid(
                    $"{fieldBase}Range",
                    $"{mn.Value}–{mx.Value}",
                    $"min ({mn.Value}) > max ({mx.Value})"
                )
            ]
        | _ -> []

    let validateUnitStructure
        (fieldName: string)
        (expectedParts: string list)
        (extracted: DoseRuleExtracted)
        =
        let nonEmpty = String.isNullOrWhiteSpace >> not
        let nonEmptyParts = expectedParts |> List.filter nonEmpty

        if nonEmptyParts |> List.isEmpty then
            Valid(fieldName, "(skipped — no units)")
        elif expectedParts |> List.forall nonEmpty then
            // All expected parts are present
            Valid(fieldName, expectedParts |> String.concat "/")
        else
            // Some expected parts are empty — incomplete unit structure
            let parts = expectedParts |> String.concat "/"
            Warning(fieldName, "", $"Incomplete unit structure: {parts}")

    let validateDoseLimitUnits (extracted: DoseRuleExtracted) =
        [
            if extracted.minQtyAdj.HasValue || extracted.maxQtyAdj.HasValue then
                yield
                    validateUnitStructure "QtyAdj" [ extracted.doseUnit; extracted.adjustUnit ] extracted

            if extracted.minPerTime.HasValue || extracted.maxPerTime.HasValue then
                yield
                    validateUnitStructure "PerTime" [ extracted.doseUnit; extracted.freqUnit ] extracted

            if extracted.minPerTimeAdj.HasValue || extracted.maxPerTimeAdj.HasValue then
                yield
                    validateUnitStructure
                        "PerTimeAdj"
                        [ extracted.doseUnit; extracted.adjustUnit; extracted.freqUnit ]
                        extracted

            if extracted.minRate.HasValue || extracted.maxRate.HasValue then
                yield
                    validateUnitStructure "Rate" [ extracted.doseUnit; extracted.rateUnit ] extracted

            if extracted.minRateAdj.HasValue || extracted.maxRateAdj.HasValue then
                yield
                    validateUnitStructure
                        "RateAdj"
                        [ extracted.doseUnit; extracted.adjustUnit; extracted.rateUnit ]
                        extracted
        ]


/// -------------------------------------------------------
/// Extraction schema validation (pure logic, no LLM needed)
/// -------------------------------------------------------

let validateExtractionJson (s: string) : Result<string, string> =
    if s |> String.IsNullOrWhiteSpace then
        "Empty response" |> Error
    else
        try
            let extracted = JsonConvert.DeserializeObject<DoseRuleExtracted>(s)
            let validDoseTypes = [ "once"; "onceTimed"; "discontinuous"; "timed"; "continuous"; "" ]
            let validAdjustUnits = [ "kg"; "m2"; "" ]
            let errors =
                [
                    if extracted.generic |> String.IsNullOrWhiteSpace then
                        yield "generic name is empty"
                    if validDoseTypes |> List.contains extracted.doseType |> not then
                        yield $"invalid doseType: '{extracted.doseType}'"
                    if validAdjustUnits |> List.contains extracted.adjustUnit |> not then
                        yield $"invalid adjustUnit: '{extracted.adjustUnit}'"
                ]

            if errors.IsEmpty then Ok s
            else errors |> String.concat "; " |> Error
        with e ->
            $"JSON parse error: {e.Message}" |> Error


/// -------------------------------------------------------
/// Prompt generation (inlined from DoseRuleExtraction.fsx)
/// -------------------------------------------------------

let systemPrompt (sourceName: string) (text: string) = $"""
You are a world-class expert in clinical pharmacology and medication dosing.
You extract structured information from free-text medication dosage descriptions.

Source name: {sourceName}

Text to extract from:
'''{text}'''

Rules:
- Return ONLY valid JSON matching the provided schema exactly.
- For any field that cannot be determined from the text, use null for numbers and "" for strings.
- Age values must be converted to integer days.
- Weight values must be converted to integer grams.
- The adjustUnit must be either "kg" or "m2" (empty string if none).
- The doseType must be one of: "once", "onceTimed", "discontinuous", "timed", "continuous".
"""

let extractionPrompt = """
Extract all dose rule information from the text and return it as a single JSON object
with fields including: generic, form, brand, indication, route, doseType, adjustUnit,
doseUnit, freqUnit, rateUnit, and all min/max quantity fields.
Important unit structure reminders:
- minPerTimeAdj and maxPerTimeAdj quantities are in doseUnit/adjustUnit/freqUnit
- minRateAdj and maxRateAdj quantities are in doseUnit/adjustUnit/rateUnit
- minQtyAdj and maxQtyAdj quantities are in doseUnit/adjustUnit
- All frequencies are integers (number of doses per freqUnit)
Respond with valid JSON only.
"""


/// -------------------------------------------------------
/// Test helpers
/// -------------------------------------------------------

let makeExtracted gen route doseType adjustUnit : DoseRuleExtracted =
    {| emptyExtracted with
        generic = gen
        route = route
        doseType = doseType
        adjustUnit = adjustUnit
    |}


let makeExtractedWithDose doseUnit adjustUnit freqUnit minPerTimeAdj maxPerTimeAdj : DoseRuleExtracted =
    {| emptyExtracted with
        generic = "testdrug"
        doseUnit = doseUnit
        adjustUnit = adjustUnit
        freqUnit = freqUnit
        minPerTimeAdj = minPerTimeAdj
        maxPerTimeAdj = maxPerTimeAdj
    |}


/// -------------------------------------------------------
/// Tests
/// -------------------------------------------------------

[<Tests>]
let validationTests =
    testList "Dose Rule Validation" [

        testList "generic validation" [
            test "empty generic returns Invalid" {
                let r = Validation.validateGeneric [ "paracetamol" ] emptyExtracted
                match r with
                | Invalid _ -> ()
                | _ -> failtest "Expected Invalid for empty generic"
            }

            test "exact match returns Valid" {
                let extracted = makeExtracted "paracetamol" "" "" ""
                let r = Validation.validateGeneric [ "paracetamol" ] extracted
                match r with
                | Valid _ -> ()
                | _ -> failtest "Expected Valid for exact match"
            }

            test "case-insensitive match returns Valid" {
                let extracted = makeExtracted "Paracetamol" "" "" ""
                let r = Validation.validateGeneric [ "paracetamol" ] extracted
                match r with
                | Valid _ -> ()
                | _ -> failtest "Expected Valid for case-insensitive match"
            }

            test "partial match returns Warning" {
                let extracted = makeExtracted "paracetamol" "" "" ""
                let r = Validation.validateGeneric [ "paracetamol tablet 500mg" ] extracted
                match r with
                | Warning _ -> ()
                | _ -> failtest "Expected Warning for partial match"
            }

            test "no match returns Invalid" {
                let extracted = makeExtracted "unknowndrug" "" "" ""
                let r = Validation.validateGeneric [ "paracetamol"; "ibuprofen" ] extracted
                match r with
                | Invalid _ -> ()
                | _ -> failtest "Expected Invalid for unknown generic"
            }
        ]

        testList "doseType validation" [
            for dt in
                [
                    "once"
                    "onceTimed"
                    "discontinuous"
                    "timed"
                    "continuous"
                    ""
                ] do
                test $"'{dt}' is a valid doseType" {
                    let extracted = makeExtracted "test" "" dt ""
                    match Validation.validateDoseType extracted with
                    | Valid _ -> ()
                    | _ -> failtest $"Expected Valid for doseType '{dt}'"
                }

            test "invalid doseType returns Invalid" {
                let extracted = makeExtracted "test" "" "bolus" ""
                match Validation.validateDoseType extracted with
                | Invalid _ -> ()
                | _ -> failtest "Expected Invalid for doseType 'bolus'"
            }
        ]

        testList "adjustUnit validation" [
            for au in [ "kg"; "m2"; "" ] do
                test $"'{au}' is a valid adjustUnit" {
                    let extracted = makeExtracted "test" "" "" au
                    match Validation.validateAdjustUnit extracted with
                    | Valid _ -> ()
                    | _ -> failtest $"Expected Valid for adjustUnit '{au}'"
                }

            test "invalid adjustUnit returns Invalid" {
                let extracted = makeExtracted "test" "" "" "lbs"
                match Validation.validateAdjustUnit extracted with
                | Invalid _ -> ()
                | _ -> failtest "Expected Invalid for adjustUnit 'lbs'"
            }
        ]

        testList "min/max range validation" [
            test "min <= max returns empty error list" {
                let result =
                    Validation.validateMinMax "qty" (Nullable 10.0) (Nullable 20.0)
                result
                |> List.isEmpty
                |> Expect.isTrue "min <= max should produce no errors"
            }

            test "min > max returns Invalid" {
                let result =
                    Validation.validateMinMax "qty" (Nullable 20.0) (Nullable 10.0)

                result
                |> List.length
                |> Expect.equal "Expected exactly one error" 1

                match result |> List.head with
                | Invalid _ -> ()
                | _ -> failtest "Expected Invalid for min > max"
            }

            test "only min, no max returns empty error list" {
                let result = Validation.validateMinMax "qty" (Nullable 10.0) (Nullable())
                result |> List.isEmpty |> Expect.isTrue "Only min should produce no errors"
            }

            test "both null returns empty error list" {
                let result = Validation.validateMinMax "qty" (Nullable()) (Nullable())
                result |> List.isEmpty |> Expect.isTrue "Both null should produce no errors"
            }
        ]

        testList "unit structure validation" [
            test "PerTimeAdj with all units valid produces Valid" {
                let extracted =
                    makeExtractedWithDose "mg" "kg" "dag" (Nullable 10.0) (Nullable 50.0)

                match
                    Validation.validateUnitStructure
                        "PerTimeAdj"
                        [ extracted.doseUnit; extracted.adjustUnit; extracted.freqUnit ]
                        extracted
                with
                | Valid _ -> ()
                | r -> failtest $"Expected Valid but got: {r}"
            }

            test "PerTimeAdj with missing freqUnit produces Warning" {
                let extracted =
                    makeExtractedWithDose "mg" "kg" "" (Nullable 10.0) (Nullable 50.0)

                match
                    Validation.validateUnitStructure
                        "PerTimeAdj"
                        [ extracted.doseUnit; extracted.adjustUnit; extracted.freqUnit ]
                        extracted
                with
                | Warning _ -> ()
                | r -> failtest $"Expected Warning but got: {r}"
            }

            test "validateDoseLimitUnits: PerTimeAdj with all units returns Valid" {
                let extracted =
                    {| emptyExtracted with
                        generic = "testdrug"
                        doseUnit = "mg"
                        adjustUnit = "kg"
                        freqUnit = "dag"
                        minPerTimeAdj = Nullable 10.0
                        maxPerTimeAdj = Nullable 50.0
                    |}

                let results = Validation.validateDoseLimitUnits extracted

                results
                |> List.exists (function
                    | Invalid _ -> true
                    | _ -> false)
                |> Expect.isFalse "No Invalid results expected"
            }

            test "validateDoseLimitUnits: Rate with no rateUnit returns Warning" {
                let extracted =
                    {| emptyExtracted with
                        generic = "testdrug"
                        doseUnit = "mg"
                        adjustUnit = ""
                        rateUnit = ""
                        minRate = Nullable 1.0
                        maxRate = Nullable 5.0
                    |}

                let results = Validation.validateDoseLimitUnits extracted

                results
                |> List.exists (function
                    | Warning _ -> true
                    | _ -> false)
                |> Expect.isTrue "Warning expected when rateUnit is empty"
            }
        ]

        testList "JSON schema validation" [
            test "valid JSON with generic passes validation" {
                let json =
                    """{
                      "source": "test", "sourceText": "test", "generic": "paracetamol",
                      "form": "", "brand": "", "gpks": [], "indication": "", "route": "",
                      "department": "", "gender": "",
                      "minAge": null, "maxAge": null, "minWeight": null, "maxWeight": null,
                      "minBSA": null, "maxBSA": null, "minGestAge": null, "maxGestAge": null,
                      "minPMAge": null, "maxPMAge": null,
                      "doseType": "discontinuous", "doseText": "", "scheduleText": "",
                      "frequencies": [], "freqUnit": "",
                      "minTime": null, "maxTime": null, "timeUnit": "",
                      "minInterval": null, "maxInterval": null, "intervalUnit": "",
                      "minDuration": null, "maxDuration": null, "durUnit": "",
                      "component": "", "substance": "", "doseUnit": "mg",
                      "adjustUnit": "kg", "rateUnit": "",
                      "minQty": null, "maxQty": null,
                      "minQtyAdj": 30.0, "maxQtyAdj": 50.0,
                      "minPerTime": null, "maxPerTime": 3000.0,
                      "minPerTimeAdj": null, "maxPerTimeAdj": null,
                      "minRate": null, "maxRate": null,
                      "minRateAdj": null, "maxRateAdj": null
                    }"""

                match validateExtractionJson json with
                | Ok _ -> ()
                | Error e -> failtest $"Expected Ok but got error: {e}"
            }

            test "JSON with empty generic fails validation" {
                let json =
                    """{
                      "source": "", "sourceText": "", "generic": "",
                      "form": "", "brand": "", "gpks": [], "indication": "", "route": "",
                      "department": "", "gender": "",
                      "minAge": null, "maxAge": null, "minWeight": null, "maxWeight": null,
                      "minBSA": null, "maxBSA": null, "minGestAge": null, "maxGestAge": null,
                      "minPMAge": null, "maxPMAge": null,
                      "doseType": "", "doseText": "", "scheduleText": "",
                      "frequencies": [], "freqUnit": "",
                      "minTime": null, "maxTime": null, "timeUnit": "",
                      "minInterval": null, "maxInterval": null, "intervalUnit": "",
                      "minDuration": null, "maxDuration": null, "durUnit": "",
                      "component": "", "substance": "", "doseUnit": "",
                      "adjustUnit": "", "rateUnit": "",
                      "minQty": null, "maxQty": null,
                      "minQtyAdj": null, "maxQtyAdj": null,
                      "minPerTime": null, "maxPerTime": null,
                      "minPerTimeAdj": null, "maxPerTimeAdj": null,
                      "minRate": null, "maxRate": null,
                      "minRateAdj": null, "maxRateAdj": null
                    }"""

                match validateExtractionJson json with
                | Error _ -> ()
                | Ok _ -> failtest "Expected Error for empty generic"
            }

            test "JSON with invalid doseType fails validation" {
                let json =
                    """{
                      "source": "", "sourceText": "", "generic": "paracetamol",
                      "form": "", "brand": "", "gpks": [], "indication": "", "route": "",
                      "department": "", "gender": "",
                      "minAge": null, "maxAge": null, "minWeight": null, "maxWeight": null,
                      "minBSA": null, "maxBSA": null, "minGestAge": null, "maxGestAge": null,
                      "minPMAge": null, "maxPMAge": null,
                      "doseType": "bolus", "doseText": "", "scheduleText": "",
                      "frequencies": [], "freqUnit": "",
                      "minTime": null, "maxTime": null, "timeUnit": "",
                      "minInterval": null, "maxInterval": null, "intervalUnit": "",
                      "minDuration": null, "maxDuration": null, "durUnit": "",
                      "component": "", "substance": "", "doseUnit": "",
                      "adjustUnit": "", "rateUnit": "",
                      "minQty": null, "maxQty": null,
                      "minQtyAdj": null, "maxQtyAdj": null,
                      "minPerTime": null, "maxPerTime": null,
                      "minPerTimeAdj": null, "maxPerTimeAdj": null,
                      "minRate": null, "maxRate": null,
                      "minRateAdj": null, "maxRateAdj": null
                    }"""

                match validateExtractionJson json with
                | Error msg ->
                    msg
                    |> Expect.stringContains "Expected error to mention doseType" "doseType"
                | Ok _ -> failtest "Expected Error for invalid doseType"
            }

            test "JSON with invalid adjustUnit fails validation" {
                let json =
                    """{
                      "source": "", "sourceText": "", "generic": "paracetamol",
                      "form": "", "brand": "", "gpks": [], "indication": "", "route": "",
                      "department": "", "gender": "",
                      "minAge": null, "maxAge": null, "minWeight": null, "maxWeight": null,
                      "minBSA": null, "maxBSA": null, "minGestAge": null, "maxGestAge": null,
                      "minPMAge": null, "maxPMAge": null,
                      "doseType": "discontinuous", "doseText": "", "scheduleText": "",
                      "frequencies": [], "freqUnit": "",
                      "minTime": null, "maxTime": null, "timeUnit": "",
                      "minInterval": null, "maxInterval": null, "intervalUnit": "",
                      "minDuration": null, "maxDuration": null, "durUnit": "",
                      "component": "", "substance": "", "doseUnit": "mg",
                      "adjustUnit": "lbs", "rateUnit": "",
                      "minQty": null, "maxQty": null,
                      "minQtyAdj": null, "maxQtyAdj": null,
                      "minPerTime": null, "maxPerTime": null,
                      "minPerTimeAdj": null, "maxPerTimeAdj": null,
                      "minRate": null, "maxRate": null,
                      "minRateAdj": null, "maxRateAdj": null
                    }"""

                match validateExtractionJson json with
                | Error msg ->
                    msg
                    |> Expect.stringContains "Expected error to mention adjustUnit" "adjustUnit"
                | Ok _ -> failtest "Expected Error for invalid adjustUnit"
            }

            test "invalid JSON string fails with parse error" {
                match validateExtractionJson "not json" with
                | Error msg ->
                    msg
                    |> Expect.stringContains "Expected parse error message" "parse error"
                | Ok _ -> failtest "Expected Error for invalid JSON"
            }

            test "empty string fails validation" {
                match validateExtractionJson "" with
                | Error _ -> ()
                | Ok _ -> failtest "Expected Error for empty string"
            }
        ]

        testList "system prompt generation" [
            test "system prompt contains source name" {
                let prompt = systemPrompt "Kinderformularium" "some text"
                prompt
                |> Expect.stringContains "Should contain source name" "Kinderformularium"
            }

            test "system prompt contains the text" {
                let prompt = systemPrompt "src" "paracetamol 10 mg/kg/dag"
                prompt
                |> Expect.stringContains "Should contain the text" "paracetamol 10 mg/kg/dag"
            }

            test "extraction prompt contains schema fields" {
                extractionPrompt
                |> Expect.stringContains "Should contain generic field" "generic"
                extractionPrompt
                |> Expect.stringContains "Should contain minPerTimeAdj field" "minPerTimeAdj"
                extractionPrompt
                |> Expect.stringContains "Should contain adjustUnit field" "adjustUnit"
            }
        ]
    ]


// Run tests — works in both FSI and CLI modes
let run () = runTestsWithCLIArgs [] [||] validationTests

// Execute tests
run () |> ignore

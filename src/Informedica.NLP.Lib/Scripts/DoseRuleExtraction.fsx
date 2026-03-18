/// NLP extraction of structured DoseRule data from free text.
///
/// This script implements a comprehensive pipeline to extract all fields of the
/// DoseRuleData model from free-text medication dosage descriptions using an LLM.
///
/// The extraction covers:
///   - Generic name, pharmaceutical form, brand, GPKs
///   - Indication, route, department, gender
///   - Patient dimensions: age (days), weight (grams), BSA (m2), gestational age, PM age
///   - Dose type and schedule text
///   - Schedule: frequencies, infusion time, interval, duration
///   - Dose limits: substance, dose unit, adjust unit, rate unit
///   - All min/max quantities: Qty, QtyAdj, PerTime, PerTimeAdj, Rate, RateAdj
///
/// Unit conventions (matching DoseRuleData):
///   - Age:         days (integer)
///   - Weight:      grams (integer)
///   - BSA:         m2 (float)
///   - GestAge:     days (integer)
///   - PMAge:       days (integer)
///   - MinPerTimeAdj unit structure: dose_unit/adjust_unit/freq_unit
///   - MinRateAdj   unit structure: dose_unit/adjust_unit/rate_unit

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
open Informedica.Utils.Lib.BCL
open Informedica.OpenAI.Lib


/// The structured JSON schema used to prompt the LLM.
/// All numeric fields that are not present in the text must be returned as null (JSON null).
/// All string fields that are not present in the text must be returned as "" (empty string).
/// All array fields that are not present in the text must be returned as [] (empty array).
///
/// Unit encoding conventions (Dutch medical terms):
///   Age/GestAge/PMAge : days as integer (e.g. "3 maanden" → 90, "2 jaar" → 730)
///   Weight            : grams as integer (e.g. "10 kg" → 10000)
///   BSA               : m2 as float (e.g. "1.5 m2" → 1.5)
///   AdjustUnit        : "kg" or "m2" only
///   DoseType          : "once" | "onceTimed" | "discontinuous" | "timed" | "continuous"
let extractionSchema = """
{
  "source": "",
  "sourceText": "",
  "generic": "",
  "form": "",
  "brand": "",
  "gpks": [],
  "indication": "",
  "route": "",
  "department": "",
  "gender": "",
  "minAge": null,
  "maxAge": null,
  "minWeight": null,
  "maxWeight": null,
  "minBSA": null,
  "maxBSA": null,
  "minGestAge": null,
  "maxGestAge": null,
  "minPMAge": null,
  "maxPMAge": null,
  "doseType": "",
  "doseText": "",
  "scheduleText": "",
  "frequencies": [],
  "freqUnit": "",
  "minTime": null,
  "maxTime": null,
  "timeUnit": "",
  "minInterval": null,
  "maxInterval": null,
  "intervalUnit": "",
  "minDuration": null,
  "maxDuration": null,
  "durUnit": "",
  "component": "",
  "substance": "",
  "doseUnit": "",
  "adjustUnit": "",
  "rateUnit": "",
  "minQty": null,
  "maxQty": null,
  "minQtyAdj": null,
  "maxQtyAdj": null,
  "minPerTime": null,
  "maxPerTime": null,
  "minPerTimeAdj": null,
  "maxPerTimeAdj": null,
  "minRate": null,
  "maxRate": null,
  "minRateAdj": null,
  "maxRateAdj": null
}
"""


/// The F# anonymous record type that mirrors the JSON schema above.
/// Used for deserialization and type-safe access to extracted fields.
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


/// System prompt instructing the LLM to act as a medication dosing extraction expert.
/// The source text is embedded so every subsequent question has full context.
let systemPrompt (sourceName: string) (text: string) = $"""
You are a world-class expert in clinical pharmacology and medication dosing.
You extract structured information from free-text medication dosage descriptions.

Source name: {sourceName}

Text to extract from:
'''{text}'''

Rules:
- Return ONLY valid JSON matching the provided schema exactly.
- For any field that cannot be determined from the text, use null for numbers and "" for strings.
- Age values must be converted to integer days (e.g. "3 maanden" = 91 days, "2 jaar" = 730 days).
- Weight values must be converted to integer grams (e.g. "10 kg" = 10000 grams).
- BSA values are kept as float m2.
- Gestational age and post-menstrual age are in integer days (weeks × 7).
- The adjustUnit must be either "kg" or "m2" (empty string if none).
- The doseType must be one of: "once", "onceTimed", "discontinuous", "timed", "continuous".
- Quantities (minQty, maxQty, etc.) are plain floating-point numbers without units.
- Do not include comments or trailing commas in the JSON output.
"""


/// User prompt requesting the full structured extraction for a single DoseRuleData record.
/// The schema is embedded to guide JSON structure.
let extractionPrompt = $"""
Extract all dose rule information from the text and return it as a single JSON object
matching this schema exactly (fill in values from the text, use null/empty for absent fields):

{extractionSchema}

Important unit structure reminders:
- minPerTimeAdj and maxPerTimeAdj quantities are in doseUnit/adjustUnit/freqUnit
- minRateAdj and maxRateAdj quantities are in doseUnit/adjustUnit/rateUnit
- minQtyAdj and maxQtyAdj quantities are in doseUnit/adjustUnit
- All frequencies are integers (number of doses per freqUnit)

Respond with valid JSON only. No additional text or explanation.
"""


/// Validates that a JSON string can be deserialized into DoseRuleExtracted and
/// that required fields (generic, doseType) have plausible values.
let validateExtractionJson (s: string) : Result<string, string> =
    if s |> String.isNullOrWhiteSpace then
        "Empty response" |> Error
    else
        try
            let extracted = JsonConvert.DeserializeObject<DoseRuleExtracted>(s)

            let errors =
                [
                    if extracted.generic |> String.isNullOrWhiteSpace then
                        yield "generic name is empty"

                    let validDoseTypes =
                        [
                            "once"
                            "onceTimed"
                            "discontinuous"
                            "timed"
                            "continuous"
                            ""
                        ]

                    if validDoseTypes |> List.contains extracted.doseType |> not then
                        let validList =
                            validDoseTypes |> List.filter ((<>) "") |> String.concat ", "
                        yield
                            $"doseType '{extracted.doseType}' is not valid; must be one of: {validList}"

                    let validAdjustUnits = [ "kg"; "m2"; "" ]
                    if validAdjustUnits |> List.contains extracted.adjustUnit |> not then
                        yield
                            $"adjustUnit '{extracted.adjustUnit}' is not valid; must be 'kg', 'm2', or empty"
                ]

            if errors |> List.isEmpty then Ok s
            else
                errors
                |> String.concat "; "
                |> Error
        with e ->
            $"JSON parse error: {e.Message}" |> Error


/// Extract a structured DoseRuleExtracted record from free text using the specified LLM.
///
/// Parameters:
///   sendMessages - Provider-specific function: model → messages → Async<Result<string, string>>
///                  (sends messages and returns the raw answer text)
///   model        - The LLM model identifier
///   sourceName   - Name of the source (e.g. "Kinderformularium", "FTK")
///   text         - The free-text dosage description to extract from
///
/// Returns: Async<Result<DoseRuleExtracted, string>>
let extractDoseRule
    (sendMessages : string -> {| role: string; content: string |} list -> Async<Result<string, string>>)
    (model: string)
    (sourceName: string)
    (text: string)
    : Async<Result<DoseRuleExtracted, string>> =

    async {
        let systemContent = systemPrompt sourceName text

        let baseMessages =
            [
                {| role = "system"; content = systemContent |}
                {| role = "user"; content = extractionPrompt |}
            ]

        let rec tryExtract attempt msgs =
            async {
                let! resp = sendMessages model msgs
                match resp with
                | Error e -> return Error e
                | Ok answer ->
                    match answer |> validateExtractionJson with
                    | Error err when attempt < 2 ->
                        // Retry once with the validation error as feedback
                        let retryMsgs =
                            msgs
                            @ [
                                {| role = "assistant"; content = answer |}
                                {|
                                    role = "user"
                                    content =
                                        $"The previous response had issues: {err}. Please correct the JSON and respond only with valid JSON matching the schema."
                                |}
                            ]

                        return! tryExtract (attempt + 1) retryMsgs
                    | Error err -> return Error err
                    | Ok validJson ->
                        return
                            validJson
                            |> JsonConvert.DeserializeObject<DoseRuleExtracted>
                            |> Ok
            }

        return! tryExtract 0 baseMessages
    }


/// Create an OpenAI message sender for use with extractDoseRule.
let openAISender (model: string) (messages: {| role: string; content: string |} list) =
    let firstMsg = messages |> List.head
    let restMsgs = messages |> List.tail

    let chatInput =
        {
            OpenAI.Chat.defaultChatInput
                model
                { Role = firstMsg.role; Content = firstMsg.content; Validator = Ok }
                [] with
                max_tokens = 2000
                response_format = { ``type`` = "json_object" }
                messages =
                    messages
                    |> List.map (fun m -> {| role = m.role; content = m.content |})
        }

    async {
        let! resp = chatInput |> OpenAI.chatJson
        return
            resp
            |> Result.map (fun r ->
                r.Response.choices
                |> List.last
                |> _.message.content)
    }


/// Create a Fireworks message sender for use with extractDoseRule.
let fireworksSender (model: string) (messages: {| role: string; content: string |} list) =
    let firstMsg = messages |> List.head

    let chatInput =
        {
            Fireworks.Chat.defaultChatInput
                model
                { Role = firstMsg.role; Content = firstMsg.content; Validator = Ok }
                [] with
                max_tokens = 2000
                messages =
                    messages
                    |> List.map (fun m -> {| role = m.role; content = m.content |})
        }

    async {
        let! resp = chatInput |> Fireworks.chatJson
        return
            resp
            |> Result.map (fun r ->
                r.Response.choices
                |> List.last
                |> _.message.content)
    }


/// Convenience function: extract using OpenAI.
let extractDoseRuleOpenAI = extractDoseRule openAISender


/// Convenience function: extract using Fireworks.
let extractDoseRuleFireworks = extractDoseRule fireworksSender


/// Pretty-print an extracted DoseRuleExtracted record for inspection.
let printExtracted (r: DoseRuleExtracted) =
    let nullable (n: Nullable<float>) =
        if n.HasValue then string n.Value else "—"

    printfn """
## Extracted DoseRule

### Medication
  generic    : %s
  form       : %s
  brand      : %s
  gpks       : %s
  route      : %s
  indication : %s
  department : %s

### Source
  source     : %s

### Patient
  gender     : %s
  age        : %s — %s days
  weight     : %s — %s g
  BSA        : %s — %s m2
  gestAge    : %s — %s days
  PMAge      : %s — %s days

### Dose type
  doseType   : %s
  doseText   : %s

### Schedule
  frequencies: %s × %s
  time       : %s — %s %s
  interval   : %s — %s %s
  duration   : %s — %s %s

### Dose limits
  substance  : %s
  component  : %s
  doseUnit   : %s
  adjustUnit : %s
  rateUnit   : %s
  Qty        : %s — %s %s
  QtyAdj     : %s — %s %s/%s
  PerTime    : %s — %s %s/%s
  PerTimeAdj : %s — %s %s/%s/%s
  Rate       : %s — %s %s/%s
  RateAdj    : %s — %s %s/%s/%s
"""
        r.generic r.form r.brand (r.gpks |> String.concat ", ")
        r.route r.indication r.department
        r.source
        r.gender
        (nullable r.minAge) (nullable r.maxAge)
        (nullable r.minWeight) (nullable r.maxWeight)
        (nullable r.minBSA) (nullable r.maxBSA)
        (nullable r.minGestAge) (nullable r.maxGestAge)
        (nullable r.minPMAge) (nullable r.maxPMAge)
        r.doseType r.doseText
        (r.frequencies |> Array.map string |> String.concat ";") r.freqUnit
        (nullable r.minTime) (nullable r.maxTime) r.timeUnit
        (nullable r.minInterval) (nullable r.maxInterval) r.intervalUnit
        (nullable r.minDuration) (nullable r.maxDuration) r.durUnit
        r.substance r.``component``
        r.doseUnit r.adjustUnit r.rateUnit
        (nullable r.minQty) (nullable r.maxQty) r.doseUnit
        (nullable r.minQtyAdj) (nullable r.maxQtyAdj) r.doseUnit r.adjustUnit
        (nullable r.minPerTime) (nullable r.maxPerTime) r.doseUnit r.freqUnit
        (nullable r.minPerTimeAdj) (nullable r.maxPerTimeAdj) r.doseUnit r.adjustUnit r.freqUnit
        (nullable r.minRate) (nullable r.maxRate) r.doseUnit r.rateUnit
        (nullable r.minRateAdj) (nullable r.maxRateAdj) r.doseUnit r.adjustUnit r.rateUnit


/// -------------------------------------------------------
/// Example usage — comment out when not running interactively
/// -------------------------------------------------------

(*
let model = OpenAI.Models.``gpt-4-turbo-preview``

let acetylsalicylzuurText = """
acetylsalicylzuur
1 maand tot 18 jaar Startdosering: Acetylsalicylzuur: 30 - 50 mg/kg/dag in 3 - 4 doses. Max: 3.000 mg/dag.
"""

let result =
    extractDoseRuleOpenAI model "Kinderformularium" acetylsalicylzuurText
    |> Async.RunSynchronously

match result with
| Ok extracted ->
    printExtracted extracted
| Error e ->
    printfn $"Extraction error: {e}"
*)

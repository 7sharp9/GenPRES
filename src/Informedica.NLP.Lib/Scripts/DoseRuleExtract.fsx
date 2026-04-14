// Extract dose rule rows from free text via a local Ollama JSON-mode LLM,
// feed the parsed JSON through `DoseRule.processDoseRuleData`, and print the
// resulting `DoseRule` array with `DoseRule.Print.toMarkdown`.
//
// Pipeline:
//   1. Load the prompt markdown file at
//      `docs/data-extraction/doserule-extraction-prompt.md`.
//   2. Call the LLM via `Extraction.extractDoseRule` — a recursive
//      validation-retry loop (up to 2 attempts) that asks for one
//      `{ "rules": [ ... ] }` JSON object.
//   3. Map the JSON to `Types.DoseRuleData[]` (GenFORM shape).
//   4. Call `DoseRule.processDoseRuleData prods routeMapping`, then
//      `mapToDoseRule` + `addDoseLimits` — the same chain used inside
//      `DoseRule.get`.
//   5. Print via `DoseRule.Print.toMarkdown` or `Printing.printExtracted`.
//
// The JSON ↔ TSV round-trip (`Conversion.toTsv` / `Conversion.fromTsv`)
// lets extracted output be saved as a TSV row matching
// `data/sources/Rules/doserules.tsv` and parsed back through the same
// `parseTsv` used by `Benchmark` for ground-truth scoring.
//
// Requires:
//   - A local Ollama server on `http://localhost:11434` with
//     `qwen3-coder:30b` (default) or another JSON-capable model pulled.
//   - `GENPRES_URL_ID` for product / route-mapping lookup.
//   - The GenFORM.Lib DLL rebuilt and loaded (see CLAUDE.md note on DLL
//     reloads — the FSI MCP server must be restarted after rebuilds).


#I __SOURCE_DIRECTORY__
#load "../../../scripts/load-dependencies.fsx"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenCORE.Lib/bin/Debug/net10.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenForm.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.NLP.Lib.dll"


open System
open System.IO
open System.Globalization
open MathNet.Numerics
open Newtonsoft.Json
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.OpenAI.Lib
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Utils


// =======================================================
// Config
// =======================================================

module Config =

    Env.loadDotEnv () |> ignore
    Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

    /// The Google Sheets data URL used by the GenFORM resource provider.
    let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")

    /// Default local Ollama model. qwen3-coder:30b is the best-behaved
    /// model for structured JSON extraction at this corpus. Override per
    /// call via `Pipeline.runWithModelAndPrint`.
    let defaultModel = "qwen3-coder:30b"

    // Bump the Ollama context window so the full extraction prompt
    // (~9 KB) plus the free-text input fit comfortably. Also cap the
    // response size and extend the HttpClient timeout (the default
    // 100 s is too short for a 30B model generating a full JSON block).
    do
        Ollama.options.num_ctx <- Nullable 16384
        Ollama.options.temperature <- Nullable 0.0
        Ollama.options.seed <- Nullable 101
        Ollama.options.repeat_penalty <- Nullable 1.3
        Ollama.options.num_predict <- Nullable 2000

        try
            Informedica.OpenAI.Lib.Utils.client.Timeout <- TimeSpan.FromMinutes 10.0
        with :? InvalidOperationException -> ()

    /// Path to the prompt markdown file.
    let promptPath =
        Path.Combine(
            __SOURCE_DIRECTORY__,
            "../../../docs/data-extraction/doserule-extraction-prompt.md"
        )
        |> Path.GetFullPath

    /// Load the prompt from the markdown file.
    let loadPrompt () = File.ReadAllText promptPath

    /// Resource provider used by `buildDoseRules`.
    let provider: Resources.IResourceProvider =
        Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


// =======================================================
// Schema — unified DoseRuleExtracted record + TSV header
// =======================================================

module Schema =

    /// Shape of one extracted dose rule, as emitted by the LLM.
    /// Numeric fields use `Nullable<float>` so the JSON parser tolerates `null`.
    /// Field names align 1:1 with the TSV header below so JSON ↔ TSV is a
    /// pure rename/format problem.
    type DoseRuleExtracted =
        {|
            sortNo: Nullable<int>
            source: string
            generic: string
            form: string
            brand: string
            gpks: string[]
            route: string
            indication: string
            scheduleText: string
            dep: string
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
            ``component``: string
            substance: string
            freqs: int[]
            doseUnit: string
            adjustUnit: string
            freqUnit: string
            rateUnit: string
            minTime: Nullable<float>
            maxTime: Nullable<float>
            timeUnit: string
            minInt: Nullable<float>
            maxInt: Nullable<float>
            intUnit: string
            minDur: Nullable<float>
            maxDur: Nullable<float>
            durUnit: string
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


    /// Container the LLM wraps its output in so we always deserialize a single
    /// top-level object (Ollama JSON mode emits one JSON object per response).
    type DoseRuleExtractionResult = {| rules: DoseRuleExtracted[] |}


    /// The canonical 50-column TSV header, tab-delimited — identical to line 1
    /// of `data/sources/Rules/doserules.tsv`. Column order must stay aligned
    /// with `Conversion.toTsvRow`.
    let tsvColumns =
        [
            "SortNo"; "Source"; "Generic"; "Form"; "Brand"; "Route"; "GPKs"
            "Indication"; "ScheduleText"; "Dep"; "Gender"; "MinAge"; "MaxAge"
            "MinWeight"; "MaxWeight"; "MinBSA"; "MaxBSA"; "MinGestAge"
            "MaxGestAge"; "MinPMAge"; "MaxPMAge"; "DoseType"; "DoseText"
            "Component"; "Substance"; "Freqs"; "DoseUnit"; "AdjustUnit"
            "FreqUnit"; "RateUnit"; "MinTime"; "MaxTime"; "TimeUnit"; "MinInt"
            "MaxInt"; "IntUnit"; "MinDur"; "MaxDur"; "DurUnit"; "MinQty"
            "MaxQty"; "MinQtyAdj"; "MaxQtyAdj"; "MinPerTime"; "MaxPerTime"
            "MinPerTimeAdj"; "MaxPerTimeAdj"; "MinRate"; "MaxRate"; "MinRateAdj"
            "MaxRateAdj"
        ]

    let tsvHeader = tsvColumns |> String.concat "\t"

    /// Allowed dose-type enum values.
    let validDoseTypes = [ "once"; "onceTimed"; "discontinuous"; "timed"; "continuous"; "" ]

    /// Allowed adjust-unit values.
    let validAdjustUnits = [ "kg"; "m2"; "" ]


// =======================================================
// Prompt — JSON schema example + strict output rules
// =======================================================

module Prompt =

    /// JSON schema shown to the LLM. All fields must be present; numbers
    /// default to `null`, strings to `""`, arrays to `[]`. Minified to save
    /// prompt tokens — LLM parses compact JSON identically.
    let jsonSchema =
        """{"rules":[{"sortNo":1,"source":"","generic":"","form":"","brand":"","gpks":[],"route":"","indication":"","scheduleText":"","dep":"","gender":"","minAge":null,"maxAge":null,"minWeight":null,"maxWeight":null,"minBSA":null,"maxBSA":null,"minGestAge":null,"maxGestAge":null,"minPMAge":null,"maxPMAge":null,"doseType":"","doseText":"","component":"","substance":"","freqs":[],"doseUnit":"","adjustUnit":"","freqUnit":"","rateUnit":"","minTime":null,"maxTime":null,"timeUnit":"","minInt":null,"maxInt":null,"intUnit":"","minDur":null,"maxDur":null,"durUnit":"","minQty":null,"maxQty":null,"minQtyAdj":null,"maxQtyAdj":null,"minPerTime":null,"maxPerTime":null,"minPerTimeAdj":null,"maxPerTimeAdj":null,"minRate":null,"maxRate":null,"minRateAdj":null,"maxRateAdj":null}]}"""


    /// Prompt augmentation for the JSON variant. Directs the LLM to emit a
    /// `{ "rules": [...] }` document following the schema.
    let jsonOverride =
        $"""
OUTPUT (overrides §3/§7):
- Emit ONE JSON object: {{"rules":[...]}}. One array entry per rule. No markdown, no fences, no prose.
- Every field present. Missing: null (num) / "" (str) / [] (arr).
- component + substance required; single-substance → both = generic.
- form: fill only if source states pharmaceutical form, else "".
- sortNo: 1,2,3… in emission order.
- doseType ∈ {{once, onceTimed, discontinuous, timed, continuous}}.
- Units: age/gestAge/pmAge = days (int); weight = grams (int); BSA = m² (float); decimal = `.`.
Schema:
{jsonSchema}"""


    /// Build the system-role message content for one extraction call.
    /// Includes the source banner, the main extraction prompt (loaded from
    /// markdown), and the strict JSON output rules.
    let systemContent (sourceName: string) =
        let banner =
            if String.IsNullOrWhiteSpace sourceName then ""
            else $"Source name: {sourceName}\n\n"

        banner + Config.loadPrompt () + jsonOverride


// =======================================================
// Validation — strict check of the raw JSON reply
// =======================================================

module Validation =

    /// Strip code fences, leading/trailing whitespace, and any commentary
    /// before the first `{` / after the last `}`.
    let cleanup (s: string) =
        if isNull s then ""
        else
            let trimmed = s.Trim()
            let noFence =
                if trimmed.StartsWith("```") then
                    let afterFirst =
                        match trimmed.IndexOf('\n') with
                        | -1 -> trimmed
                        | i -> trimmed.Substring(i + 1)

                    let endFence = afterFirst.LastIndexOf("```")
                    if endFence >= 0 then afterFirst.Substring(0, endFence) else afterFirst
                else
                    trimmed

            let first = noFence.IndexOf('{')
            let last = noFence.LastIndexOf('}')

            if first >= 0 && last > first then noFence.Substring(first, last - first + 1)
            else noFence.Trim()


    /// Validate that a JSON string can be deserialized into
    /// `DoseRuleExtractionResult` and that every rule has a non-empty
    /// `generic`, a recognised `doseType`, and a recognised `adjustUnit`.
    /// Returns the cleaned JSON on success.
    let validateExtractionJson (s: string) : Result<string, string> =
        if String.isNullOrWhiteSpace s then
            Error "Empty response"
        else
            let cleaned = cleanup s

            try
                let parsed =
                    JsonConvert.DeserializeObject<Schema.DoseRuleExtractionResult>(cleaned)

                if isNull (box parsed) || isNull (box parsed.rules) then
                    Error "JSON missing 'rules' array"
                elif parsed.rules.Length = 0 then
                    Error "JSON 'rules' array is empty"
                else
                    let errors =
                        parsed.rules
                        |> Array.mapi (fun i r ->
                            [
                                if String.isNullOrWhiteSpace r.generic then
                                    yield $"rule {i + 1}: generic is empty"

                                if Schema.validDoseTypes |> List.contains r.doseType |> not then
                                    let valids =
                                        Schema.validDoseTypes
                                        |> List.filter ((<>) "")
                                        |> String.concat ", "

                                    yield $"rule {i + 1}: doseType '{r.doseType}' is not valid; must be one of: {valids}"

                                if Schema.validAdjustUnits |> List.contains r.adjustUnit |> not then
                                    yield $"rule {i + 1}: adjustUnit '{r.adjustUnit}' is not valid; must be 'kg', 'm2', or empty"
                            ]
                        )
                        |> Array.collect List.toArray

                    if errors.Length = 0 then Ok cleaned
                    else errors |> String.concat "; " |> Error
            with e ->
                Error $"JSON parse error: {e.Message}"


// =======================================================
// Extraction — recursive tryExtract loop (ported from
// DoseRuleExtraction.fsx) over a pluggable sendMessages
// function. An Ollama sender is provided.
// =======================================================

module Extraction =

    /// Abstract LLM sender. Given a model name and the full message
    /// history, returns the raw assistant content as a string.
    type Sender =
        string
            -> Informedica.OpenAI.Lib.Types.Message list
            -> Async<Result<string, string>>


    /// Ollama sender: treats the last message as the trailing user turn
    /// expected by `Ollama.chat` and passes everything else as history.
    let ollamaSender: Sender =
        fun model msgs ->
            async {
                match List.rev msgs with
                | [] -> return Error "no messages"
                | last :: revInit ->
                    let history = List.rev revInit
                    let! resp = Ollama.chat model history last
                    return resp |> Result.map _.Response.message.content
            }


    /// Extract a structured `DoseRuleExtractionResult` from free text using
    /// the specified LLM sender. Retries up to 2 times on validation failure,
    /// feeding the validation error back as a user turn.
    ///
    /// Parameters:
    ///   sendMessages - LLM sender (e.g. `ollamaSender`)
    ///   model        - The model identifier (e.g. "qwen3-coder:30b")
    ///   sourceName   - Source name included in the system banner
    ///   text         - The free-text dosage description
    let extractDoseRule
        (sendMessages: Sender)
        (model: string)
        (sourceName: string)
        (text: string)
        : Async<Result<Schema.DoseRuleExtractionResult, string>>
        =
        let mkSystem = Informedica.OpenAI.Lib.Message.system
        let mkUser = Informedica.OpenAI.Lib.Message.user
        let mkAssistant = Informedica.OpenAI.Lib.Message.assistant

        async {
            let baseMsgs =
                [
                    mkSystem (Prompt.systemContent sourceName)
                    mkUser text
                ]

            let rec tryExtract attempt msgs =
                async {
                    let! resp = sendMessages model msgs

                    match resp with
                    | Error e -> return Error e
                    | Ok answer ->
                        match Validation.validateExtractionJson answer with
                        | Error err when attempt < 2 ->
                            let retryMsgs =
                                msgs
                                @ [
                                    mkAssistant answer
                                    mkUser
                                        $"The previous response had issues: {err}. Please correct the JSON and respond only with valid JSON matching the schema."
                                ]

                            return! tryExtract (attempt + 1) retryMsgs
                        | Error err -> return Error err
                        | Ok validJson ->
                            return
                                validJson
                                |> JsonConvert.DeserializeObject<Schema.DoseRuleExtractionResult>
                                |> Ok
                }

            return! tryExtract 0 baseMsgs
        }


// =======================================================
// Conversion — JSON ↔ DoseRuleData, JSON ↔ TSV
// =======================================================

module Conversion =

    let inline private brFromFloat f =
        Informedica.Utils.Lib.BCL.BigRational.fromFloat f

    let inline private nullableToBr (n: Nullable<float>) =
        if n.HasValue then brFromFloat n.Value else None

    let private brOptToNullable (br: BigRational option) : Nullable<float> =
        match br with
        | Some v -> Nullable(BigRational.toFloat v)
        | None -> Nullable()

    let private orEmpty (s: string) = if isNull s then "" else s


    /// Convert one JSON-extracted record to `DoseRuleData` (GenFORM shape).
    let toDoseRuleData (r: Schema.DoseRuleExtracted) : DoseRuleData =
        let freqs = r.freqs |> Array.choose (float >> brFromFloat)

        {
            Source = orEmpty r.source
            Indication = orEmpty r.indication
            Generic = orEmpty r.generic
            Form = orEmpty r.form
            Brand = orEmpty r.brand
            GPKs =
                if isNull r.gpks then [||]
                else
                    r.gpks
                    |> Array.map String.trim
                    |> Array.filter String.notEmpty
                    |> Array.distinct
            Route = orEmpty r.route
            Department = orEmpty r.dep
            ScheduleText = orEmpty r.scheduleText
            Gender = orEmpty r.gender |> Gender.fromString
            MinAge = nullableToBr r.minAge
            MaxAge = nullableToBr r.maxAge
            MinWeight = nullableToBr r.minWeight
            MaxWeight = nullableToBr r.maxWeight
            MinBSA = nullableToBr r.minBSA
            MaxBSA = nullableToBr r.maxBSA
            MinGestAge = nullableToBr r.minGestAge
            MaxGestAge = nullableToBr r.maxGestAge
            MinPMAge = nullableToBr r.minPMAge
            MaxPMAge = nullableToBr r.maxPMAge
            DoseType = orEmpty r.doseType
            DoseText = orEmpty r.doseText
            Frequencies = freqs
            DoseUnit = orEmpty r.doseUnit
            AdjustUnit = orEmpty r.adjustUnit
            FreqUnit = orEmpty r.freqUnit
            RateUnit = orEmpty r.rateUnit
            MinTime = nullableToBr r.minTime
            MaxTime = nullableToBr r.maxTime
            TimeUnit = orEmpty r.timeUnit
            MinInterval = nullableToBr r.minInt
            MaxInterval = nullableToBr r.maxInt
            IntervalUnit = orEmpty r.intUnit
            MinDur = nullableToBr r.minDur
            MaxDur = nullableToBr r.maxDur
            DurUnit = orEmpty r.durUnit
            Component = orEmpty r.``component``
            Substance = orEmpty r.substance
            MinQty = nullableToBr r.minQty
            MaxQty = nullableToBr r.maxQty
            MinQtyAdj = nullableToBr r.minQtyAdj
            MaxQtyAdj = nullableToBr r.maxQtyAdj
            MinPerTime = nullableToBr r.minPerTime
            MaxPerTime = nullableToBr r.maxPerTime
            MinPerTimeAdj = nullableToBr r.minPerTimeAdj
            MaxPerTimeAdj = nullableToBr r.maxPerTimeAdj
            MinRate = nullableToBr r.minRate
            MaxRate = nullableToBr r.maxRate
            MinRateAdj = nullableToBr r.minRateAdj
            MaxRateAdj = nullableToBr r.maxRateAdj
            Products = [||]
        }


    /// Inverse of `toDoseRuleData` used by `fromTsv`. BigRational values
    /// lose precision on the float conversion — round-tripping a value
    /// that is not exactly representable as a float will drift, but for
    /// the dosing-corpus ranges in use this is below clinical relevance.
    let doseRuleDataToExtracted (d: DoseRuleData) : Schema.DoseRuleExtracted =
        {|
            sortNo = Nullable()
            source = d.Source
            generic = d.Generic
            form = d.Form
            brand = d.Brand
            gpks = d.GPKs
            route = d.Route
            indication = d.Indication
            scheduleText = d.ScheduleText
            dep = d.Department
            gender = d.Gender |> Gender.toString
            minAge = brOptToNullable d.MinAge
            maxAge = brOptToNullable d.MaxAge
            minWeight = brOptToNullable d.MinWeight
            maxWeight = brOptToNullable d.MaxWeight
            minBSA = brOptToNullable d.MinBSA
            maxBSA = brOptToNullable d.MaxBSA
            minGestAge = brOptToNullable d.MinGestAge
            maxGestAge = brOptToNullable d.MaxGestAge
            minPMAge = brOptToNullable d.MinPMAge
            maxPMAge = brOptToNullable d.MaxPMAge
            doseType = d.DoseType
            doseText = d.DoseText
            ``component`` = d.Component
            substance = d.Substance
            freqs =
                d.Frequencies
                |> Array.map (BigRational.toFloat >> int)
            doseUnit = d.DoseUnit
            adjustUnit = d.AdjustUnit
            freqUnit = d.FreqUnit
            rateUnit = d.RateUnit
            minTime = brOptToNullable d.MinTime
            maxTime = brOptToNullable d.MaxTime
            timeUnit = d.TimeUnit
            minInt = brOptToNullable d.MinInterval
            maxInt = brOptToNullable d.MaxInterval
            intUnit = d.IntervalUnit
            minDur = brOptToNullable d.MinDur
            maxDur = brOptToNullable d.MaxDur
            durUnit = d.DurUnit
            minQty = brOptToNullable d.MinQty
            maxQty = brOptToNullable d.MaxQty
            minQtyAdj = brOptToNullable d.MinQtyAdj
            maxQtyAdj = brOptToNullable d.MaxQtyAdj
            minPerTime = brOptToNullable d.MinPerTime
            maxPerTime = brOptToNullable d.MaxPerTime
            minPerTimeAdj = brOptToNullable d.MinPerTimeAdj
            maxPerTimeAdj = brOptToNullable d.MaxPerTimeAdj
            minRate = brOptToNullable d.MinRate
            maxRate = brOptToNullable d.MaxRate
            minRateAdj = brOptToNullable d.MinRateAdj
            maxRateAdj = brOptToNullable d.MaxRateAdj
        |}


    let private formatNullable (n: Nullable<float>) =
        if n.HasValue then n.Value.ToString(CultureInfo.InvariantCulture)
        else ""

    let private formatInt (n: Nullable<int>) =
        if n.HasValue then string n.Value else "0"


    /// Render one `DoseRuleExtracted` as a `\t`-joined row matching
    /// `Schema.tsvColumns`. Nullable numbers become `""` when absent;
    /// arrays are `;`-joined.
    let toTsvRow (r: Schema.DoseRuleExtracted) : string =
        let lookup =
            [
                "SortNo", formatInt r.sortNo
                "Source", orEmpty r.source
                "Generic", orEmpty r.generic
                "Form", orEmpty r.form
                "Brand", orEmpty r.brand
                "Route", orEmpty r.route
                "GPKs",
                    (if isNull r.gpks then "" else r.gpks |> String.concat ";")
                "Indication", orEmpty r.indication
                "ScheduleText", orEmpty r.scheduleText
                "Dep", orEmpty r.dep
                "Gender", orEmpty r.gender
                "MinAge", formatNullable r.minAge
                "MaxAge", formatNullable r.maxAge
                "MinWeight", formatNullable r.minWeight
                "MaxWeight", formatNullable r.maxWeight
                "MinBSA", formatNullable r.minBSA
                "MaxBSA", formatNullable r.maxBSA
                "MinGestAge", formatNullable r.minGestAge
                "MaxGestAge", formatNullable r.maxGestAge
                "MinPMAge", formatNullable r.minPMAge
                "MaxPMAge", formatNullable r.maxPMAge
                "DoseType", orEmpty r.doseType
                "DoseText", orEmpty r.doseText
                "Component", orEmpty r.``component``
                "Substance", orEmpty r.substance
                "Freqs",
                    (if isNull r.freqs then ""
                     else r.freqs |> Array.map string |> String.concat ";")
                "DoseUnit", orEmpty r.doseUnit
                "AdjustUnit", orEmpty r.adjustUnit
                "FreqUnit", orEmpty r.freqUnit
                "RateUnit", orEmpty r.rateUnit
                "MinTime", formatNullable r.minTime
                "MaxTime", formatNullable r.maxTime
                "TimeUnit", orEmpty r.timeUnit
                "MinInt", formatNullable r.minInt
                "MaxInt", formatNullable r.maxInt
                "IntUnit", orEmpty r.intUnit
                "MinDur", formatNullable r.minDur
                "MaxDur", formatNullable r.maxDur
                "DurUnit", orEmpty r.durUnit
                "MinQty", formatNullable r.minQty
                "MaxQty", formatNullable r.maxQty
                "MinQtyAdj", formatNullable r.minQtyAdj
                "MaxQtyAdj", formatNullable r.maxQtyAdj
                "MinPerTime", formatNullable r.minPerTime
                "MaxPerTime", formatNullable r.maxPerTime
                "MinPerTimeAdj", formatNullable r.minPerTimeAdj
                "MaxPerTimeAdj", formatNullable r.maxPerTimeAdj
                "MinRate", formatNullable r.minRate
                "MaxRate", formatNullable r.maxRate
                "MinRateAdj", formatNullable r.minRateAdj
                "MaxRateAdj", formatNullable r.maxRateAdj
            ]
            |> Map.ofList

        Schema.tsvColumns
        |> List.map (fun c ->
            match lookup.TryFind c with
            | Some v -> v
            | None -> ""
        )
        |> String.concat "\t"


    /// Render a `DoseRuleExtractionResult` as a TSV block with the
    /// canonical header followed by one row per rule.
    let toTsv (r: Schema.DoseRuleExtractionResult) : string =
        let header = Schema.tsvHeader
        let rows = r.rules |> Array.map toTsvRow
        Array.append [| header |] rows |> String.concat "\n"


// =======================================================
// Pipeline — TSV parsing, GenFORM post-processing, and
// synchronous convenience wrappers.
// =======================================================

module Pipeline =

    /// Strip surrounding junk (leading/trailing whitespace, accidental code
    /// fences, stray leading tabs) and split into non-empty lines.
    let private cleanLines (tsv: string) =
        tsv.Split([| '\n' |])
        |> Array.map _.Trim('\r').TrimEnd()
        |> Array.filter (fun l -> not (String.isNullOrWhiteSpace l))
        |> Array.filter (fun l -> not (l.StartsWith("```")))
        |> Array.map (fun l -> if l.StartsWith("\t") then l.Substring(1) else l)


    /// Detect whether a line is the canonical TSV header (first field = "SortNo").
    let private isHeaderLine (line: string) =
        let first = line.Split('\t') |> Array.tryHead |> Option.defaultValue ""
        first.Trim() = "SortNo"


    /// Parse a TSV block into `DoseRuleData[]`. Injects the canonical header
    /// if the input skipped it. Column names and unit semantics follow
    /// `DoseRule.getData` in GenFORM.Lib.
    let parseTsv (tsv: string) : DoseRuleData[] =
        let cleaned = cleanLines tsv

        let lines =
            match cleaned |> Array.tryHead with
            | Some first when isHeaderLine first -> cleaned
            | _ -> Array.append [| Schema.tsvHeader |] cleaned

        if lines.Length < 2 then
            [||]
        else
            let rawRows = lines |> Array.map _.Split('\t')
            let headerLen = rawRows[0].Length

            let pad (row: string[]) =
                if row.Length >= headerLen then row
                else Array.append row (Array.create (headerLen - row.Length) "")

            let rows = rawRows |> Array.map pad
            let columns = rows[0]
            let getColumn = Csv.getStringColumn columns
            let toBrOpt = BigRational.toBrs >> Array.tryHead

            rows
            |> Array.tail
            |> Array.map (fun r ->
                let get name =
                    try
                        getColumn r name
                    with _ ->
                        ""

                {
                    Source = get "Source"
                    Indication = get "Indication"
                    Generic = get "Generic"
                    Form = get "Form"
                    Brand = get "Brand"
                    GPKs =
                        get "GPKs"
                        |> String.splitAt ';'
                        |> Array.map String.trim
                        |> Array.filter String.notEmpty
                        |> Array.distinct
                    Route = get "Route"
                    Department = get "Dep"
                    ScheduleText = get "ScheduleText"
                    Gender = get "Gender" |> Gender.fromString
                    MinAge = get "MinAge" |> toBrOpt
                    MaxAge = get "MaxAge" |> toBrOpt
                    MinWeight = get "MinWeight" |> toBrOpt
                    MaxWeight = get "MaxWeight" |> toBrOpt
                    MinBSA = get "MinBSA" |> toBrOpt
                    MaxBSA = get "MaxBSA" |> toBrOpt
                    MinGestAge = get "MinGestAge" |> toBrOpt
                    MaxGestAge = get "MaxGestAge" |> toBrOpt
                    MinPMAge = get "MinPMAge" |> toBrOpt
                    MaxPMAge = get "MaxPMAge" |> toBrOpt
                    DoseType = get "DoseType"
                    DoseText = get "DoseText"
                    Frequencies = get "Freqs" |> BigRational.toBrs
                    DoseUnit = get "DoseUnit"
                    AdjustUnit = get "AdjustUnit"
                    FreqUnit = get "FreqUnit"
                    RateUnit = get "RateUnit"
                    MinTime = get "MinTime" |> toBrOpt
                    MaxTime = get "MaxTime" |> toBrOpt
                    TimeUnit = get "TimeUnit"
                    MinInterval = get "MinInt" |> toBrOpt
                    MaxInterval = get "MaxInt" |> toBrOpt
                    IntervalUnit = get "IntUnit"
                    MinDur = get "MinDur" |> toBrOpt
                    MaxDur = get "MaxDur" |> toBrOpt
                    DurUnit = get "DurUnit"
                    Component = get "Component"
                    Substance = get "Substance"
                    MinQty = get "MinQty" |> toBrOpt
                    MaxQty = get "MaxQty" |> toBrOpt
                    MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                    MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                    MinPerTime = get "MinPerTime" |> toBrOpt
                    MaxPerTime = get "MaxPerTime" |> toBrOpt
                    MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                    MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                    MinRate = get "MinRate" |> toBrOpt
                    MaxRate = get "MaxRate" |> toBrOpt
                    MinRateAdj = get "MinRateAdj" |> toBrOpt
                    MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                    Products = [||]
                }
            )


    /// Parse a TSV block into `DoseRuleExtracted[]` — the inverse of
    /// `Conversion.toTsv`. Used for round-trip checks and for replaying
    /// archived TSV rows through the JSON-based pipeline.
    let fromTsv (tsv: string) : Schema.DoseRuleExtracted[] =
        tsv
        |> parseTsv
        |> Array.map Conversion.doseRuleDataToExtracted


    /// Feed the parsed `DoseRuleData` through the same post-processing
    /// chain as `DoseRule.get` (processDoseRuleData → mapToDoseRule →
    /// addDoseLimits) and return the resulting DoseRule array.
    let buildDoseRules (data: DoseRuleData[]) =
        let prods = Config.provider.GetProducts()
        let routeMapping = Config.provider.GetRouteMappings()
        let formRoutes = Config.provider.GetFormRoutes()

        data
        |> DoseRule.processDoseRuleData prods routeMapping
        |> Result.map (fun processed ->
            let oks, _errs =
                processed
                |> Array.map (fun d -> d, d |> DoseRule.mapToDoseRule)
                |> Array.partition (snd >> Result.isOk)

            oks
            |> Array.map (fun (d, r) ->
                match r with
                | Ok dr -> dr, d
                | Error _ -> failwith "unreachable: already filtered to Ok"
            )
            |> Array.groupBy fst
            |> Array.map (fun (dr, rs) ->
                dr
                |> DoseRule.addDoseLimits routeMapping formRoutes (rs |> Array.map snd)
            )
        )


    /// End-to-end: free text → `DoseRuleExtractionResult` via Ollama →
    /// `DoseRuleData[]` → `DoseRule[]` → markdown string.
    let extractAndFormat
        (model: string)
        (sourceName: string)
        (freeText: string)
        =
        async {
            let! res =
                Extraction.extractDoseRule Extraction.ollamaSender model sourceName freeText

            match res with
            | Error e -> return Error $"LLM/JSON error: {e}"
            | Ok payload ->
                let data = payload.rules |> Array.map Conversion.toDoseRuleData

                if Array.isEmpty data then
                    return Error "LLM returned zero rules"
                else
                    match buildDoseRules data with
                    | Error msgs -> return Error $"processDoseRuleData failed: {msgs}"
                    | Ok rules -> return rules |> DoseRule.Print.toMarkdown |> Ok
        }


    /// Synchronous wrapper: extract and print with the default model.
    let runAndPrint (sourceName: string) (freeText: string) =
        match
            extractAndFormat Config.defaultModel sourceName freeText
            |> Async.RunSynchronously
        with
        | Ok md -> printfn $"{md}"
        | Error e -> eprintfn $"Extraction failed: {e}"


    /// Synchronous wrapper: extract and print with a specified model.
    let runWithModelAndPrint
        (model: string)
        (sourceName: string)
        (freeText: string)
        =
        match extractAndFormat model sourceName freeText |> Async.RunSynchronously with
        | Ok md -> printfn $"{md}"
        | Error e -> eprintfn $"Extraction failed: {e}"


// =======================================================
// Printing — pretty-print a DoseRuleExtracted for inspection.
// Ported from DoseRuleExtraction.fsx and remapped to the
// unified record shape (dep / minInt / minDur / freqs).
// =======================================================

module Printing =

    let private nullable (n: Nullable<float>) =
        if n.HasValue then string n.Value else "—"


    /// Pretty-print an extracted DoseRuleExtracted record for inspection.
    let printExtracted (r: Schema.DoseRuleExtracted) =
        printfn
            """
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
            r.generic
            r.form
            r.brand
            (if isNull r.gpks then "" else r.gpks |> String.concat ", ")
            r.route
            r.indication
            r.dep
            r.source
            r.gender
            (nullable r.minAge)
            (nullable r.maxAge)
            (nullable r.minWeight)
            (nullable r.maxWeight)
            (nullable r.minBSA)
            (nullable r.maxBSA)
            (nullable r.minGestAge)
            (nullable r.maxGestAge)
            (nullable r.minPMAge)
            (nullable r.maxPMAge)
            r.doseType
            r.doseText
            (if isNull r.freqs then "" else r.freqs |> Array.map string |> String.concat ";")
            r.freqUnit
            (nullable r.minTime)
            (nullable r.maxTime)
            r.timeUnit
            (nullable r.minInt)
            (nullable r.maxInt)
            r.intUnit
            (nullable r.minDur)
            (nullable r.maxDur)
            r.durUnit
            r.substance
            r.``component``
            r.doseUnit
            r.adjustUnit
            r.rateUnit
            (nullable r.minQty)
            (nullable r.maxQty)
            r.doseUnit
            (nullable r.minQtyAdj)
            (nullable r.maxQtyAdj)
            r.doseUnit
            r.adjustUnit
            (nullable r.minPerTime)
            (nullable r.maxPerTime)
            r.doseUnit
            r.freqUnit
            (nullable r.minPerTimeAdj)
            (nullable r.maxPerTimeAdj)
            r.doseUnit
            r.adjustUnit
            r.freqUnit
            (nullable r.minRate)
            (nullable r.maxRate)
            r.doseUnit
            r.rateUnit
            (nullable r.minRateAdj)
            (nullable r.maxRateAdj)
            r.doseUnit
            r.adjustUnit
            r.rateUnit


    /// Print every rule in a `DoseRuleExtractionResult` via `printExtracted`.
    let printExtractionResult (r: Schema.DoseRuleExtractionResult) =
        if isNull (box r.rules) || r.rules.Length = 0 then
            printfn "(no rules)"
        else
            r.rules |> Array.iter printExtracted


// =======================================================
// Benchmark — evaluate local Ollama models against the
// authoritative dose-rule TSV. Unchanged in behaviour;
// re-points to Extraction.extractDoseRule +
// Conversion.toDoseRuleData.
// =======================================================

module Benchmark =

    open System.Diagnostics
    open MathNet.Numerics

    /// Canonical TSV path relative to the repo root.
    let tsvPath =
        Path.Combine(
            __SOURCE_DIRECTORY__,
            "../../../data/sources/Rules/doserules.tsv"
        )
        |> Path.GetFullPath


    /// Parse the authoritative TSV into `DoseRuleData[]`.
    let loadGroundTruth () : DoseRuleData[] =
        let raw = File.ReadAllText tsvPath
        Pipeline.parseTsv raw


    /// A benchmark sample: one input ScheduleText plus all ground-truth
    /// rows that share it (one per Substance / phase).
    type Sample =
        {
            Label: string
            ScheduleText: string
            Expected: DoseRuleData[]
        }


    /// Pick `count` ScheduleText groups from the ground truth, biased
    /// toward multi-phase paragraphs (more interesting to grade) but with
    /// one single-row group for coverage.
    let pickSamples (count: int) (seed: int) (data: DoseRuleData[]) : Sample[] =
        let rng = Random(seed)

        let groups =
            data
            |> Array.filter (fun d -> String.notEmpty d.ScheduleText)
            |> Array.groupBy _.ScheduleText.Trim()
            |> Array.map (fun (t, rs) -> t, rs)

        let multiPhase =
            groups
            |> Array.filter (fun (_, rs) -> rs.Length >= 2)
            |> Array.sortBy (fun _ -> rng.Next())
            |> Array.truncate (max 1 (count - 1))

        let singlePhase =
            groups
            |> Array.filter (fun (_, rs) -> rs.Length = 1)
            |> Array.sortBy (fun _ -> rng.Next())
            |> Array.truncate 1

        Array.append multiPhase singlePhase
        |> Array.mapi (fun i (text, rows) ->
            let first = rows |> Array.head
            let label =
                $"#{i + 1} {first.Generic} / {first.Route} / {first.Indication}"
                |> fun s -> if s.Length > 80 then s.Substring(0, 80) + "…" else s

            {
                Label = label
                ScheduleText = text
                Expected = rows
            }
        )


    /// Result of grading one extraction.
    type Grade =
        {
            Model: string
            Sample: string
            RowCountExpected: int
            RowCountExtracted: int
            DoseTypesMatch: bool
            DoseTextsMatch: bool
            SubstancesMatch: bool
            NumericFieldMatches: int
            NumericFieldMax: int
            ElapsedMs: int64
            Error: string option
        }

        member this.Score =
            if this.Error.IsSome then 0
            else
                let rowPts = if this.RowCountExpected = this.RowCountExtracted then 2 else 0
                let dtPts = if this.DoseTypesMatch then 2 else 0
                let textPts = if this.DoseTextsMatch then 1 else 0
                let subPts = if this.SubstancesMatch then 1 else 0
                let numPts = this.NumericFieldMatches
                rowPts + dtPts + textPts + subPts + numPts

        member this.MaxScore =
            2 + 2 + 1 + 1 + this.NumericFieldMax


    let private eqBr (a: BigRational option) (b: BigRational option) =
        match a, b with
        | None, None -> true
        | Some x, Some y -> x = y
        | _ -> false


    let private numericFields
        : (DoseRuleData -> BigRational option) list
        =
        [
            _.MinQtyAdj
            _.MaxQtyAdj
            _.MinPerTime
            _.MaxPerTime
            _.MinPerTimeAdj
            _.MaxPerTimeAdj
            _.MinRate
            _.MaxRate
        ]


    let private gradeRows
        (expected: DoseRuleData[])
        (extracted: DoseRuleData[])
        =
        let key (r: DoseRuleData) =
            r.DoseType, r.DoseText.ToLower(), r.Substance.ToLower()

        let byKey = extracted |> Array.map (fun r -> key r, r) |> Map.ofArray

        let mutable matches = 0
        let mutable max = 0

        for exp in expected do
            let k = key exp
            match byKey |> Map.tryFind k with
            | None ->
                for getField in numericFields do
                    if (getField exp).IsSome then
                        max <- max + 1
            | Some act ->
                for getField in numericFields do
                    let e = getField exp
                    if e.IsSome then
                        max <- max + 1
                        if eqBr e (getField act) then
                            matches <- matches + 1

        matches, max


    let private lowerSet xs =
        xs |> Array.map (fun (s: string) -> s.ToLower()) |> Set.ofArray


    let private gradeSample (model: string) (sample: Sample) : Grade =
        let sw = Stopwatch.StartNew()

        let runAsync =
            async {
                try
                    let! res =
                        Extraction.extractDoseRule
                            Extraction.ollamaSender
                            model
                            "benchmark"
                            sample.ScheduleText

                    match res with
                    | Error e -> return Error e
                    | Ok payload ->
                        let rows = payload.rules |> Array.map Conversion.toDoseRuleData
                        return Ok rows
                with ex ->
                    return Error ex.Message
            }

        let result =
            try
                Async.RunSynchronously(runAsync, timeout = 10 * 60 * 1000)
            with ex ->
                Error ex.Message

        sw.Stop()

        match result with
        | Error e ->
            {
                Model = model
                Sample = sample.Label
                RowCountExpected = sample.Expected.Length
                RowCountExtracted = 0
                DoseTypesMatch = false
                DoseTextsMatch = false
                SubstancesMatch = false
                NumericFieldMatches = 0
                NumericFieldMax = 0
                ElapsedMs = sw.ElapsedMilliseconds
                Error = Some e
            }
        | Ok extracted ->
            let expTypes = sample.Expected |> Array.map _.DoseType |> lowerSet
            let actTypes = extracted |> Array.map _.DoseType |> lowerSet

            let expTexts = sample.Expected |> Array.map _.DoseText |> lowerSet
            let actTexts = extracted |> Array.map _.DoseText |> lowerSet

            let expSubs = sample.Expected |> Array.map _.Substance |> lowerSet
            let actSubs = extracted |> Array.map _.Substance |> lowerSet

            let numMatches, numMax = gradeRows sample.Expected extracted

            {
                Model = model
                Sample = sample.Label
                RowCountExpected = sample.Expected.Length
                RowCountExtracted = extracted.Length
                DoseTypesMatch = expTypes = actTypes
                DoseTextsMatch = expTexts = actTexts
                SubstancesMatch = expSubs = actSubs
                NumericFieldMatches = numMatches
                NumericFieldMax = numMax
                ElapsedMs = sw.ElapsedMilliseconds
                Error = None
            }


    let private printGradeLine (g: Grade) =
        let status =
            match g.Error with
            | Some e -> $"ERR: {e.Substring(0, min 60 e.Length)}"
            | None ->
                let dt = if g.DoseTypesMatch then "✓DT" else "✗DT"
                let dx = if g.DoseTextsMatch then "✓TX" else "✗TX"
                let sub = if g.SubstancesMatch then "✓Sub" else "✗Sub"
                $"rows {g.RowCountExtracted}/{g.RowCountExpected} {dt} {dx} {sub} num {g.NumericFieldMatches}/{g.NumericFieldMax}"

        printfn
            $"  %-28s{g.Model} %-40s{g.Sample.PadRight(40).Substring(0, 40)} score %2d{g.Score}/%-2d{g.MaxScore} %6d{g.ElapsedMs}ms  %s{status}"


    let private printScoreboard (grades: Grade[]) =
        let groups = grades |> Array.groupBy _.Model

        printfn ""
        printfn "=========================================================================="
        printfn "Scoreboard (higher is better)"
        printfn "=========================================================================="
        printfn "%-28s %-10s %-10s %-12s %s" "Model" "Score" "Max" "Avg ms" "Errors"

        for model, gs in
            groups
            |> Array.sortByDescending (fun (_, gs) -> gs |> Array.sumBy _.Score)
            do
            let total = gs |> Array.sumBy _.Score
            let max = gs |> Array.sumBy _.MaxScore
            let avg =
                if gs.Length > 0 then
                    (gs |> Array.sumBy _.ElapsedMs) / int64 gs.Length
                else
                    0L

            let errs = gs |> Array.filter _.Error.IsSome |> Array.length

            printfn $"%-28s{model} %-10d{total} %-10d{max} %-12d{avg} %d{errs}"


    /// Run the full benchmark. Prints progress per (model, sample) and a
    /// final scoreboard.
    let run (models: string list) (sampleCount: int) (seed: int) : Grade[] =
        let gt = loadGroundTruth ()
        printfn $"Loaded %d{gt.Length} ground-truth DoseRuleData rows from %s{tsvPath}"

        let samples = pickSamples sampleCount seed gt
        printfn $"Picked %d{samples.Length} samples (seed=%d{seed})."

        samples
        |> Array.iteri (fun i s ->
            printfn
                $"  Sample %d{i + 1}: %s{s.Label} (%d{s.Expected.Length} expected row(s), %d{s.ScheduleText.Length} chars)"
        )

        printfn ""

        let grades = ResizeArray<Grade>()

        for model in models do
            printfn $"--- %s{model} ---"

            for sample in samples do
                let g = gradeSample model sample
                grades.Add g
                printGradeLine g

            printfn ""

        let arr = grades.ToArray()
        printScoreboard arr
        arr


// =======================================================
// Example / benchmark invocation — comment out when
// loading the script without triggering a network call.
// =======================================================

/// Models available on the local Ollama server (adjust for your machine):
let benchmarkModels =
    [
        Config.defaultModel
    ]


let freeText =
    """
acetylsalicylzuur, oraal, Ziekte van Kawasaki.
1 maand tot 18 jaar Startdosering: Acetylsalicylzuur: 30 - 50 mg/kg/dag in 3 - 4 doses.
Max: 3.000 mg/dag.
Onderhoudsdosering: Nadat temperatuur genormaliseerd is en CRP gedaald:
dosering verlagen tot 3 - 5 mg/kg/dag in 1 dosis.
"""


// Uncomment one of the following to exercise the pipeline interactively:
//
//   Pipeline.runWithModelAndPrint Config.defaultModel "Kinderformularium" freeText
//
//   match
//       Extraction.extractDoseRule
//           Extraction.ollamaSender
//           Config.defaultModel
//           "Kinderformularium"
//           freeText
//       |> Async.RunSynchronously
//   with
//   | Ok r -> Printing.printExtractionResult r
//   | Error e -> eprintfn $"Extraction failed: {e}"


Benchmark.run benchmarkModels 100 42 |> ignore

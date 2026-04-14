/// Extract dose rule rows from free text using the TSV-based extraction prompt
/// at `docs/data-extraction/doserule-extraction-prompt.md`, feed them through
/// `DoseRule.processDoseRuleData`, and print the resulting `DoseRule` array
/// with `DoseRule.Print.toMarkdown`.
///
/// Pipeline:
///   1. Load the prompt markdown file.
///   2. Send prompt + free-text to an LLM. The LLM returns a tab-delimited TSV
///      block with a header row and one data row per extracted dose rule.
///   3. Parse the TSV into `DoseRuleData[]` using the same column mapping as
///      `DoseRule.getData` in GenFORM.Lib.
///   4. Call `DoseRule.processDoseRuleData prods routeMapping data`,
///      `DoseRule.mapToDoseRule`, and `DoseRule.addDoseLimits` to build
///      `DoseRule[]` — the same chain used inside `DoseRule.get`.
///   5. Print via `DoseRule.Print.toMarkdown`.
///
/// Requires:
///   - A local Ollama server on `http://localhost:11434` with at least one of:
///       qwen3-coder:30b (default — best for structured TSV output),
///       deepseek-r1:32b, gpt-oss:20b, gemma3:12b, llama3:latest.
///   - `GENPRES_URL_ID` for product / route-mapping lookup.
///   - The GenFORM.Lib DLL rebuilt and loaded (see CLAUDE.md note on DLL
///     reloads — the FSI MCP server must be restarted after rebuilds).


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
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.OpenAI.Lib
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Utils


Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")


// -------------------------------------------------------
// 1. Load the extraction prompt
// -------------------------------------------------------

let promptPath =
    Path.Combine(
        __SOURCE_DIRECTORY__,
        "../../../docs/data-extraction/doserule-extraction-prompt.md"
    )
    |> Path.GetFullPath


let loadPrompt () = File.ReadAllText promptPath


// -------------------------------------------------------
// 2. LLM call — prompt + free text → raw TSV string
// -------------------------------------------------------

/// The canonical 50-column TSV header, tab-delimited — identical to line 1 of
/// `data/sources/Rules/doserules.tsv`. Prepended to whatever the LLM returns
/// so parsing is robust against models that skip the header.
let tsvHeader =
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
    |> String.concat "\t"


/// Additional override telling the model the exact row shape to emit.
let headerOverride =
    let fieldCount = tsvHeader.Split('\t').Length

    $"""

STRICT OUTPUT RULES — the following override §3 and §7 of the prompt above:

1. Emit **only** tab-delimited data rows. One row per extracted dose rule.
   Separate rows with a single newline (`\n`). Never put two rows on one
   line.
2. **Do NOT** emit a header row. **Do NOT** emit row numbers, bullets, or
   leading tabs. The first character of every row is the SortNo field value.
3. Each row has **exactly {fieldCount - 1} tab characters** ({fieldCount}
   fields). Empty fields are two adjacent tabs. Use `\t` as separator only;
   never inside a field. Never skip or merge a column — even empty
   `Brand`, `GPKs`, `Dep`, `Form` slots MUST emit their tab.
4. **Component** and **Substance** are REQUIRED — fill both with the
   generic name for single-substance drugs (e.g. `acetylsalicylzuur`).
5. Only fill **Form** when the source explicitly mentions a pharmaceutical
   form. If not mentioned, leave it empty — do not invent `tablet`, etc.
6. SortNo: assign 1, 2, 3 … in the order you emit rows.
7. No code fences, no markdown, no explanation. Tab-delimited rows only.

ANTI-DUPLICATION & MULTI-PHASE RULES (override §5 of the prompt above):

8. **NEVER emit duplicate rows.** Two rows are duplicates when they share
   the same (Source, Generic, Route, Indication, Patient Category, DoseType,
   DoseText, Component, Substance) tuple. If you are about to repeat a row,
   STOP emitting and finish the response. Do not pad, do not loop.
9. **Multi-phase dosing MUST produce distinct rows.** When the source
   mentions both a loading / starting dose AND a maintenance dose
   (cues: `startdosering` + `onderhoudsdosering`, `oplaaddosis` +
   `onderhoud`, `initieel` + `vervolgens`, `dag 1` + `dag 2-N`, …),
   emit **at least one row per phase**, each with its own `DoseText`
   (e.g. `startdosering`, `onderhoudsdosering`) and its own dose limits.
   Missing the start- or maintenance-phase row is a bug.
10. **Do not invent GPKs.** Leave `GPKs` empty unless the source explicitly
    lists a GPK (7-digit numeric code). Do not fabricate codes from memory.
11. **Row budget**: typical free-text inputs yield 1 – 6 rows. If you find
    yourself at row 10 and the source has not clearly enumerated that many
    distinct rules, you are looping — stop.

The schema ({fieldCount} columns, in this exact order) is:
{tsvHeader}"""


/// Default local Ollama model. Override by passing a different name to
/// `extractTsvOllama` — one of: `qwen3-coder:30b`, `deepseek-r1:32b`,
/// `gpt-oss:20b`, `gemma3:12b`, `gemma3:4b`, `llama3:latest`,
/// `llama3.2:latest`, `deepseek-r1:14b`.
///
/// NOTE: do NOT use a thinking model (e.g. `qwen3.5:latest`, `deepseek-r1:*`)
/// with the TSV variant — the chain-of-thought tokens bloat the response,
/// blow past the HttpClient timeout, and corrupt the tab-delimited output.
/// Prefer `qwen3-coder:30b` for TSV; thinking models are tolerable for the
/// JSON variant only when think-tokens are stripped.
let defaultOllamaModel = "qwen3-coder:30b"


/// Bump the Ollama context window so the full extraction prompt (~9 KB) plus
/// the free-text input fit comfortably. Called once at script load time.
///
/// Also bump the underlying HttpClient timeout: the library default is 100 s
/// which is too short for a 30B model generating a full TSV block.
do
    Ollama.options.num_ctx <- Nullable 16384
    Ollama.options.temperature <- Nullable 0.0
    Ollama.options.seed <- Nullable 101
    // Penalise verbatim repetition. The prompt is large and the model loves
    // to pad the tail of the response with duplicate rows once it has found
    // a "safe" shape. A mild penalty nudges it to stop instead.
    Ollama.options.repeat_penalty <- Nullable 1.3
    // Cap output so a runaway model cannot burn 20 min on duplicate rows.
    // A single Dose Rule row is ~400 tokens; 2000 tokens covers ~5 rows,
    // which is enough for all realistic single-paragraph inputs.
    Ollama.options.num_predict <- Nullable 2000
    // HttpClient.Timeout can only be set before the first request. Swallow
    // the InvalidOperationException when re-loading the script in an FSI
    // session that has already made a call.
    try
        Informedica.OpenAI.Lib.Utils.client.Timeout <- TimeSpan.FromMinutes 10.0
    with :? InvalidOperationException -> ()


/// Send the prompt + free-text to a local Ollama server and return the raw
/// assistant message content (expected to be the TSV block).
let extractTsvOllama (model: string) (prompt: string) (freeText: string) =
    let systemMsg = Message.system (prompt + headerOverride)
    let userMsg = Message.user freeText

    async {
        let! resp = Ollama.chat model [ systemMsg ] userMsg
        return resp |> Result.map _.Response.message.content
    }


// -------------------------------------------------------
// 3. Parse TSV → DoseRuleData[]
// -------------------------------------------------------

/// Strip surrounding junk (leading/trailing whitespace, accidental code fences,
/// stray leading/trailing tabs) and split into non-empty lines.
let private cleanLines (tsv: string) =
    tsv.Split([| '\n' |])
    |> Array.map (fun l -> l.Trim('\r').TrimEnd())
    |> Array.filter (fun l -> not (String.isNullOrWhiteSpace l))
    |> Array.filter (fun l -> not (l.StartsWith("```")))
    // drop a single accidental leading tab (models sometimes indent rows)
    |> Array.map (fun l -> if l.StartsWith("\t") then l.Substring(1) else l)


/// Detect whether a line is the canonical TSV header (first field = "SortNo").
let private isHeaderLine (line: string) =
    let first = line.Split('\t') |> Array.tryHead |> Option.defaultValue ""
    first.Trim() = "SortNo"


/// Parse a TSV block into `DoseRuleData[]`. Injects the canonical header if
/// the LLM skipped it. Column names and unit semantics follow
/// `DoseRule.getData` in GenFORM.Lib.
let parseTsv (tsv: string) : Types.DoseRuleData[] =
    let cleaned = cleanLines tsv

    let lines =
        match cleaned |> Array.tryHead with
        | Some first when isHeaderLine first -> cleaned
        | _ -> Array.append [| tsvHeader |] cleaned

    if lines.Length < 2 then
        [||]
    else
        let rawRows = lines |> Array.map (fun l -> l.Split('\t'))
        let headerLen = rawRows[0].Length
        // pad short data rows with empty strings so getStringColumn works
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


// -------------------------------------------------------
// 4. Resource provider (products, route mappings, form routes)
// -------------------------------------------------------

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


// -------------------------------------------------------
// 5. Full pipeline: free text → markdown
// -------------------------------------------------------

/// Feed the parsed DoseRuleData through the same post-processing chain as
/// `DoseRule.get` (processDoseRuleData → mapToDoseRule → addDoseLimits) and
/// return the resulting DoseRule array.
let buildDoseRules (data: Types.DoseRuleData[]) =
    let prods = provider.GetProducts()
    let routeMapping = provider.GetRouteMappings()
    let formRoutes = provider.GetFormRoutes()

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


/// End-to-end: free text → extracted TSV → DoseRule[] → markdown string.
/// `model` is a local Ollama model name (see `defaultOllamaModel`).
let extractAndFormat (model: string) (freeText: string) =
    async {
        let prompt = loadPrompt ()

        let! tsvRes = extractTsvOllama model prompt freeText

        match tsvRes with
        | Error e -> return Error $"LLM error: {e}"
        | Ok tsv ->
            let data = parseTsv tsv

            if data |> Array.isEmpty then
                return Error $"No rows parsed from LLM output:\n{tsv}"
            else
                match data |> buildDoseRules with
                | Error msgs -> return Error $"processDoseRuleData failed: {msgs}"
                | Ok rules -> return rules |> DoseRule.Print.toMarkdown |> Ok
    }


/// Evaluate the pipeline synchronously and print the markdown (or the error).
let runAndPrint (freeText: string) =
    match extractAndFormat defaultOllamaModel freeText |> Async.RunSynchronously with
    | Ok md -> printfn $"{md}"
    | Error e -> eprintfn $"Extraction failed: {e}"


/// Same as `runAndPrint`, but lets you pick a specific Ollama model.
let runWithModelAndPrint (model: string) (freeText: string) =
    match extractAndFormat model freeText |> Async.RunSynchronously with
    | Ok md -> printfn $"{md}"
    | Error e -> eprintfn $"Extraction failed: {e}"


// =======================================================
// JSON VARIANT
// =======================================================
//
// Local models are unreliable at emitting a fixed-width 50-column TSV. The
// JSON path avoids that by letting the LLM emit a nested structure that is
// validated and retried on parse failure. We then map the JSON records to
// `DoseRuleData[]` and feed them through the same `buildDoseRules` pipeline.
//
// Uses Ollama's native `format: "json"` mode via `Ollama.json<_>` for
// structured decoding.

/// Shape of one extracted dose rule, as emitted by the LLM.
/// Numeric fields use `Nullable<float>` so the JSON parser tolerates `null`.
/// Matches the field names described in the Dutch-language extraction prompt.
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


/// JSON schema shown to the LLM. All fields must be present; numbers default
/// to `null`, strings to `""`, arrays to `[]`.
let private jsonSchema =
    """
{
  "rules": [
    {
      "sortNo": 1,
      "source": "",
      "generic": "",
      "form": "",
      "brand": "",
      "gpks": [],
      "route": "",
      "indication": "",
      "scheduleText": "",
      "dep": "",
      "gender": "",
      "minAge": null,   "maxAge": null,
      "minWeight": null, "maxWeight": null,
      "minBSA": null,   "maxBSA": null,
      "minGestAge": null, "maxGestAge": null,
      "minPMAge": null,   "maxPMAge": null,
      "doseType": "",
      "doseText": "",
      "component": "",
      "substance": "",
      "freqs": [],
      "doseUnit": "",
      "adjustUnit": "",
      "freqUnit": "",
      "rateUnit": "",
      "minTime": null, "maxTime": null, "timeUnit": "",
      "minInt": null,  "maxInt": null,  "intUnit": "",
      "minDur": null,  "maxDur": null,  "durUnit": "",
      "minQty": null, "maxQty": null,
      "minQtyAdj": null, "maxQtyAdj": null,
      "minPerTime": null, "maxPerTime": null,
      "minPerTimeAdj": null, "maxPerTimeAdj": null,
      "minRate": null, "maxRate": null,
      "minRateAdj": null, "maxRateAdj": null
    }
  ]
}
"""


/// Prompt augmentation for the JSON variant. Directs the LLM to emit a
/// `{ "rules": [...] }` document following the schema.
let private jsonOverride =
    $"""

STRICT OUTPUT RULES — override §3/§7 of the prompt above:

1. Respond with **one** JSON object with a single top-level property `rules`
   whose value is an array of dose-rule objects. One array entry per
   extracted dose rule.
2. **No** tab-delimited output. **No** markdown. **No** code fences. **No**
   commentary. Only valid JSON.
3. For absent values use `null` (numbers) or `""` (strings) or `[]` (arrays).
   Never omit a field.
4. `component` and `substance` are REQUIRED. For single-substance drugs fill
   both with the generic name.
5. Only fill `form` when the source explicitly mentions a pharmaceutical
   form. If not mentioned, use `""`.
6. Assign `sortNo` 1, 2, 3, … in the order of the emitted rules.
7. `doseType` must be one of: `once`, `onceTimed`, `discontinuous`, `timed`,
   `continuous`.
8. Age / gestational age / post-menstrual age in days (integer). Weight in
   grams (integer). BSA in m² (float). Decimal separator `.`.

Schema (one example row shown; emit one entry per extracted rule):
{jsonSchema}"""


/// Convert one JSON-extracted record to a `DoseRuleData` suitable for the
/// GenFORM pipeline. `BigRational.fromFloat` may fail on non-finite values,
/// so we guard with `Option.bind`.
let toDoseRuleData (r: DoseRuleExtracted) : Types.DoseRuleData =
    // `open Informedica.GenForm.Lib.Utils` above shadows the Utils.Lib.BCL
    // `BigRational` module, so `fromFloat` must be fully qualified.
    let inline brFromFloat f =
        Informedica.Utils.Lib.BCL.BigRational.fromFloat f

    let inline nullableToBr (n: Nullable<float>) =
        if n.HasValue then brFromFloat n.Value else None

    let freqs =
        r.freqs
        |> Array.choose (float >> brFromFloat)

    {
        Source = if isNull r.source then "" else r.source
        Indication = if isNull r.indication then "" else r.indication
        Generic = if isNull r.generic then "" else r.generic
        Form = if isNull r.form then "" else r.form
        Brand = if isNull r.brand then "" else r.brand
        GPKs =
            if isNull r.gpks then [||]
            else
                r.gpks
                |> Array.map String.trim
                |> Array.filter String.notEmpty
                |> Array.distinct
        Route = if isNull r.route then "" else r.route
        Department = if isNull r.dep then "" else r.dep
        ScheduleText = if isNull r.scheduleText then "" else r.scheduleText
        Gender =
            (if isNull r.gender then "" else r.gender) |> Gender.fromString
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
        DoseType = if isNull r.doseType then "" else r.doseType
        DoseText = if isNull r.doseText then "" else r.doseText
        Frequencies = freqs
        DoseUnit = if isNull r.doseUnit then "" else r.doseUnit
        AdjustUnit = if isNull r.adjustUnit then "" else r.adjustUnit
        FreqUnit = if isNull r.freqUnit then "" else r.freqUnit
        RateUnit = if isNull r.rateUnit then "" else r.rateUnit
        MinTime = nullableToBr r.minTime
        MaxTime = nullableToBr r.maxTime
        TimeUnit = if isNull r.timeUnit then "" else r.timeUnit
        MinInterval = nullableToBr r.minInt
        MaxInterval = nullableToBr r.maxInt
        IntervalUnit = if isNull r.intUnit then "" else r.intUnit
        MinDur = nullableToBr r.minDur
        MaxDur = nullableToBr r.maxDur
        DurUnit = if isNull r.durUnit then "" else r.durUnit
        Component = if isNull r.``component`` then "" else r.``component``
        Substance = if isNull r.substance then "" else r.substance
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


/// Send prompt + free-text to Ollama in JSON mode and deserialize the
/// response into `DoseRuleExtractionResult`.
let extractJsonOllama (model: string) (prompt: string) (freeText: string) =
    let systemMsg = Message.system (prompt + jsonOverride)
    let userMsg = Message.user freeText
    Ollama.json<DoseRuleExtractionResult> model [ systemMsg ] userMsg


/// End-to-end JSON variant: free text → JSON → DoseRuleData[] → DoseRule[]
/// → markdown string.
let extractAndFormatJson (model: string) (freeText: string) =
    async {
        let prompt = loadPrompt ()

        let! res = extractJsonOllama model prompt freeText

        match res with
        | Error e -> return Error $"LLM/JSON error: {e}"
        | Ok payload ->
            let data = payload.rules |> Array.map toDoseRuleData

            if data |> Array.isEmpty then
                return Error "LLM returned zero rules"
            else
                match data |> buildDoseRules with
                | Error msgs -> return Error $"processDoseRuleData failed: {msgs}"
                | Ok rules -> return rules |> DoseRule.Print.toMarkdown |> Ok
    }


/// Synchronous wrapper: JSON variant with default model.
let runAndPrintJson (freeText: string) =
    match extractAndFormatJson defaultOllamaModel freeText |> Async.RunSynchronously with
    | Ok md -> printfn $"{md}"
    | Error e -> eprintfn $"Extraction failed: {e}"


/// Synchronous wrapper: JSON variant with specified model.
let runWithModelAndPrintJson (model: string) (freeText: string) =
    match extractAndFormatJson model freeText |> Async.RunSynchronously with
    | Ok md -> printfn $"{md}"
    | Error e -> eprintfn $"Extraction failed: {e}"


// -------------------------------------------------------
// Example usage
// -------------------------------------------------------

let freeText =
    """
acetylsalicylzuur, oraal, Ziekte van Kawasaki.
1 maand tot 18 jaar Startdosering: Acetylsalicylzuur: 30 - 50 mg/kg/dag in 3 - 4 doses.
Max: 3.000 mg/dag.
Onderhoudsdosering: Nadat temperatuur genormaliseerd is en CRP gedaald:
dosering verlagen tot 3 - 5 mg/kg/dag in 1 dosis.
"""

runWithModelAndPrintJson "functiongemma" freeText

// =======================================================
// BENCHMARK — Evaluate local Ollama models against the
// authoritative dose-rule TSV.
// =======================================================
//
// Pipeline per (model, sample):
//   1. Pick one or more ScheduleText samples from
//      `data/sources/Rules/doserules.tsv`.
//   2. Feed each ScheduleText to the model via
//      `extractJsonOllama` (JSON variant).
//   3. Compare the extracted `DoseRuleData[]` against the
//      ground-truth rows that share the same ScheduleText.
//   4. Score the result and time the call.
//
// Output is a scoreboard printed to stdout. No files are
// written.

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


    /// Parse the authoritative TSV (95 columns including duplicates) into
    /// `DoseRuleData[]`. We only read the first 51 columns by name —
    /// `Csv.getStringColumn` takes the first match, which is the
    /// non-duplicate occurrence.
    let loadGroundTruth () : Types.DoseRuleData[] =
        let raw = File.ReadAllText tsvPath
        // Reuse parseTsv — the canonical file starts with "SortNo" so the
        // header is picked up automatically.
        parseTsv raw


    /// A benchmark sample: the input ScheduleText plus all ground-truth
    /// rows that share that ScheduleText (one per Substance / phase).
    type Sample =
        {
            Label: string
            ScheduleText: string
            Expected: Types.DoseRuleData[]
        }


    /// Pick `count` ScheduleText groups from the ground truth, biased
    /// toward multi-phase / multi-substance paragraphs (more interesting
    /// to grade) but with one single-row group for coverage. `seed`
    /// randomises which rows are picked within each bucket so repeated
    /// runs hit different corners of the corpus.
    let pickSamples (count: int) (seed: int) (data: Types.DoseRuleData[]) : Sample[] =
        let rng = Random(seed)
        let groups =
            data
            |> Array.filter (fun d -> String.notEmpty d.ScheduleText)
            |> Array.groupBy (fun d -> d.ScheduleText.Trim())
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


    /// Compare two optional BigRationals for equality, tolerating `None` / `None`.
    let private eqBr (a: BigRational option) (b: BigRational option) =
        match a, b with
        | None, None -> true
        | Some x, Some y -> x = y
        | _ -> false


    /// Numeric fields we grade per-row. Whenever the expected value is not
    /// empty, we count one towards `Max`; we count one towards `Matches`
    /// when the extracted value agrees.
    let private numericFields
        : (Types.DoseRuleData -> BigRational option) list
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


    /// Match each extracted row against its most likely expected row by
    /// (DoseType, DoseText, Substance) then grade numeric fields.
    let private gradeRows
        (expected: Types.DoseRuleData[])
        (extracted: Types.DoseRuleData[])
        =
        let key (r: Types.DoseRuleData) =
            r.DoseType, r.DoseText.ToLower(), r.Substance.ToLower()

        let byKey = extracted |> Array.map (fun r -> key r, r) |> Map.ofArray

        let mutable matches = 0
        let mutable max = 0

        for exp in expected do
            let k = key exp
            match byKey |> Map.tryFind k with
            | None ->
                // No matched row → count expected numeric fields as missed.
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


    let private lowerSet xs = xs |> Array.map (fun (s: string) -> s.ToLower()) |> Set.ofArray


    let private gradeSample (model: string) (sample: Sample) : Grade =
        let sw = Stopwatch.StartNew()

        let runAsync =
            async {
                try
                    let prompt = loadPrompt ()
                    let! res = extractJsonOllama model prompt sample.ScheduleText

                    match res with
                    | Error e -> return Error e
                    | Ok payload ->
                        let rows = payload.rules |> Array.map toDoseRuleData
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


    /// Print a compact per-(model, sample) grade line.
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
            "  %-28s %-40s score %2d/%-2d %6dms  %s"
            g.Model
            (g.Sample.PadRight(40).Substring(0, 40))
            g.Score
            g.MaxScore
            g.ElapsedMs
            status


    /// Print the scoreboard — one row per model, totals across samples.
    let private printScoreboard (grades: Grade[]) =
        let groups = grades |> Array.groupBy _.Model

        printfn ""
        printfn "=========================================================================="
        printfn "Scoreboard (higher is better)"
        printfn "=========================================================================="
        printfn "%-28s %-10s %-10s %-12s %s" "Model" "Score" "Max" "Avg ms" "Errors"

        for model, gs in groups |> Array.sortByDescending (fun (_, gs) -> gs |> Array.sumBy _.Score) do
            let total = gs |> Array.sumBy _.Score
            let max = gs |> Array.sumBy _.MaxScore
            let avg = if gs.Length > 0 then (gs |> Array.sumBy _.ElapsedMs) / int64 gs.Length else 0L
            let errs = gs |> Array.filter (fun g -> g.Error.IsSome) |> Array.length

            printfn "%-28s %-10d %-10d %-12d %d" model total max avg errs


    /// Run the full benchmark. Prints progress per (model, sample) and a
    /// final scoreboard.
    let run (models: string list) (sampleCount: int) (seed: int) : Grade[] =
        let gt = loadGroundTruth ()
        printfn "Loaded %d ground-truth DoseRuleData rows from %s" gt.Length tsvPath

        let samples = pickSamples sampleCount seed gt
        printfn "Picked %d samples (seed=%d)." samples.Length seed

        samples
        |> Array.iteri (fun i s ->
            printfn
                "  Sample %d: %s (%d expected row(s), %d chars)"
                (i + 1)
                s.Label
                s.Expected.Length
                s.ScheduleText.Length
        )

        printfn ""

        let grades = ResizeArray<Grade>()

        for model in models do
            printfn "--- %s ---" model

            for sample in samples do
                let g = gradeSample model sample
                grades.Add g
                printGradeLine g

            printfn ""

        let arr = grades.ToArray()
        printScoreboard arr
        arr


/// Models available on the local Ollama server (adjust for your machine):
let benchmarkModels =
    [
        defaultOllamaModel
    ]


// Run the benchmark on 3 samples (seed fixed for reproducibility). Comment
// this line out to load the script without triggering the benchmark.
Benchmark.run benchmarkModels 100 42 |> ignore

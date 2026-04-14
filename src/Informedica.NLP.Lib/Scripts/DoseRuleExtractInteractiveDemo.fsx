// Demonstrate the `Interactive.runInteractive` flow added to
// `DoseRuleExtract.fsx` end-to-end without hitting the network, Ollama,
// or GenFORM resources. All heavy dependencies (Ollama sender, real
// `Pipeline.checkGrammar`, real `RuleValidation.validate`) are replaced
// by stubs, and stdin is pre-fed via `Console.SetIn` so the script can
// run non-interactively from `dotnet fsi` or the FSI MCP server.
//
// Mirrors the hierarchical shape: one `DoseRuleExtracted` per input
// `scheduleText`, with `doseTypes[]` (start + maintenance) and
// `doseLimits[]` under each dose type.
//
// Run:
//     cd src/Informedica.NLP.Lib/Scripts
//     dotnet fsi DoseRuleExtractInteractiveDemo.fsx
//
// Expected: five labelled scenarios print their step traces and final
// `Saved` / `Aborted` result. No side effects — no file writes, no
// network calls.


#I __SOURCE_DIRECTORY__

open System
open System.IO


module Schema =
    type DoseLimit =
        {|
            ``component``: string
            substance: string
            doseUnit: string
            adjustUnit: string
            rateUnit: string
        |}

    type DoseType =
        {|
            doseType: string
            doseText: string
            doseLimits: DoseLimit[]
        |}

    type DoseRuleExtracted =
        {|
            scheduleText: string
            gender: string
            minAge: Nullable<float>
            maxAge: Nullable<float>
            doseTypes: DoseType[]
        |}


let mkLimit comp sub : Schema.DoseLimit =
    {|
        ``component`` = comp
        substance = sub
        doseUnit = "mg"
        adjustUnit = "kg"
        rateUnit = ""
    |}


let mkDoseType dt comp sub : Schema.DoseType =
    {|
        doseType = dt
        doseText = ""
        doseLimits = [| mkLimit comp sub |]
    |}


let mkRule (txt: string) : Schema.DoseRuleExtracted =
    {|
        scheduleText = txt
        gender = ""
        minAge = Nullable 30.0
        maxAge = Nullable 6570.0
        doseTypes =
            [|
                mkDoseType "start" "Acetylsalicylzuur" "acetylsalicylzuur"
                mkDoseType "maintenance" "Acetylsalicylzuur" "acetylsalicylzuur"
            |]
    |}


module Extraction =

    type Sender = Sender

    let ollamaSender = Sender

    // Stub: ignore input, emit one hierarchical rule with two dose
    // types. The wrapper still has a `rules` array for parity with the
    // real `DoseRuleExtractionResult`.
    let extractDoseRule
        (_: Sender)
        (_: string)
        (_: string)
        (freeText: string)
        =
        async {
            let rule = mkRule freeText
            return Ok {| rules = [| rule |] |}
        }


module Pipeline =

    let saveScheduleText
        (t: string)
        (r: Schema.DoseRuleExtracted)
        : Schema.DoseRuleExtracted
        =
        {| r with scheduleText = t |}

    // Stub grammar check = collapse whitespace deterministically.
    let checkGrammar
        (_: Extraction.Sender)
        (_: string)
        (r: Schema.DoseRuleExtracted)
        =
        let cleaned =
            System.Text.RegularExpressions.Regex.Replace(r.scheduleText, @"\s+", " ").Trim()

        {| r with scheduleText = cleaned |}


module RuleValidation =

    let printCounts (r: Schema.DoseRuleExtracted) =
        let totalLimits =
            r.doseTypes
            |> Array.sumBy (fun dt -> dt.doseLimits.Length)

        printfn
            "doseTypes=%d doseLimits=%d"
            r.doseTypes.Length
            totalLimits

    let validate
        (r: Schema.DoseRuleExtracted)
        : Result<Schema.DoseRuleExtracted, string list>
        =
        Ok r


module Interactive =

    type StepDecision =
        | Proceed
        | SaveAndExit
        | Exit


    type RunResult =
        | Saved of Schema.DoseRuleExtracted
        | Aborted of stage: string
        | Failed of string


    let promptStep (label: string) : StepDecision =
        printfn ""
        printfn $"[{label}] (P)roceed / (S)ave and exit / (E)xit  [P]:"
        let line = Console.ReadLine()

        let key =
            if isNull line then ""
            else line.Trim().ToUpperInvariant()

        printfn "  -> read: %A" key

        if key.StartsWith "S" then SaveAndExit
        elif key.StartsWith "E" then Exit
        else Proceed


    let private saveCurrent (r: Schema.DoseRuleExtracted) = r


    let runInteractive sender model sourceName freeText : RunResult =
        let extracted =
            match
                Extraction.extractDoseRule sender model sourceName freeText
                |> Async.RunSynchronously
            with
            | Ok r -> Ok r.rules
            | Error e -> Error e

        match extracted with
        | Error e -> Failed $"extraction failed: {e}"
        | Ok rs when Array.isEmpty rs -> Failed "extraction returned zero rules"
        | Ok rs when rs.Length > 1 ->
            Failed $"extraction returned {rs.Length} rules; expected 1 per scheduleText"
        | Ok rs ->
            let r0 = rs[0]

            printfn "=== Stage 1: extracted ==="
            r0 |> RuleValidation.printCounts

            match promptStep "after extract" with
            | Exit -> Aborted "extract"
            | SaveAndExit -> Saved(saveCurrent r0)
            | Proceed ->
                printfn ""
                printfn "=== Stage 2: checkGrammar ==="
                let cleaned = Pipeline.checkGrammar sender model r0
                printfn "scheduleText (cleaned):"
                printfn "%s" cleaned.scheduleText

                match promptStep "after checkGrammar" with
                | Exit -> Aborted "checkGrammar"
                | SaveAndExit -> Saved(saveCurrent cleaned)
                | Proceed ->
                    printfn ""
                    printfn "=== Stage 3: validate ==="

                    match RuleValidation.validate cleaned with
                    | Error errs ->
                        errs |> List.iter (printfn "FAIL: %s")
                        Failed(String.concat "; " errs)
                    | Ok r ->
                        printfn "validation OK"

                        match promptStep "after validate" with
                        | Exit -> Aborted "validate"
                        | SaveAndExit
                        | Proceed -> Saved(saveCurrent r)


let raw =
    "   1 maand tot 18 jaar Startdosering:   30 - 50 mg/kg/dag\n\n   Max: 3.000 mg/dag.   "


let run (inputs: string) label =
    printfn "\n##### SCENARIO: %s (stdin=%A) #####" label inputs
    use reader = new StringReader(inputs)
    Console.SetIn(reader)

    let result =
        Interactive.runInteractive
            Extraction.ollamaSender
            "stub-model"
            "Kinderformularium"
            raw

    printfn "RESULT: %A" result

    match result with
    | Interactive.Saved r ->
        printfn "saved scheduleText: %A" r.scheduleText
        printfn "doseTypes.Length = %d" r.doseTypes.Length
        for dt in r.doseTypes do
            printfn "  doseType=%s (%d doseLimits)" dt.doseType dt.doseLimits.Length
    | _ -> ()


run "P\nP\nP\n" "all Proceed (full pipeline)"
run "P\nS\n" "Proceed then Save-and-Exit after checkGrammar"
run "E\n" "Exit immediately after extract"
run "S\n" "Save-and-Exit right after extract (raw text)"
run "\n\n\n" "empty input defaults to Proceed"

#I __SOURCE_DIRECTORY__
#load "load.fsx"


open Informedica.Utils.Lib.BCL
open Shared.Types


// Prototype for FormularyService.checkDoseRules → TextBlock[] with severity color.
// Each didNotPass entry from Check.checkDoseRule is tab-separated:
//   "{target}\t{route}\t{patientCategory}\t{message}"
// Frequency inconsistencies contain the substring "frequenties" in the 4th field
// (Check.fs lines 488-491). Everything else is a dose-range check emitted via
// inRangeOf (Check.fs lines 493-559).


type Severity =
    | Frequency
    | DoseRange


let classify (raw: string) =
    match raw |> String.split "\t" with
    | [ _; _; _; msg ] when msg.Contains "frequenties" -> Frequency
    | _ -> DoseRange


let aggregate (severities: Severity[]) =
    if severities |> Array.isEmpty then Valid
    elif severities |> Array.forall ((=) Frequency) then Warning
    else Alert


/// Mimics the existing ServerApi.Mappers.parseTextItem for prototyping.
let private parseTextItem (s: string) =
    if s |> String.isNullOrWhiteSpace then [||]
    else [| Normal s |]


/// Given the raw didNotPass strings (already deduplicated) and the single/multi
/// rule flag used for formatting, build the TextBlock[] for form.DoseCheck.
let buildDoseCheck (singleRule: bool) (rawLines: string[]) : TextBlock[] =
    let cleaned = rawLines |> Array.filter String.notEmpty
    let severities = cleaned |> Array.map classify
    let wrap = aggregate severities

    let formatted =
        cleaned
        |> Array.map (fun s ->
            match s |> String.split "\t" with
            | [ s1; _; p; s2 ] ->
                if singleRule then $"{s1} {s2}"
                else $"{s1} {p} {s2}"
            | _ -> s)

    match formatted with
    | [||] -> [| "Ok!" |> parseTextItem |> Valid |]
    | xs -> xs |> Array.map (parseTextItem >> wrap)


// --- Validation ---

let tab a b c d = sprintf "%s\t%s\t%s\t%s" a b c d


// Case 1: no violations → Valid / "Ok!"
let case1 = buildDoseCheck true [||]
printfn "Case 1 (no violations): %A" case1

// Case 2: only frequency inconsistencies → Warning
let case2 =
    [|
        tab "paracetamol" "oraal" "0-1 jaar" "frequenties 4 x per dag niet gelijk aan 6 x per dag"
        tab "paracetamol" "oraal" "1-2 jaar" "frequenties 3 x per dag is subset van 4 x per dag"
    |]
    |> buildDoseCheck false
printfn "Case 2 (frequency only): %A" case2

// Case 3: dose-range inconsistency → Alert
let case3 =
    [|
        tab "paracetamol" "oraal" "0-1 jaar" "keer dosering per dag niet in bereik"
    |]
    |> buildDoseCheck true
printfn "Case 3 (dose range): %A" case3

// Case 4: mixed (freq + dose range) → Alert
let case4 =
    [|
        tab "paracetamol" "oraal" "0-1 jaar" "frequenties 4 x per dag niet gelijk aan 6 x per dag"
        tab "paracetamol" "oraal" "0-1 jaar" "dosering per kg per dag niet in bereik"
    |]
    |> buildDoseCheck false
printfn "Case 4 (mixed): %A" case4


// Assertions
let expectCtor name expected actual =
    let ctor tb =
        match tb with
        | Valid _ -> "Valid"
        | Caution _ -> "Caution"
        | Warning _ -> "Warning"
        | Alert _ -> "Alert"
    actual
    |> Array.iter (fun tb ->
        let c = ctor tb
        if c <> expected then
            failwithf "%s: expected %s got %s" name expected c)
    printfn "%s → all %s ✓" name expected


expectCtor "Case 1" "Valid" case1
expectCtor "Case 2" "Warning" case2
expectCtor "Case 3" "Alert" case3
expectCtor "Case 4" "Alert" case4

(*
    EquationsFixture.fsx  —  GENERATOR (not a test fixture)
    -------------------------------------------------------
    The order equations are an INVARIANT of GenORDER, so instead of fetching the
    "Equations" Google sheet at runtime (EquationMapping.getEquations_ -> live fetch,
    needs GENPRES_URL_ID) they are embedded directly in EquationMapping as a readable
    array. This removes the Sheets dependency from the GenORDER solve path.

    This script:
      1. fetches the live "Equations" sheet (urlId from env — only needed to regenerate),
      2. emits the F# block to paste into EquationMapping (array + new getEquations_),
      3. verifies the embedded array reproduces the live equations for every dose type.

    Re-run only when the equation invariant changes.

        cd tests/Informedica.GenORDER.Tests/Scripts
        dotnet fsi EquationsFixture.fsx

    Requires Informedica.Utils.Lib built (dotnet run build).

    Index semantics (EquationMapping.Literals): 3 discontinuous, 4 continuous,
    5 timed, 6 once, 7 onceTimed. Equation string is column 1; a dose type applies
    when that dose type's column holds "x".
*)

#I __SOURCE_DIRECTORY__
#r "../../../src/Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"

open System.IO
open Informedica.Utils.Lib
open Informedica.Utils.Lib.Web


let private doseTypeName =
    [ 3, "discontinuous"; 4, "continuous"; 5, "timed"; 6, "once"; 7, "onceTimed" ]

let private allIndices = doseTypeName |> List.map fst   // [3;4;5;6;7]


let private fetchRows () =
    Env.loadDotEnv () |> ignore

    let urlId =
        Env.getItem "GENPRES_URL_ID"
        |> Option.defaultWith (fun () ->
            failwith "GENPRES_URL_ID not set (needed only to regenerate the embedded equations)"
        )

    match GoogleSheets.getCsvDataFromSheetSync urlId "Equations" with
    | Ok rows -> rows
    | Error e -> failwith $"could not fetch Equations sheet: %A{e}"


// (equation, dose-type indices it applies to), in sheet order, marker-driven —
// exactly the data the source getEquations_ filter operates on.
let private parseRows (rows: string[][]) =
    if rows.Length <= 1 then
        []
    else
        rows
        |> Array.skip 1
        |> Array.choose (fun xs ->
            if xs.Length > 1 then
                let eqn = xs[1]
                let dts = allIndices |> List.filter (fun i -> xs.Length > i && xs[i] = "x")

                if eqn <> "" && not dts.IsEmpty then Some(eqn, dts) else None
            else
                None
        )
        |> Array.toList


// ---------------------------------------------------------------------------
// EMIT the F# block to paste into EquationMapping
// ---------------------------------------------------------------------------

let private emit (parsed: (string * int list) list) =
    let nameOf i = doseTypeName |> List.find (fst >> (=) i) |> snd

    let dtsText dts =
        if List.sort dts = allIndices then
            "allDoseTypes"
        else
            dts |> List.map (fun i -> $"Literals.%s{nameOf i}") |> String.concat "; " |> sprintf "[ %s ]"

    let lines =
        [
            "    // Order equations are an invariant of GenORDER and are embedded here instead"
            "    // of being fetched from the \"Equations\" Google sheet. Each equation is paired"
            "    // with the dose types it applies to (Literals: discontinuous=3 .. onceTimed=7)."
            "    let private allDoseTypes ="
            "        [ Literals.discontinuous; Literals.continuous; Literals.timed; Literals.once; Literals.onceTimed ]"
            ""
            "    let equations : (string * int list) list ="
            "        ["
            yield!
                parsed
                |> List.map (fun (eqn, dts) -> $"            \"%s{eqn}\", %s{dtsText dts}")
            "        ]"
            ""
            "    let private getEquations_ indx ="
            "        equations"
            "        |> List.filter (fun (_, dts) -> dts |> List.contains indx)"
            "        |> List.map fst"
        ]

    let outPath = Path.Combine(__SOURCE_DIRECTORY__, "EquationsEmbedded.generated.fs")
    File.WriteAllLines(outPath, lines)
    printfn $"wrote F# block ({parsed.Length} equations) -> %s{outPath}\n"
    lines |> List.iter (printfn "%s")
    outPath


// ---------------------------------------------------------------------------
// VALIDATE the embedded array == live, per dose type
// ---------------------------------------------------------------------------

let private validate (rows: string[][]) (parsed: (string * int list) list) =
    // embedded loader, exactly the proposed getEquations_
    let embedded indx =
        parsed |> List.filter (fun (_, dts) -> dts |> List.contains indx) |> List.map fst

    // current source filter over the live rows
    let live indx =
        if rows.Length <= 1 then
            []
        else
            rows
            |> Array.skip 1
            |> Array.filter (fun xs -> xs.Length > indx && xs[indx] = "x" && xs.Length > 1)
            |> Array.map (Array.item 1)
            |> Array.toList

    printfn ""
    let mutable ok = true

    for indx, name in doseTypeName do
        if embedded indx = live indx then
            printfn $"OK   %-14s{name}: %2i{(embedded indx).Length} eqs match live sheet"
        else
            ok <- false
            printfn $"FAIL %-14s{name}: live=%i{(live indx).Length} embedded=%i{(embedded indx).Length}"

    printfn ""
    printfn $"""==> %s{if ok then "embedded array reproduces the live equations exactly" else "MISMATCH"}"""


let rows = fetchRows ()
let parsed = parseRows rows
emit parsed |> ignore
validate rows parsed

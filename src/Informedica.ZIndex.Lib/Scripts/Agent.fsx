#I __SOURCE_DIRECTORY__

// Agent architecture demonstration for Informedica.ZIndex.Lib
//
// This script shows how to wrap the ZIndex API in a Command/Response agent
// using Informedica.Agents.Lib.  It follows the script-based development
// workflow described in AGENTS.md — prototype here first, migrate to source
// once the design is approved.
//
// To run: from this directory
//   dotnet fsi Agent.fsx
//
// Pre-requisites: dotnet build GenPRES.sln

#load "load.fsx"

#r "../../Informedica.Agents.Lib/bin/Debug/net10.0/Informedica.Agents.Lib.dll"

open System

open Informedica.Utils.Lib
open Informedica.ZIndex.Lib
open Informedica.Agents.Lib


// ============================================================
// Command / Response discriminated union types
// ============================================================

/// Commands that can be sent to the ZIndex agent
[<RequireQualifiedAccess>]
type ZIndexCommand =
    // Product queries
    | GetProducts
    | FilterByGeneric of generic: string
    | FilterByBrand of brand: string
    | FindByGPK of gpk: int
    // Dose / substance queries
    | GetDoseRules
    | GetSubstances
    // Reference-data queries
    | GetATCGroups
    // Lifecycle
    | Stop of AsyncReplyChannel<unit>


/// Responses returned by the ZIndex agent
[<RequireQualifiedAccess>]
type ZIndexResponse =
    | Products of GenPresProduct array
    | DoseRules of DoseRule array
    | Substances of Substance array
    | ATCGroups of ATCGroup array
    | Stopped
    | Error of string


// ============================================================
// Agent state: data is loaded once at startup and then
// served from memory on every query.
// ============================================================

type ZIndexState =
    {
        Products: GenPresProduct array
        DoseRules: DoseRule array
        Substances: Substance array
        ATCGroups: ATCGroup array
    }


let loadState () =
    printfn "Loading ZIndex data into agent state ..."
    Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "0")
    FilePath.useDemo () |> ignore

    let products = GenPresProduct.get []
    let doseRules = DoseRule.get []
    let substances = Substance.get ()
    let atcGroups = ATCGroup.get ()

    printfn $"  Products:   {products.Length}"
    printfn $"  DoseRules:  {doseRules.Length}"
    printfn $"  Substances: {substances.Length}"
    printfn $"  ATCGroups:  {atcGroups.Length}"

    {
        Products = products
        DoseRules = doseRules
        Substances = substances
        ATCGroups = atcGroups
    }


// ============================================================
// Command processor — pure function: State -> Command -> Response * State
// ============================================================

let processCommand (state: ZIndexState) (cmd: ZIndexCommand) : ZIndexResponse * ZIndexState =
    let response =
        match cmd with
        | ZIndexCommand.GetProducts -> ZIndexResponse.Products state.Products

        | ZIndexCommand.FilterByGeneric generic ->
            state.Products
            |> Array.filter (fun p -> p.Name |> String.toLower |> String.contains (generic |> String.toLower))
            |> ZIndexResponse.Products

        | ZIndexCommand.FilterByBrand brand -> GenPresProduct.findByBrand brand |> ZIndexResponse.Products

        | ZIndexCommand.FindByGPK gpk ->
            match GenPresProduct.findByGPK gpk with
            | Some p -> ZIndexResponse.Products [| p |]
            | None -> ZIndexResponse.Products [||]

        | ZIndexCommand.GetDoseRules -> ZIndexResponse.DoseRules state.DoseRules

        | ZIndexCommand.GetSubstances -> ZIndexResponse.Substances state.Substances

        | ZIndexCommand.GetATCGroups -> ZIndexResponse.ATCGroups state.ATCGroups

        | ZIndexCommand.Stop replyChannel ->
            replyChannel.Reply(())
            ZIndexResponse.Stopped

    response, state // state is read-only; always return unchanged


// ============================================================
// Agent factory
// ============================================================

/// Create and start a ZIndex agent pre-loaded with medication data.
let createZIndexAgent () =
    let state = loadState ()
    Agent.createStatefulReply<ZIndexCommand, ZIndexResponse, ZIndexState> (state, processCommand)


// ============================================================
// Helper: send a command and get a typed response
// ============================================================

let ask (agent: Agent<ZIndexCommand * AsyncReplyChannel<ZIndexResponse>>) cmd = agent |> Agent.postAndReply cmd


// ============================================================
// Demo — exercise the agent
// ============================================================

printfn "\n--- ZIndex Agent Demo ---\n"

let agent = createZIndexAgent ()

// 1. Query total product count
match agent |> ask ZIndexCommand.GetProducts with
| ZIndexResponse.Products ps -> printfn $"Total GenPresProducts: {ps.Length}"
| other -> printfn $"Unexpected: {other}"


// 2. Filter by generic name
match agent |> ask (ZIndexCommand.FilterByGeneric "paracetamol") with
| ZIndexResponse.Products ps ->
    printfn $"\nParacetamol products found: {ps.Length}"
    ps |> Array.truncate 3 |> Array.iter (fun p -> printfn $"  {p.Name} ({p.Form})")
| other -> printfn $"Unexpected: {other}"


// 3. Query ATC groups
match agent |> ask ZIndexCommand.GetATCGroups with
| ZIndexResponse.ATCGroups gs -> printfn $"\nATC groups: {gs.Length}"
| other -> printfn $"Unexpected: {other}"


// 4. Look up a specific GPK
match agent |> ask (ZIndexCommand.FindByGPK 2194) with
| ZIndexResponse.Products [| p |] -> printfn $"\nGPK 2194: {p.Name}"
| ZIndexResponse.Products [||] -> printfn "\nGPK 2194: not found"
| other -> printfn $"Unexpected: {other}"


// 5. Dispose agent
Agent.dispose agent
printfn "\nAgent disposed."


// ============================================================
// Notes for migration to source files
// ============================================================
//
// When migrating this design to .fs source files:
//
// 1.  Add an Agent.fs file to Informedica.ZIndex.Lib containing:
//         - ZIndexCommand (DU)
//         - ZIndexResponse (DU)
//         - ZIndexState (record)
//         - loadState (unit -> ZIndexState)
//         - processCommand (State -> Command -> Response * State)
//         - create (unit -> Agent<...>)
//
// 2.  Add ProjectReference to Informedica.Agents.Lib in ZIndex.Lib.fsproj
//
// 3.  Register the agent as a singleton in the server startup (e.g., in
//     ServerApi.fs or Program.fs).

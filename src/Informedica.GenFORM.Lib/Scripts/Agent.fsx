#I __SOURCE_DIRECTORY__

// Agent architecture demonstration for Informedica.GenFORM.Lib
//
// This script shows how to wrap the GenFORM resource provider in a
// Command/Response agent using Informedica.Agents.Lib.  It follows the
// script-based development workflow described in AGENTS.md — prototype here
// first, migrate to source once the design is approved.
//
// To run: from this directory
//   dotnet fsi Agent.fsx
//
// Pre-requisites: dotnet build GenPRES.sln

#load "load.fsx"

#r "../bin/Debug/net10.0/Informedica.GenForm.Lib.dll"
#r "../../Informedica.Agents.Lib/bin/Debug/net10.0/Informedica.Agents.Lib.dll"

open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.Logging.Lib
open Informedica.Agents.Lib


// ============================================================
// Command / Response discriminated union types
// ============================================================

/// Commands that can be sent to the GenFORM resource agent
[<RequireQualifiedAccess>]
type GenFormCommand =
    // Read access — serve from cache
    | GetDoseRules
    | GetSolutionRules
    | GetRenalRules
    | GetResourceInfo
    // Filtered access
    | FilterPrescriptionRules  of filter: PrescriptionRule.Filter
    | GetPrescriptionRules     of patient: PatientCategory
    // Cache management
    | ReloadCache


/// Responses returned by the GenFORM agent
[<RequireQualifiedAccess>]
type GenFormResponse =
    | DoseRules         of DoseRule array
    | SolutionRules     of SolutionRule array
    | RenalRules        of RenalRule array
    | ResourceInfo      of Resources.ResourceInfo
    | PrescriptionRules of Result<PrescriptionRule array, Message list>
    | CacheReloaded
    | Error             of string list


// ============================================================
// Agent state: the IResourceProvider (CachedResourceProvider)
// ============================================================

type GenFormState = IResourceProvider


// ============================================================
// Command processor — State -> Command -> Response * State
//
// The CachedResourceProvider handles its own TTL cache internally.
// The agent adds:
//   • a single async boundary for all callers
//   • an explicit ReloadCache command that replaces the lock-based reset
//   • a natural hook for audit-trail logging (see the wrapper below)
// ============================================================

let processCommand (logger: Logger) (state: GenFormState) (cmd: GenFormCommand) : GenFormResponse * GenFormState =
    let response =
        match cmd with
        | GenFormCommand.GetDoseRules ->
            GenFormResponse.DoseRules (state.GetDoseRules())

        | GenFormCommand.GetSolutionRules ->
            GenFormResponse.SolutionRules (state.GetSolutionRules())

        | GenFormCommand.GetRenalRules ->
            GenFormResponse.RenalRules (state.GetRenalRules())

        | GenFormCommand.GetResourceInfo ->
            GenFormResponse.ResourceInfo (state.GetResourceInfo())

        | GenFormCommand.FilterPrescriptionRules filter ->
            let result = Api.filterPrescriptionRules state filter
            GenFormResponse.PrescriptionRules result

        | GenFormCommand.GetPrescriptionRules patient ->
            let rules = Api.getPrescriptionRules state patient
            GenFormResponse.PrescriptionRules (Ok rules)

        | GenFormCommand.ReloadCache ->
            Api.reloadCache logger state
            GenFormResponse.CacheReloaded

    response, state   // provider reference is unchanged; its internal state may be refreshed


// ============================================================
// Agent factory
// ============================================================

/// Create and start a GenFORM agent backed by a cached resource provider.
///
/// <param name="logger">Logger for diagnostic messages</param>
/// <param name="dataUrlId">GENPRES_URL_ID environment variable value</param>
let createGenFormAgent (logger: Logger) (dataUrlId: string) =
    let provider = Api.getCachedProviderWithDataUrlId logger dataUrlId
    Agent.createStatefulReply<GenFormCommand, GenFormResponse, GenFormState>(
        provider,
        processCommand logger
    )


// ============================================================
// Optional: audit-trail wrapper
//
// Logs every command + response pair.  Requires only that the inner agent
// exists — no changes to the inner agent's logic.
// ============================================================

let withAuditLog (logger: Logger) (inner: Agent<GenFormCommand * AsyncReplyChannel<GenFormResponse>>) =
    Agent.createReply<GenFormCommand, GenFormResponse>(fun cmd ->
        Logging.logInfo logger $"[AUDIT] GenForm ← {cmd}"
        let response = inner |> Agent.postAndReply cmd
        Logging.logInfo logger $"[AUDIT] GenForm → {response}"
        response
    )


// ============================================================
// Helpers
// ============================================================

let ask (agent: Agent<GenFormCommand * AsyncReplyChannel<GenFormResponse>>) cmd =
    agent |> Agent.postAndReply cmd

let askAsync (agent: Agent<GenFormCommand * AsyncReplyChannel<GenFormResponse>>) cmd =
    agent |> Agent.postAndAsyncReply cmd


// ============================================================
// Demo — exercise the agent
// ============================================================

printfn "\n--- GenFORM Agent Demo ---\n"

Env.loadDotEnv () |> ignore

let dataUrlId = Environment.GetEnvironmentVariable "GENPRES_URL_ID"

if dataUrlId |> String.isNullOrWhiteSpace then
    printfn "GENPRES_URL_ID is not set; using demo data path"
    Environment.SetEnvironmentVariable("GENPRES_PROD", "0")

let noOpLogger = FormLogging.noOp

let agent    = createGenFormAgent noOpLogger dataUrlId
let audited  = withAuditLog noOpLogger agent


// 1. Resource info
printfn "1. Resource info:"
match audited |> ask GenFormCommand.GetResourceInfo with
| GenFormResponse.ResourceInfo info ->
    printfn $"   Loaded: {info.IsLoaded}  Last updated: {info.LastUpdated}"
| GenFormResponse.Error msgs ->
    msgs |> List.iter (eprintfn "   Error: %s")
| other ->
    printfn $"   Unexpected: {other}"


// 2. Dose rules count
printfn "\n2. Dose rules:"
match audited |> ask GenFormCommand.GetDoseRules with
| GenFormResponse.DoseRules drs ->
    printfn $"   {drs.Length} dose rules loaded"
| GenFormResponse.Error msgs ->
    msgs |> List.iter (eprintfn "   Error: %s")
| other ->
    printfn $"   Unexpected: {other}"


// 3. Solution rules count
printfn "\n3. Solution rules:"
match audited |> ask GenFormCommand.GetSolutionRules with
| GenFormResponse.SolutionRules srs ->
    printfn $"   {srs.Length} solution rules loaded"
| GenFormResponse.Error msgs ->
    msgs |> List.iter (eprintfn "   Error: %s")
| other ->
    printfn $"   Unexpected: {other}"


// 4. Filter prescription rules for a minimal patient context
printfn "\n4. Prescription rules (unfiltered — may take a moment):"

let emptyFilter = PrescriptionRule.emptyFilter

match audited |> ask (GenFormCommand.FilterPrescriptionRules emptyFilter) with
| GenFormResponse.PrescriptionRules (Ok rules) ->
    printfn $"   {rules.Length} prescription rules"
| GenFormResponse.PrescriptionRules (Error msgs) ->
    msgs |> List.iter (fun m -> eprintfn $"   Error: {m}")
| GenFormResponse.Error msgs ->
    msgs |> List.iter (eprintfn "   Error: %s")
| other ->
    printfn $"   Unexpected: {other}"


// 5. Async call example
printfn "\n5. Async call for renal rules:"

async {
    let! response = audited |> askAsync GenFormCommand.GetRenalRules
    match response with
    | GenFormResponse.RenalRules rrs ->
        printfn $"   {rrs.Length} renal rules"
    | other ->
        printfn $"   Unexpected: {other}"
}
|> Async.RunSynchronously


// 6. Dispose agents
Agent.dispose audited
Agent.dispose agent
printfn "\nAgents disposed."


// ============================================================
// Notes for migration to source files
// ============================================================
//
// When migrating to .fs source files:
//
// 1.  Add Agent.fs to Informedica.GenFORM.Lib (after Api.fs in .fsproj)
//     containing:
//         - GenFormCommand (DU)
//         - GenFormResponse (DU)
//         - processCommand
//         - create (Logger -> string -> Agent<...>)
//         - withAuditLog (Logger -> Agent<...> -> Agent<...>)
//
// 2.  Add ProjectReference to Informedica.Agents.Lib in GenFORM.Lib.fsproj
//
// 3.  In the server startup, replace:
//         let provider = Api.getCachedProviderWithDataUrlId logger dataUrlId
//     with:
//         let genFormAgent = GenFormAgent.create logger dataUrlId
//
// 4.  Replace all direct provider calls:
//         provider |> Api.filterPrescriptionRules filter
//     with agent calls:
//         genFormAgent |> Agent.postAndAsyncReply (GenFormCommand.FilterPrescriptionRules filter)

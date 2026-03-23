#I __SOURCE_DIRECTORY__

// Agent architecture demonstration for Informedica.GenORDER.Lib
//
// This script shows how to wrap the GenORDER API in a Command/Response agent
// using Informedica.Agents.Lib.  It builds on two key facts:
//
//   1. OrderContext.Command is ALREADY defined in GenORDER.Lib/Api.fs
//   2. The IServerApi.processCommand in Shared/Api.fs is the top-level
//      version of this same pattern
//
// The agent architecture generalises the existing Command DU so it is
// accessible at the library level — not just from the server.
//
// Follows the script-based development workflow from AGENTS.md.
// Prototype here first, migrate to source once the design is approved.
//
// To run: from this directory
//   dotnet fsi Agent.fsx
//
// Pre-requisites: dotnet build GenPRES.sln

#load "load.fsx"

#r "../../Informedica.Agents.Lib/bin/Debug/net10.0/Informedica.Agents.Lib.dll"

open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.GenOrder.Lib
open Informedica.Logging.Lib
open Informedica.Agents.Lib


// ============================================================
// Response type
//
// NOTE: OrderContext.Command already exists in the library.
//       We only need to define the Response side.
// ============================================================

/// Response returned by the GenORDER agent for OrderContext commands
[<RequireQualifiedAccess>]
type OrderContextResponse =
    | OrderContextResult of OrderContext
    | Error              of string list


// ============================================================
// A top-level agent command that can be extended over time
// to cover additional library concerns (formulary queries,
// nutrition plans, etc.)
// ============================================================

/// Top-level command for the GenORDER agent
[<RequireQualifiedAccess>]
type GenOrderCommand =
    | OrderCtxCmd        of OrderContext.Command
    | CreateOrderContext of Patient
    | GetIndications     of OrderContext
    | GetGenerics        of OrderContext
    | GetRoutes          of OrderContext
    | GetForms           of OrderContext
    | GetDiluents        of OrderContext


/// Top-level response from the GenORDER agent
[<RequireQualifiedAccess>]
type GenOrderResponse =
    | OrderContextResult of Result<OrderContext, string list>
    | StringArray        of string array
    | Error              of string list


// ============================================================
// Agent state: Logger + IResourceProvider
//
// The OrderContext itself is NOT part of the agent state.
// Each command carries the current OrderContext with it, which
// means the agent is stateless with respect to individual
// prescriptions — it is safe to share across multiple callers.
// ============================================================

type GenOrderState =
    {
        Logger:   Logger
        Provider: IResourceProvider
    }


// ============================================================
// Command processor
// ============================================================

/// Apply an OrderContext.Command to a context, returning the updated context.
/// This mirrors the logic in ServerApi.OrderContext.evaluate.
let applyOrderContextCmd (logger: Logger) (provider: IResourceProvider) (cmd: OrderContext.Command) : Result<OrderContext, string list> =
    let ctx = OrderContext.Command.get cmd

    // Delegate to the library functions based on command type.
    // The full implementation of each case is in ServerApi.fs.
    // This script demonstrates the pattern; migrate the logic to the
    // library when the agent design is approved.
    try
        let updatedCtx =
            match cmd with
            | OrderContext.Command.UpdateOrderContext _ ->
                // Re-derive filter and scenarios from current patient + rules
                let ctx, _ = OrderContext.getRules logger provider ctx
                ctx

            | OrderContext.Command.SelectOrderScenario _ ->
                // The client has already selected a scenario; the context is
                // returned as-is (selection state is inside the context)
                ctx

            | OrderContext.Command.UpdateOrderScenario _ ->
                // Evaluate the selected scenario against the prescription rules
                let ctx, _ = OrderContext.getRules logger provider ctx
                ctx

            | OrderContext.Command.ResetOrderScenario _ ->
                // Rebuild scenarios from scratch
                let ctx, _ = OrderContext.getRules logger provider ctx
                ctx

            // Property adjustments: currently handled in ServerApi.
            // The agent stubs them out — full migration moves the logic here.
            | _ ->
                ctx

        Ok updatedCtx
    with
    | ex ->
        Error [ ex.Message ]


let processCommand (state: GenOrderState) (cmd: GenOrderCommand) : GenOrderResponse * GenOrderState =
    let response =
        match cmd with
        | GenOrderCommand.OrderCtxCmd orderCmd ->
            let result = applyOrderContextCmd state.Logger state.Provider orderCmd
            GenOrderResponse.OrderContextResult result

        | GenOrderCommand.CreateOrderContext patient ->
            let ctx = OrderContext.create state.Logger state.Provider patient
            GenOrderResponse.OrderContextResult (Ok ctx)

        | GenOrderCommand.GetIndications ctx ->
            let indications = Filters.getIndications state.Logger state.Provider ctx.Patient
            GenOrderResponse.StringArray indications

        | GenOrderCommand.GetGenerics ctx ->
            let generics = Filters.getGenerics state.Logger state.Provider ctx.Patient
            GenOrderResponse.StringArray generics

        | GenOrderCommand.GetRoutes ctx ->
            let routes = Filters.getRoutes state.Logger state.Provider ctx.Patient
            GenOrderResponse.StringArray routes

        | GenOrderCommand.GetForms ctx ->
            let forms = Filters.getForms state.Logger state.Provider ctx.Patient
            GenOrderResponse.StringArray forms

        | GenOrderCommand.GetDiluents ctx ->
            // filterDiluents expects a GenFORM DoseFilter, construct one from the OrderContext
            let doseFilter : Informedica.GenForm.Lib.Types.DoseFilter =
                {
                    Indication = ctx.Filter.Indication
                    Generic = ctx.Filter.Generic
                    Route = ctx.Filter.Route
                    Form = ctx.Filter.Form
                    DoseType = ctx.Filter.DoseType
                    Diluent = ctx.Filter.Diluent
                    Components = ctx.Filter.SelectedComponents |> Array.toList
                    Patient = ctx.Patient
                }
            let diluents = Filters.filterDiluents state.Logger state.Provider doseFilter
            GenOrderResponse.StringArray diluents

    response, state   // state (logger + provider) is always unchanged


// ============================================================
// Agent factory
// ============================================================

/// Create and start a GenORDER agent.
///
/// <param name="logger">Logger for diagnostic output</param>
/// <param name="provider">Loaded GenFORM resource provider</param>
let createGenOrderAgent (logger: Logger) (provider: IResourceProvider) =
    let state = { Logger = logger; Provider = provider }
    Agent.createStatefulReply<GenOrderCommand, GenOrderResponse, GenOrderState>(
        state,
        processCommand
    )


// ============================================================
// Audit-trail wrapper (same pattern as GenFORM Agent.fsx)
// ============================================================

let withAuditLog (logger: Logger) (inner: Agent<GenOrderCommand * AsyncReplyChannel<GenOrderResponse>>) =
    Agent.createReply<GenOrderCommand, GenOrderResponse>(fun cmd ->
        Informedica.Logging.Lib.Logging.logInfo logger $"[AUDIT] GenOrder ← {cmd}"
        let response = inner |> Agent.postAndReply cmd
        Informedica.Logging.Lib.Logging.logInfo logger $"[AUDIT] GenOrder → {response}"
        response
    )


// ============================================================
// Helpers
// ============================================================

let ask (agent: Agent<GenOrderCommand * AsyncReplyChannel<GenOrderResponse>>) cmd =
    agent |> Agent.postAndReply cmd

let askAsync (agent: Agent<GenOrderCommand * AsyncReplyChannel<GenOrderResponse>>) cmd =
    agent |> Agent.postAndAsyncReply cmd


// ============================================================
// Demo — exercise the agent
// ============================================================

printfn "\n--- GenORDER Agent Demo ---\n"

Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_PROD", "0")

let noOpLogger = FormLogging.noOp
let dataUrlId  = Environment.GetEnvironmentVariable "GENPRES_URL_ID"

// Create resource provider (GenFORM layer)
let provider = Api.getCachedProviderWithDataUrlId noOpLogger dataUrlId

// Create the GenORDER agent
let agent   = createGenOrderAgent noOpLogger provider
let audited = withAuditLog noOpLogger agent


// 1. Create an order context for a test patient
printfn "1. Create order context for a test patient:"

let testPatient : Patient =
    {
        Patient.empty with
            Age      = Some (ValueUnit.singleWithUnit Units.Time.year 5.)
            Weight   = Some (ValueUnit.singleWithUnit Units.Weight.kiloGram 20.)
            Height   = Some (ValueUnit.singleWithUnit Units.Height.centiMeter 110.)
            Department = Some "ICU"
            Location = Some "NICU"
    }

match audited |> ask (GenOrderCommand.CreateOrderContext testPatient) with
| GenOrderResponse.OrderContextResult (Ok ctx) ->
    printfn $"   Context created for patient (department: {ctx.Patient.Department})"
    printfn $"   Available generics: {ctx.Filter.Generics.Length}"

    // 2. Get available routes for this context
    printfn "\n2. Get routes available for this patient:"
    match audited |> ask (GenOrderCommand.GetRoutes ctx) with
    | GenOrderResponse.StringArray routes ->
        let routeStr = routes |> String.concat ", "
        printfn $"   Routes: {routeStr}"
    | GenOrderResponse.Error msgs ->
        msgs |> List.iter (eprintfn "   Error: %s")
    | other ->
        printfn $"   Unexpected: {other}"

    // 3. Apply an UpdateOrderContext command (existing Command DU)
    printfn "\n3. Apply OrderContext.Command.UpdateOrderContext:"
    match audited |> ask (GenOrderCommand.OrderCtxCmd (OrderContext.Command.UpdateOrderContext ctx)) with
    | GenOrderResponse.OrderContextResult (Ok updated) ->
        printfn $"   Updated context — scenarios: {updated.Scenarios.Length}"
    | GenOrderResponse.OrderContextResult (Error msgs) ->
        msgs |> List.iter (eprintfn "   Error: %s")
    | GenOrderResponse.Error msgs ->
        msgs |> List.iter (eprintfn "   Error: %s")
    | other ->
        printfn $"   Unexpected: {other}"

| GenOrderResponse.OrderContextResult (Error msgs) ->
    msgs |> List.iter (eprintfn "Error: %s")
| GenOrderResponse.Error msgs ->
    msgs |> List.iter (eprintfn "Error: %s")
| other ->
    printfn $"Unexpected: {other}"


// 4. Async interaction
printfn "\n4. Async creation of a second context:"

async {
    let! response = audited |> askAsync (GenOrderCommand.CreateOrderContext testPatient)
    match response with
    | GenOrderResponse.OrderContextResult (Ok ctx) ->
        printfn $"   Async context: {ctx.Filter.Generics.Length} generics available"
    | other ->
        printfn $"   Unexpected: {other}"
}
|> Async.RunSynchronously


// Dispose agents
Agent.dispose audited
Agent.dispose agent
printfn "\nAgents disposed."


// ============================================================
// Connection to the existing IServerApi pattern
// ============================================================
//
// The existing Shared.Api.Command mirrors this design at the top level:
//
//     type IServerApi = {
//         processCommand: Command -> Async<Result<Response, string[]>>
//     }
//
// The GenORDER agent sits one layer below, handling only order-context
// concerns.  The server's processCommand fan-out would become:
//
//     Command.processCmd provider cmd = async {
//         match cmd with
//         | OrderContextCmd (orderCmd, sharedCtx) ->
//             let libCmd = OrderContext.Command.<...>
//             let! resp = genOrderAgent |> Agent.postAndAsyncReply libCmd
//             return resp |> mapToSharedResponse
//         | FormularyCmd formulary ->
//             ...
//     }


// ============================================================
// Notes for migration to source files
// ============================================================
//
// When migrating to .fs source files:
//
// 1.  Add Agent.fs to Informedica.GenORDER.Lib (after Api.fs in .fsproj)
//     containing:
//         - OrderContextResponse (DU)
//         - GenOrderCommand (DU)
//         - GenOrderResponse (DU)
//         - GenOrderState (record)
//         - applyOrderContextCmd (move server-side logic here)
//         - processCommand
//         - create (Logger -> IResourceProvider -> Agent<...>)
//
// 2.  GenORDER.Lib already transitively depends on Agents.Lib via Logging.Lib,
//     so no new ProjectReference is strictly required — but adding an explicit
//     one to Agents.Lib improves discoverability.
//
// 3.  The server startup becomes:
//         let genOrderAgent = GenOrderAgent.create logger provider
//
// 4.  Command.processCmd delegates to the agent:
//         let! resp = genOrderAgent |> Agent.postAndAsyncReply libCmd

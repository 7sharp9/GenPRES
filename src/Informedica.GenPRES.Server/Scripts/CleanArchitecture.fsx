/// Clean Safe Architecture demonstration script for GenPRES
///
/// This script demonstrates the implemented ports-and-adapters architecture
/// using the actual compiled types from the server project.
///
/// Architecture overview:
///   Ports (ServerApi.Ports.fs)         — FormularyPort, OrderContextPort, etc.
///   Adapters (ServerApi.Adapters.fs)   — direct adapter wiring provider+logger
///   AgentAdapters (ServerApi.AgentAdapters.fs) — MailboxProcessor-serialized adapter
///   Command (ServerApi.Command.fs)     — thin dispatcher against AppEnv ports
///   CompositionRoot (ServerApi.CompositionRoot.fs) — single wiring point
///
/// USAGE
/// -----
///   cd src/Informedica.GenPRES.Server/Scripts
///   dotnet fsi CleanArchitecture.fsx
///
/// Or via FSI MCP server:
///   #I "/path/to/src/Informedica.GenPRES.Server/Scripts"
///   #load "CleanArchitecture.fsx"

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Shared
open Shared.Types
open ServerApi


// ============================================================
// SECTION 1 — Architecture overview (implemented)
// ============================================================

printfn ""
printfn "=== 1. Implemented Clean Architecture ==="
printfn ""
printfn "The server now follows a ports-and-adapters architecture:"
printfn ""
printfn "  ServerApi.Ports.fs         — Port record types (FormularyPort, OrderContextPort, etc.)"
printfn "  ServerApi.Adapters.fs      — Direct adapters (provider + logger -> ports)"
printfn "  ServerApi.AgentAdapters.fs — Agent-backed adapters (MailboxProcessor serialization)"
printfn "  ServerApi.Command.fs       — Thin command dispatcher against AppEnv ports"
printfn "  ServerApi.CompositionRoot.fs — Single wiring point (creates IServerApi)"
printfn ""
printfn "Key properties:"
printfn "  - Command.processCmd takes AppEnv, not provider/logger"
printfn "  - All ports use uniform Async<Result<'T, string[]>> effect type"
printfn "  - requireLoaded is a simple string[] option guard in AppEnv"


// ============================================================
// SECTION 2 — Port types (from compiled ServerApi.Ports)
// ============================================================

printfn ""
printfn "=== 2. Port Types ==="
printfn ""
printfn "  FormularyPort      = { getFormulary; getParenteralia }"
printfn "  OrderContextPort   = { evaluate }"
printfn "  OrderPlanPort      = { updateOrderPlan; filterOrderPlan }"
printfn "  NutritionPlanPort  = { initNutritionPlan; addNutritionContext; ... }"
printfn "  AppEnv             = { formulary; orderContext; orderPlan; nutritionPlan; requireLoaded }"

// Verify the types are available from the compiled DLLs
let _ : FormularyPort -> unit = ignore
let _ : OrderContextPort -> unit = ignore
let _ : OrderPlanPort -> unit = ignore
let _ : NutritionPlanPort -> unit = ignore
let _ : AppEnv -> unit = ignore
printfn "  [OK] All port types resolved from compiled server"


// ============================================================
// SECTION 3 — Stub adapters for testing
// ============================================================

printfn ""
printfn "=== 3. Stub Adapters (no provider needed) ==="
printfn ""

let emptyCtx = Models.OrderContext.empty
let emptyFormulary : Formulary =
    {
        Generics = [||]; Indications = [||]; Routes = [||]; Forms = [||]
        DoseTypes = [||]; PatientCategories = [||]; Products = [||]
        Generic = None; Indication = None; Route = None; Form = None
        DoseType = None; PatientCategory = None; Patient = None; Markdown = ""
    }

let stubFormulary : FormularyPort =
    {
        getFormulary = fun form -> async { return Ok { form with Markdown = "stubbed formulary" } }
        getParenteralia = fun par -> async { return Ok par }
    }

let stubOrderContext : OrderContextPort =
    {
        evaluate = fun _cmd ctx -> async { return Ok ctx }
    }

let stubOrderPlan : OrderPlanPort =
    {
        updateOrderPlan = fun tp _ -> async { return Ok tp }
        filterOrderPlan = fun tp -> async { return Ok tp }
    }

let stubNutritionPlan : NutritionPlanPort =
    {
        initNutritionPlan = fun pat -> async { return Ok (Models.NutritionPlan.create pat [||]) }
        addNutritionContext = fun (plan, _) -> async { return Ok plan }
        removeNutritionContext = fun (plan, _) -> async { return Ok plan }
        updateNutritionOrderContext = fun (plan, _, _) -> async { return Ok plan }
        selectNutritionOrderScenario = fun (plan, _, _) -> async { return Ok plan }
        navigateNutritionOrderContext = fun (plan, _, _, _) -> async { return Ok plan }
    }

let stubEnv : AppEnv =
    {
        formulary = stubFormulary
        orderContext = stubOrderContext
        orderPlan = stubOrderPlan
        nutritionPlan = stubNutritionPlan
        requireLoaded = fun () -> None
    }

printfn "  Created stub AppEnv — no IResourceProvider or network needed"


// ============================================================
// SECTION 4 — Test command routing with stubs
// ============================================================

printfn ""
printfn "=== 4. Command Routing Test (stub) ==="
printfn ""

let testFormularyCmd () =
    let result =
        Command.processCmd stubEnv (Api.FormularyCmd emptyFormulary)
        |> Async.RunSynchronously

    match result with
    | Ok (Api.FormularyResp f) ->
        printfn $"  FormularyCmd -> Ok (Markdown = \"{f.Markdown}\")"
    | other ->
        printfn $"  FormularyCmd -> UNEXPECTED: {other}"

let testOrderContextCmd () =
    let result =
        Command.processCmd stubEnv (Api.OrderContextCmd (Api.UpdateOrderContext, emptyCtx))
        |> Async.RunSynchronously

    match result with
    | Ok (Api.OrderContextResp (Api.OrderContextResult _)) ->
        printfn "  OrderContextCmd -> Ok (OrderContextResult)"
    | other ->
        printfn $"  OrderContextCmd -> UNEXPECTED: {other}"

let testRequireLoadedGuard () =
    let notLoadedEnv =
        { stubEnv with requireLoaded = fun () -> Some [| "not ready" |] }

    let result =
        Command.processCmd notLoadedEnv (Api.FormularyCmd emptyFormulary)
        |> Async.RunSynchronously

    match result with
    | Error msgs ->
        let s = msgs |> String.concat ", "
        printfn $"  requireLoaded guard -> Error: {s}"
    | Ok _ ->
        printfn "  requireLoaded guard -> UNEXPECTED: should have returned Error"

testFormularyCmd ()
testOrderContextCmd ()
testRequireLoadedGuard ()


// ============================================================
// SECTION 5 — Error propagation test
// ============================================================

printfn ""
printfn "=== 5. Error Propagation Test ==="
printfn ""

let failingEnv =
    { stubEnv with
        formulary =
            {
                getFormulary = fun _ -> async { return Error [| "connection failed" |] }
                getParenteralia = fun _ -> async { return Error [| "connection failed" |] }
            }
    }

let testErrorPropagation () =
    let result =
        Command.processCmd failingEnv (Api.FormularyCmd emptyFormulary)
        |> Async.RunSynchronously

    match result with
    | Error msgs ->
        let s = msgs |> String.concat ", "
        printfn $"  Error propagated: {s}"
    | Ok _ ->
        printfn "  UNEXPECTED: should have returned Error"

testErrorPropagation ()


// ============================================================
// SECTION 6 — Summary
// ============================================================

printfn ""
printfn "=== 6. Architecture Summary ==="
printfn ""
printfn "  Layer              | File                          | Description"
printfn "  ──────────────────────────────────────────────────────────────────────────"
printfn "  Presentation       | IServerApi (Remoting)          | unchanged"
printfn "  Command Router     | ServerApi.Command.fs           | thin dispatcher against AppEnv"
printfn "  Ports              | ServerApi.Ports.fs             | FormularyPort, OrderContextPort, etc."
printfn "  Direct Adapters    | ServerApi.Adapters.fs          | provider+logger -> ports"
printfn "  Agent Adapters     | ServerApi.AgentAdapters.fs     | MailboxProcessor serialization"
printfn "  Services           | ServerApi.Services.fs          | Formulary, OrderContext, etc."
printfn "  Mappers            | ServerApi.Mappers.fs           | DTO conversions"
printfn "  Composition Root   | ServerApi.CompositionRoot.fs   | single wiring point"
printfn "  Domain             | GenOrder/GenForm (pure)        | unchanged"
printfn "  Infrastructure     | ResourceProvider/Cached        | unchanged"
printfn ""
printfn "All phases of the Clean Safe Architecture migration are complete."
printfn "See docs/mdr/design-history/clean-safe-architecture.md for the design document."

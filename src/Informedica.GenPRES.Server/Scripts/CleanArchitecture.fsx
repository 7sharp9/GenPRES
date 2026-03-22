/// Investigation script: Safe Clean Architecture / Tagless Final for GenPRES
///
/// This script accompanies the design document at
/// docs/mdr/design-history/clean-safe-architecture.md and demonstrates
/// how the Tagless Final pattern could be applied to the current server
/// application layer.
///
/// Reference: https://rdeneau.gitbook.io/safe-clean-architecture/domain-workflows/1-introduction/5-tagless-final
///
/// USAGE
/// -----
///   cd src/Informedica.GenPRES.Server/Scripts
///   dotnet fsi CleanArchitecture.fsx
///
/// Or, from the FSI MCP server:
///   #I "/path/to/src/Informedica.GenPRES.Server/Scripts"
///   #load "CleanArchitecture.fsx"
///

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Shared
open Shared.Types



// ============================================================
// SECTION 1 — Current architecture overview
// ============================================================
//
// The current ServerApi.fs module is the single Application layer that:
//   - Receives commands from the client (IServerApi.processCommand)
//   - Resolves dependencies (provider, logger) at call time
//   - Orchestrates domain operations (GenOrder / GenForm / etc.)
//   - Maps DTOs between the Shared API types and domain types
//
// Key observations about the current design:
//
//  (a) IResourceProvider (GenForm.Lib.Resources) is already a Port — the
//      data access layer is abstracted behind an interface.  This is a
//      strong starting point for Clean Architecture.
//
//  (b) ResourceConfig uses a record-of-functions to describe *how* each
//      resource is loaded.  This is exactly the Tagless Final style.
//
//  (c) The domain libraries (GenSolver, GenOrder, GenForm) are mostly
//      pure F# and do not carry I/O effects — they receive data through
//      parameters, which keeps the Domain layer clean.
//
//  (d) ServerApi.fs mixes several concerns into ~1 400 lines:
//       - DTO mapping (Mappers module)
//       - Application orchestration (OrderContext, Formulary, etc.)
//       - Command routing (Command module)
//       - Logging infrastructure setup (logger construction inside Command)
//
//  (e) Both logger and provider are threaded explicitly through every
//      function call.  This is effectively a manual Reader monad, but
//      without the composability benefits.

printfn ""
printfn "=== 1. Current Architecture Observations ==="
printfn ""
printfn "Strengths:"
printfn "  • IResourceProvider already acts as a data-access Port"
printfn "  • ResourceConfig uses record-of-functions (Tagless Final style)"
printfn "  • Domain libraries (GenSolver / GenOrder / GenForm) are mostly pure"
printfn "  • IServerApi defines a clean presentation-layer boundary"
printfn ""
printfn "Gaps identified:"
printfn "  • ServerApi.fs mixes DTO mapping, orchestration, routing, and infra"
printfn "  • Logger dependency is threaded manually — no composition root"
printfn "  • No application-layer port for the ordering workflow itself"
printfn "  • Effects (async / Result) are inconsistently composed"



// ============================================================
// SECTION 2 — Tagless Final pattern in F#
// ============================================================
//
// "Tagless Final" in F# means defining behaviour as a record of
// functions (an "algebra") that the application layer codes against.
// Concrete implementations are provided at the composition root.
//
// The pattern looks like this:
//
//   // Define the algebra (the port)
//   type IFormularyAlgebra<'F> = {
//       getDoseRules   : Filter -> 'F<DoseRule[]>
//       getSolutionRules : Filter -> 'F<SolutionRule[]>
//   }
//
// In F# we do not have higher-kinded types (HKTs), so the canonical
// approach is to fix the effect type.  For async + error handling the
// idiomatic F# choice is Async<Result<'T, string[]>>.
//
// For GenPRES the realistic upgrade is:
//   1. Keep the existing IResourceProvider port (it works well).
//   2. Define narrower application-level "workflow ports" as record
//      types for each application service.
//   3. Pass those workflow ports into the application functions instead
//      of passing provider + logger separately everywhere.
//   4. Wire everything together in a single Composition Root.

printfn ""
printfn "=== 2. Tagless Final pattern in F# ==="
printfn ""
printfn "In F# without HKTs, Tagless Final is best expressed as records"
printfn "of functions (ports) with a fixed effect type."
printfn ""
printfn "Recommended effect type for GenPRES application services:"
printfn "  Async<Result<'T, string[]>>"


// ============================================================
// SECTION 3 — Proposed Application-Layer Ports (record-of-functions)
// ============================================================
//
// These ports represent the abstract interface that application
// services code against.  Concrete adapters implement them using the
// existing IResourceProvider and domain libraries.
//
// NOTE: These are *prototypes* demonstrating the concept.  Migration
//       to source files is subject to user review.

/// Narrow port for formulary data access needs of the application layer.
type IFormularyPort =
    {
        /// Retrieve dose rules matching the given filter.
        getDoseRules   : Formulary -> Async<Result<Shared.Types.Formulary, string>>
        /// Retrieve solution rules (parenteral) matching the given filter.
        getParenteralia : Parenteralia -> Async<Result<Shared.Types.Parenteralia, string>>
    }

/// Narrow port for order-context evaluation.
type IOrderContextPort =
    {
        /// Evaluate an order-context command and return the updated context.
        evaluate :
            Api.OrderContextCommand
            -> OrderContext
            -> Async<Result<OrderContext, string[]>>
    }

/// Narrow port for treatment-plan (order-plan) management.
type IOrderPlanPort =
    {
        updateOrderPlan :
            OrderPlan
            -> (Api.OrderContextCommand * OrderContext) option
            -> Async<Result<OrderPlan, string[]>>

        filterOrderPlan : OrderPlan -> Async<Result<OrderPlan, string[]>>
    }

/// Narrow port for nutrition-plan management.
type INutritionPlanPort =
    {
        initNutritionPlan    : Patient -> Async<Result<NutritionPlan, string[]>>
        updateNutritionCtx   : NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        selectNutritionScen  : NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        navigateNutritionCtx : NutritionPlan * string * Api.OrderContextCommand * OrderContext -> Async<Result<NutritionPlan, string[]>>
        addNutritionCtx      : NutritionPlan * NutritionCategory -> Async<Result<NutritionPlan, string[]>>
        removeNutritionCtx   : NutritionPlan * string -> Async<Result<NutritionPlan, string[]>>
    }

/// Root environment passed into all application services — the
/// Tagless Final "environment algebra".  Wire this once at startup.
type AppEnv =
    {
        formulary    : IFormularyPort
        orderContext : IOrderContextPort
        orderPlan    : IOrderPlanPort
        nutritionPlan: INutritionPlanPort
    }

printfn ""
printfn "=== 3. Proposed Application-Layer Port types ==="
printfn ""
printfn "  IFormularyPort        — formulary & parenteral data retrieval"
printfn "  IOrderContextPort     — order-context evaluation workflow"
printfn "  IOrderPlanPort        — treatment-plan update / filter"
printfn "  INutritionPlanPort    — nutrition-plan lifecycle"
printfn "  AppEnv                — root environment (composition root input)"



// ============================================================
// SECTION 4 — Application service using AppEnv (Tagless Final style)
// ============================================================
//
// With the ports in place the Command router becomes a thin dispatcher
// that calls into the narrow ports.  It has no knowledge of provider,
// logger, or any concrete infrastructure.

/// Application-layer command handler coded against AppEnv ports.
/// This replaces the concrete provider/logger threading in Command.processCmd.
module ApplicationService =

    open Shared.Api

    let processCommand (env: AppEnv) (cmd: Command) : Async<Result<Response, string[]>> =
        match cmd with
        | OrderContextCmd (ctxCmd, ctx) ->
            async {
                let! result = env.orderContext.evaluate ctxCmd ctx
                return result |> Result.map (OrderContextResult >> OrderContextResp)
            }

        | OrderPlanCmd (UpdateOrderPlan (tp, cmdOpt)) ->
            async {
                let! result = env.orderPlan.updateOrderPlan tp cmdOpt
                return
                    result
                    |> Result.map (OrderPlanUpdated >> OrderPlanResp)
            }

        | OrderPlanCmd (FilterOrderPlan tp) ->
            async {
                let! result = env.orderPlan.filterOrderPlan tp
                return
                    result
                    |> Result.map (OrderPlanFiltered >> OrderPlanResp)
            }

        | FormularyCmd form ->
            async {
                let! result = env.formulary.getDoseRules form
                return result |> Result.mapError Array.singleton |> Result.map FormularyResp
            }

        | ParenteraliaCmd par ->
            async {
                let! result = env.formulary.getParenteralia par
                return result |> Result.mapError Array.singleton |> Result.map ParenteraliaResp
            }

        | NutritionPlanCmd (InitNutritionPlan patient) ->
            async {
                let! result = env.nutritionPlan.initNutritionPlan patient
                return result |> Result.map (NutritionPlanInitialised >> NutritionPlanResp)
            }

        | NutritionPlanCmd (UpdateNutritionOrderContext (plan, id, ctx)) ->
            async {
                let! result = env.nutritionPlan.updateNutritionCtx (plan, id, ctx)
                return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
            }

        | NutritionPlanCmd (SelectNutritionOrderScenario (plan, id, ctx)) ->
            async {
                let! result = env.nutritionPlan.selectNutritionScen (plan, id, ctx)
                return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
            }

        | NutritionPlanCmd (NavigateNutritionOrderContext (plan, id, ctxCmd, ctx)) ->
            async {
                let! result = env.nutritionPlan.navigateNutritionCtx (plan, id, ctxCmd, ctx)
                return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
            }

        | NutritionPlanCmd (AddNutritionContext (plan, category)) ->
            async {
                let! result = env.nutritionPlan.addNutritionCtx (plan, category)
                return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
            }

        | NutritionPlanCmd (RemoveNutritionContext (plan, id)) ->
            async {
                let! result = env.nutritionPlan.removeNutritionCtx (plan, id)
                return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
            }

printfn ""
printfn "=== 4. ApplicationService.processCommand ==="
printfn ""
printfn "  ApplicationService.processCommand : AppEnv -> Command -> Async<Result<Response, string[]>>"
printfn "  → no provider/logger threading; all dependencies through AppEnv ports"



// ============================================================
// SECTION 5 — Concrete Adapter: wiring AppEnv from IResourceProvider
// ============================================================
//
// The Composition Root creates the AppEnv by wiring the narrow ports
// to the concrete implementations that use IResourceProvider.
//
// This adapter layer is the only code that knows about the concrete
// infrastructure types.  It lives in the Server project (infrastructure
// layer) and is kept away from the domain and application layers.

module Adapters =

    open ServerApi
    open Informedica.Logging.Lib

    let private wrapSync f = async { return f }

    /// Build an IOrderContextPort from concrete server functions.
    let makeOrderContextPort (provider: Resources.IResourceProvider) (logger: Logger) : IOrderContextPort =
        {
            evaluate = fun ctxCmd ctx ->
                wrapSync (OrderContext.evaluate logger provider ctxCmd ctx)
        }

    /// Build an IOrderPlanPort from concrete server functions.
    //  NOTE: OrderPlan.updateOrderPlan returns OrderPlan (not Result) because it
    //  swallows errors internally (falls back to current plan on failure).
    //  The port wraps the result in Ok to give a uniform Async<Result<_,_>> surface.
    let makeOrderPlanPort (provider: Resources.IResourceProvider) (logger: Logger) : IOrderPlanPort =
        {
            updateOrderPlan = fun tp cmdOpt ->
                wrapSync (
                    OrderPlan.updateOrderPlan logger provider tp cmdOpt
                    |> OrderPlan.calculateTotals
                    |> Ok
                )
            filterOrderPlan = fun tp ->
                wrapSync (tp |> OrderPlan.calculateTotals |> Ok)
        }

    /// Build an INutritionPlanPort from concrete server functions.
    let makeNutritionPlanPort (provider: Resources.IResourceProvider) (logger: Logger) : INutritionPlanPort =
        {
            initNutritionPlan    = fun patient -> wrapSync (NutritionPlan.initNutritionPlan logger provider patient)
            updateNutritionCtx   = fun args    -> wrapSync (NutritionPlan.updateNutritionOrderContext logger provider args)
            selectNutritionScen  = fun args    -> wrapSync (NutritionPlan.selectNutritionOrderScenario logger provider args)
            navigateNutritionCtx = fun (plan, id, ctxCmd, ctx) -> wrapSync (NutritionPlan.navigateNutritionOrderContext logger provider (plan, id, ctxCmd, ctx))
            addNutritionCtx      = fun args    -> wrapSync (NutritionPlan.addNutritionContext logger provider args)
            removeNutritionCtx   = fun args    -> wrapSync (NutritionPlan.removeNutritionContext args)
        }

    /// Build an IFormularyPort from concrete server functions.
    //  NOTE: Formulary.get returns Result<Formulary, 'err>.
    //  We normalise the error to string to satisfy the port signature.
    let makeFormularyPort (provider: Resources.IResourceProvider) : IFormularyPort =
        {
            getDoseRules    = fun form -> wrapSync (Formulary.get provider form |> Result.mapError (sprintf "%A"))
            getParenteralia = fun par  -> wrapSync (Parenteralia.get provider par)
        }

    /// Create the full AppEnv for use in production.
    let makeAppEnv (provider: Resources.IResourceProvider) (logger: Logger) : AppEnv =
        {
            formulary     = makeFormularyPort provider
            orderContext  = makeOrderContextPort provider logger
            orderPlan     = makeOrderPlanPort provider logger
            nutritionPlan = makeNutritionPlanPort provider logger
        }

printfn ""
printfn "=== 5. Concrete Adapters ==="
printfn ""
printfn "  Adapters.makeAppEnv : IResourceProvider -> Logger -> AppEnv"
printfn "  → single wiring point; all concrete dependencies resolved here"



// ============================================================
// SECTION 6 — Testability improvement (stub adapter)
// ============================================================
//
// With the AppEnv ports, unit tests no longer need a real provider.
// A stub AppEnv can be constructed inline with the exact behaviour
// needed for a particular test scenario.

module StubAdapters =

    /// Stub order-context port that always succeeds with the same context.
    let orderContextPortReturnsOk (returnCtx: OrderContext) : IOrderContextPort =
        {
            evaluate = fun _cmd _ctx -> async { return Ok returnCtx }
        }

    /// Stub order-context port that always returns an error.
    let orderContextPortFails (msgs: string[]) : IOrderContextPort =
        {
            evaluate = fun _cmd _ctx -> async { return Error msgs }
        }

    /// Minimal stub AppEnv for tests that only exercise order-context commands.
    let makeMinimalStubEnv (returnCtx: OrderContext) : AppEnv =
        let notImplemented name = failwith $"StubAdapter: {name} not implemented"
        {
            formulary = {
                getDoseRules    = fun _ -> notImplemented "getDoseRules"
                getParenteralia = fun _ -> notImplemented "getParenteralia"
            }
            orderContext  = orderContextPortReturnsOk returnCtx
            orderPlan     = {
                updateOrderPlan = fun _ _ -> notImplemented "updateOrderPlan"
                filterOrderPlan = fun _   -> notImplemented "filterOrderPlan"
            }
            nutritionPlan = {
                initNutritionPlan    = fun _       -> notImplemented "initNutritionPlan"
                updateNutritionCtx   = fun _       -> notImplemented "updateNutritionCtx"
                selectNutritionScen  = fun _       -> notImplemented "selectNutritionScen"
                navigateNutritionCtx = fun _       -> notImplemented "navigateNutritionCtx"
                addNutritionCtx      = fun _       -> notImplemented "addNutritionCtx"
                removeNutritionCtx   = fun _       -> notImplemented "removeNutritionCtx"
            }
        }

printfn ""
printfn "=== 6. Stub Adapters (for unit testing) ==="
printfn ""
printfn "  StubAdapters.makeMinimalStubEnv — creates a test AppEnv with only"
printfn "  the order-context port wired; everything else raises on call."



// ============================================================
// SECTION 7 — Recommended migration plan
// ============================================================

printfn ""
printfn "=== 7. Recommended Migration Plan ==="
printfn ""
printfn "PHASE 1 — Low-risk, high-value: split ServerApi.fs into layers"
printfn "  (a) Extract Mappers module → ServerApi.Mappers.fs  (no logic, pure mapping)"
printfn "  (b) Extract Command router → ServerApi.Command.fs  (wires port calls)"
printfn "  (c) Extract Application services → ServerApi.Services.fs"
printfn "  (d) Create Composition Root → Server.CompositionRoot.fs"
printfn ""
printfn "PHASE 2 — Introduce AppEnv / ports for new features"
printfn "  (a) Define AppEnv record type in Shared or Server project"
printfn "  (b) New application services code against AppEnv ports only"
printfn "  (c) Concrete adapters live in an Adapters/ sub-module"
printfn ""
printfn "PHASE 3 — Refactor existing services to use AppEnv"
printfn "  (a) OrderContext → IOrderContextPort"
printfn "  (b) Formulary / Parenteralia → IFormularyPort"
printfn "  (c) OrderPlan → IOrderPlanPort"
printfn "  (d) NutritionPlan → INutritionPlanPort"
printfn ""
printfn "PHASE 4 — Improved testability"
printfn "  (a) Add unit tests for ApplicationService.processCommand using StubAdapters"
printfn "  (b) Remove dependence on IResourceProvider in Server test project"
printfn ""
printfn "WHAT TO LEAVE ALONE"
printfn "  • IResourceProvider — already a good port; do not change"
printfn "  • Domain libraries (GenOrder, GenForm, GenSolver) — already pure"
printfn "  • IServerApi — already a good presentation boundary"
printfn "  • Client-side Elmish MVU — out of scope"



// ============================================================
// SECTION 8 — Summary table
// ============================================================

printfn ""
printfn "=== 8. Architecture Gap Summary ==="
printfn ""
printfn "  Layer              | Current                    | Clean / TF target"
printfn "  ─────────────────────────────────────────────────────────────────────"
printfn "  Presentation       | IServerApi (Remoting)      | unchanged"
printfn "  Application        | ServerApi.fs (1 400 lines) | split: Mappers + Services + Command"
printfn "  Ports              | IResourceProvider only     | + IOrderContextPort, IOrderPlanPort, ..."
printfn "  Composition Root   | implicit in Server.fs      | explicit CompositionRoot.fs"
printfn "  Domain             | GenOrder/GenForm (pure)    | unchanged"
printfn "  Infrastructure     | ResourceProvider/Cached    | + Adapters module"
printfn "  Testability        | requires real provider     | stub AppEnv for unit tests"
printfn ""
printfn "Investigation complete.  See docs/mdr/design-history/clean-safe-architecture.md"
printfn "for the full write-up."

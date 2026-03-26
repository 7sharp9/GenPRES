# Clean Safe Architecture

**Issue**: [Safe Clean Architecture #194](https://github.com/informedica/GenPRES/issues/194)

**References**:
- <https://rdeneau.gitbook.io/safe-clean-architecture/architecture/2-principles>
- <https://blog.ploeh.dk/2020/03/02/impureim-sandwich/>
- <https://github.com/rdeneau/gitbook-safe-clean-archi>

**Date**: 2026-03-25 (updated from original 2026-03-22)

## Table of Contents

- [Summary](#summary)
- [Implemented Architecture](#implemented-architecture)
  - [Layer Overview](#layer-overview)
  - [File Layout](#file-layout)
  - [Port Types — Tagless Final Style](#port-types--tagless-final-style)
  - [Command Router](#command-router)
  - [Adapters and Agent Adapters](#adapters-and-agent-adapters)
  - [Composition Root](#composition-root)
- [Architecture Principles](#architecture-principles)
  - [Clean Safe Architecture Layers](#clean-safe-architecture-layers)
  - [Tagless Final in F#](#tagless-final-in-f)
  - [Impureim Sandwich](#impureim-sandwich)
- [Vertical Slice Architecture — Future Direction](#vertical-slice-architecture--future-direction)
  - [What Is a Vertical Slice?](#what-is-a-vertical-slice)
  - [Current State: Horizontal Layers](#current-state-horizontal-layers)
  - [Target State: Vertical Slices](#target-state-vertical-slices)
  - [Migration Strategy](#migration-strategy)
- [Testing with Stub Adapters](#testing-with-stub-adapters)
- [What Remains Unchanged](#what-remains-unchanged)
- [Architecture Status Table](#architecture-status-table)
- [Demonstration Script](#demonstration-script)

---

## Summary

The GenPRES server has been migrated from a monolithic 1 400-line `ServerApi.fs` to a Clean Safe Architecture using the **Tagless Final** pattern (records of functions as ports) and an explicit **Composition Root**.

All four migration phases are complete:

1. ✅ `ServerApi.fs` split into cohesive focused files
2. ✅ Application-layer ports introduced (`FormularyPort`, `OrderContextPort`, `OrderPlanPort`, `NutritionPlanPort`, `InteractionPort`)
3. ✅ `AppEnv` composition root wires all ports
4. ✅ Stub adapters support unit testing without any I/O

The domain libraries (`GenOrder`, `GenForm`, `GenSolver`) remain pure and unchanged.  The next architectural investigation focuses on moving towards **vertical slice architecture** — grouping each bounded domain context as a self-contained module.

---

## Implemented Architecture

### Layer Overview

```
┌──────────────────────────────────────────────────────────────────┐
│  PRESENTATION     IServerApi (Fable.Remoting)                    │
│                   ServerApi.ApiImpl.fs  — one line               │
├──────────────────────────────────────────────────────────────────┤
│  COMPOSITION ROOT ServerApi.CompositionRoot.fs                   │
│                   Wires provider → AgentAdapters → AppEnv →      │
│                   IServerApi                                     │
├──────────────────────────────────────────────────────────────────┤
│  COMMAND ROUTER   ServerApi.Command.fs                           │
│                   Pattern-matches Command DU against AppEnv      │
│                   ports; uniform Async<Result<_,string[]>>       │
├──────────────────────────────────────────────────────────────────┤
│  PORTS            ServerApi.Ports.fs                             │
│                   FormularyPort, OrderContextPort, OrderPlanPort │
│                   NutritionPlanPort, InteractionPort, AppEnv     │
├────────────────────────────┬─────────────────────────────────────┤
│  DIRECT ADAPTERS           │  AGENT ADAPTERS                     │
│  ServerApi.Adapters.fs     │  ServerApi.AgentAdapters.fs         │
│  provider + logger → ports │  MailboxProcessor per component     │
├──────────────────────────────────────────────────────────────────┤
│  APPLICATION SERVICES  ServerApi.Services.fs                     │
│  FormularyService, OrderContextService, OrderPlanService, ...    │
├──────────────────────────────────────────────────────────────────┤
│  MAPPERS           ServerApi.Mappers.fs                          │
│                   Pure DTO ↔ domain conversions                  │
├──────────────────────────────────────────────────────────────────┤
│  PORT (INFRASTRUCTURE) IResourceProvider                         │
│                   GenForm.Lib.Resources                          │
├──────────────────────────────────────────────────────────────────┤
│  DOMAIN            GenOrder.Lib  GenForm.Lib  GenSolver.Lib      │
│                    Pure functional — no I/O                      │
├──────────────────────────────────────────────────────────────────┤
│  INFRASTRUCTURE    ResourceProvider / CachedResourceProvider     │
│                    Google Sheets → CSV → domain types            │
└──────────────────────────────────────────────────────────────────┘
```

### File Layout

```
src/Informedica.GenPRES.Server/
├── Server.fs                      — entry point; creates provider; calls CompositionRoot.compose
├── ServerApi.Mappers.fs           — pure DTO ↔ domain conversions
├── ServerApi.Services.fs          — application services (FormularyService, OrderContextService, …)
├── ServerApi.Ports.fs             — port record types + AppEnv
├── ServerApi.Adapters.fs          — direct adapters: provider + logger → port records
├── ServerApi.AgentAdapters.fs     — MailboxProcessor-backed adapters (per component)
├── ServerApi.Command.fs           — thin command dispatcher; routes Command DU → AppEnv ports
├── ServerApi.CompositionRoot.fs   — single wiring point; creates IServerApi
└── ServerApi.ApiImpl.fs           — one-liner: createServerApi = CompositionRoot.compose
```

### Port Types — Tagless Final Style

All ports are F# record types (Tagless Final / algebra encoding).  Every port operation returns `Async<Result<'T, string[]>>`, giving a **uniform effect type** across the entire application layer.

```fsharp
// ServerApi.Ports.fs

type FormularyPort =
    {
        getFormulary    : Formulary -> Async<Result<Formulary, string[]>>
        getParenteralia : Parenteralia -> Async<Result<Parenteralia, string[]>>
    }

type OrderContextPort =
    { evaluate : OrderContextCommand -> OrderContext -> Async<Result<OrderContext, string[]>> }

type OrderPlanPort =
    {
        updateOrderPlan : OrderPlan -> (OrderContextCommand * OrderContext) option -> Async<Result<OrderPlan, string[]>>
        filterOrderPlan : OrderPlan -> Async<Result<OrderPlan, string[]>>
    }

type NutritionPlanPort =
    {
        initNutritionPlan            : Patient -> Async<Result<NutritionPlan, string[]>>
        addNutritionContext          : NutritionPlan * NutritionCategory -> Async<Result<NutritionPlan, string[]>>
        removeNutritionContext       : NutritionPlan * string -> Async<Result<NutritionPlan, string[]>>
        updateNutritionOrderContext  : NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        selectNutritionOrderScenario : NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        navigateNutritionOrderContext: NutritionPlan * string * OrderContextCommand * OrderContext -> Async<Result<NutritionPlan, string[]>>
    }

type InteractionPort =
    {
        checkInteractions : string list -> Async<Result<DrugInteraction list, string[]>>
        getDrugNames      : unit -> Async<Result<string list, string[]>>
    }

/// Root environment — passed to every command handler
type AppEnv =
    {
        formulary     : FormularyPort
        orderContext  : OrderContextPort
        orderPlan     : OrderPlanPort
        nutritionPlan : NutritionPlanPort
        interaction   : InteractionPort
        requireLoaded : unit -> string[] option   // guard: returns Some errors if resources not yet loaded
    }
```

### Command Router

`ServerApi.Command.fs` is the only file that touches both `AppEnv` and the `Command` discriminated union.  It is intentionally thin — it matches commands to port calls and wraps the results in the appropriate response case:

```fsharp
// ServerApi.Command.fs

let processCmd (env: AppEnv) cmd =
    match cmd with
    | InteractionCmd GetDrugNames -> ...           // allowed before load
    | _ ->
        match env.requireLoaded () with
        | Some msgs -> async { return Error msgs } // guard: resources not loaded yet
        | None ->
            match cmd with
            | OrderContextCmd(ctxCmd, ctx) ->
                async {
                    let! result = env.orderContext.evaluate ctxCmd ctx
                    return result |> Result.map (OrderContextResult >> OrderContextResp)
                }
            | FormularyCmd form -> ...
            | ...
```

### Adapters and Agent Adapters

Two adapter implementations are provided:

**Direct adapters** (`ServerApi.Adapters.fs`) — call service functions synchronously inside an `async` block.  Useful for simple cases or scripting.

**Agent adapters** (`ServerApi.AgentAdapters.fs`) — each bounded domain context gets its own `MailboxProcessor` agent:

- `FormularyAgent` — serialises formulary/parenteralia lookups
- `OrderCtxAgent` — serialises order-context evaluations
- `OrderPlanAgent` — serialises order-plan updates
- `NutritionAgent` — serialises all nutrition-plan operations
- `InteractionAgent` — serialises drug-interaction checks

Per-component agents ensure that concurrent client requests do not block each other across domain boundaries.  The `OrderContextPort` record backed by the `OrderCtxAgent` is passed into the `OrderPlanAgent` and `NutritionAgent` so that cross-component calls stay within the agent boundary.

The production `AppEnv` is always built via `AgentAdapters.makeAppEnv provider`:

```fsharp
// Server.fs → CompositionRoot.compose → AgentAdapters.makeAppEnv
let env = AgentAdapters.makeAppEnv provider
```

### Composition Root

`ServerApi.CompositionRoot.fs` is the single wiring point.  `Server.fs` creates the `provider` and calls `compose`:

```fsharp
// ServerApi.CompositionRoot.fs
let compose (provider: IResourceProvider) : IServerApi =
    let env = AgentAdapters.makeAppEnv provider
    {
        processCommand =
            fun cmd ->
                async {
                        try
                            writeInfoMessage $"Processing command: {cmd |> Shared.Api.Command.toString}"
                            let! result = Command.processCmd env cmd
                            writeInfoMessage $"Finished processing command: {cmd |> Shared.Api.Command.toString}"
                            return result
                        with ex ->
                            writeErrorMessage $"Error processing command: {cmd |> Shared.Api.Command.toString}\n{ex}"
                            return Error [| ex.Message |]
        testApi = fun () -> async { return "Hello world!" }
    }
```

---

## Architecture Principles

### Clean Safe Architecture Layers

Clean Architecture in the SAFE Stack context has five concerns:

1. **Domain Core** — pure types and business rules; no I/O.
2. **Application Services** — orchestrate workflows; depend on ports, not concrete infrastructure.
3. **Ports** — abstract interfaces (records of functions in F#) representing external capabilities.
4. **Adapters / Infrastructure** — concrete implementations of ports; depend on external systems.
5. **Composition Root** — the single place that wires everything together.

The dependency rule: inner layers must not depend on outer layers.

```
Domain Core  →  Application Services  →  Ports  ←  Adapters  ←  Composition Root
```

### Tagless Final in F#

"Tagless Final" (also called "finally tagless") is a functional programming pattern where abstract behaviours are represented as parameterised *algebras*.  In Haskell this uses type classes; in F# the idiomatic encoding is a **record of functions**.

The application layer codes against these abstract records and never imports a concrete `IResourceProvider`.  Concrete adapters implement the records at the composition root only.

Key properties:

- **Testability** — any port can be replaced with a stub record in tests.
- **Composability** — ports are plain F# values; they compose naturally.
- **Single dependency axis** — the application layer only sees the port records, not the infrastructure.
- **Uniform effect type** — `Async<Result<'T, string[]>>` for every port operation.

`ResourceConfig` in `GenForm.Lib` was already using this pattern before the server-side migration and served as the model to follow.

### Impureim Sandwich

The [Impureim Sandwich](https://blog.ploeh.dk/2020/03/02/impureim-sandwich/) (Mark Seemann, 2020) is a simple but powerful structuring principle for functional code that must interact with the real world:

```
┌─────────────────────────────────┐
│  Impure — read inputs (I/O)     │   e.g. load resources, receive HTTP command
├─────────────────────────────────┤
│  Pure   — process (domain logic)│   e.g. calculate dosage, validate constraints
├─────────────────────────────────┤
│  Impure — write outputs (I/O)   │   e.g. send HTTP response, write logs
└─────────────────────────────────┘
```

The idea is to push all I/O to the edges so that the largest possible portion of code is **pure** and therefore trivially testable.

**How GenPRES maps to the sandwich:**

| Slice | GenPRES element |
|---|---|
| Impure read | `Server.fs` creates `provider`; `CachedResourceProvider` loads Google Sheets once on startup |
| Pure core | `GenForm.Lib`, `GenOrder.Lib`, `GenSolver.Lib` — pure domain functions with no I/O |
| Application services | `OrderContextService`, `FormularyService`, etc. — orchestrate pure domain calls |
| Impure write | `AgentAdapters` posts results back through `Async`; `CompositionRoot` returns `IServerApi` over Fable.Remoting |

The port types (`FormularyPort`, `OrderContextPort`, …) form the seam between the pure core and impure edges.  Stub adapters in tests replace the impure edges with pure in-memory values, turning the entire application-layer into a pure sandwich that can be tested without I/O.

**Remaining gap**: the application-service functions (`FormularyService.get`, `OrderContextService.evaluate`, …) still accept a concrete `IResourceProvider` rather than a narrow port.  Moving these to accept their respective port records (instead of the whole provider) would push the sandwich seam inward and further isolate the pure core.

---

## Vertical Slice Architecture — Future Direction

### What Is a Vertical Slice?

Vertical Slice Architecture (Jimmy Bogard) organises code by **feature** or **bounded context** rather than by technical layer.  Each slice is a self-contained unit that owns everything it needs — types, ports, services, adapters — for one domain area.

```
Horizontal layers (current):              Vertical slices (future direction):
                                          
  ServerApi.Ports.fs ──────────────        Formulary/
  ServerApi.Services.fs ───────────          Formulary.Ports.fs
  ServerApi.Adapters.fs ───────────          Formulary.Services.fs
  ServerApi.AgentAdapters.fs ──────          Formulary.Adapters.fs
                                             Formulary.AgentAdapter.fs
                                          
                                          OrderContext/
                                            OrderContext.Ports.fs
                                            OrderContext.Services.fs
                                            OrderContext.Adapters.fs
                                            OrderContext.AgentAdapter.fs
                                          
                                          OrderPlan/
                                            ...
                                          
                                          NutritionPlan/
                                            ...
                                          
                                          Interaction/
                                            ...
```

### Current State: Horizontal Layers

The implemented architecture is already well-structured but follows a horizontal layer model:

- All ports live in one file (`ServerApi.Ports.fs`)
- All services live in one file (`ServerApi.Services.fs`)
- All adapters live in two files (`ServerApi.Adapters.fs`, `ServerApi.AgentAdapters.fs`)

The five bounded contexts — **Formulary**, **OrderContext**, **OrderPlan**, **NutritionPlan**, **Interaction** — each have their own port records and agents, but they are spread horizontally across the layer files.

### Target State: Vertical Slices

A modular vertical-slice layout would co-locate each bounded context's concerns:

```
src/Informedica.GenPRES.Server/
├── Server.fs
├── ServerApi.ApiImpl.fs          — entry point, unchanged
├── ServerApi.CompositionRoot.fs  — assembles slices, unchanged
├── ServerApi.Command.fs          — routes Command DU to slice ports, unchanged
│
├── Slices/
│   ├── Formulary/
│   │   ├── FormularyPorts.fs       — port types
│   │   ├── FormularyServices.fs    — pure orchestration
│   │   ├── FormularyAdapters.fs    — direct and agent adapters
│   │   └── FormularyMappers.fs     — DTO conversions (optional)
│   │
│   ├── OrderContext/
│   │   ├── OrderContextPorts.fs
│   │   ├── OrderContextServices.fs
│   │   └── OrderContextAdapters.fs
│   │
│   ├── OrderPlan/
│   │   ├── OrderPlanPorts.fs
│   │   ├── OrderPlanServices.fs
│   │   └── OrderPlanAdapters.fs
│   │
│   ├── NutritionPlan/
│   │   ├── NutritionPlanPorts.fs
│   │   ├── NutritionPlanServices.fs
│   │   └── NutritionPlanAdapters.fs
│   │
│   └── Interaction/
│       ├── InteractionPorts.fs
│       ├── InteractionServices.fs
│       └── InteractionAdapters.fs
│
└── ServerApi.Ports.fs            — re-exports AppEnv (assembly point for all slice ports)
```

Each slice would be:

- **Independently testable** — stub only that slice's port record.
- **Independently deployable** (in theory) — the bounded context owns its full stack.
- **Minimal coupling** — the only cross-slice dependency is the `OrderContextPort` that `OrderPlan` and `NutritionPlan` call into (already handled via injection today).

### Migration Strategy

The migration to vertical slices can be done **incrementally** without changing any behaviour:

1. **Extract one slice at a time** — start with `Interaction` (simplest; no cross-slice dependencies).
2. **Move port type into the slice folder** — `InteractionPort` moves to `Slices/Interaction/InteractionPorts.fs`.
3. **Move services and adapter logic** — move the relevant private functions from `ServerApi.Services.fs` and `ServerApi.AgentAdapters.fs`.
4. **Re-export from the top-level files** — the slice types are re-exported from `ServerApi.Ports.fs` / `ServerApi.AgentAdapters.fs` during transition to keep `ServerApi.Command.fs` and `CompositionRoot.fs` unchanged.
5. **Repeat for each bounded context** — Formulary, OrderContext, OrderPlan, NutritionPlan.
6. **Remove the top-level shim files** once all slices are extracted.

**Risk**: Low — each step is a pure refactoring (move code, keep behaviour).
**Benefit**: Each bounded context can be reviewed, tested, and evolved independently.

---

## Testing with Stub Adapters

With `AppEnv` in place, any application-layer test can build a minimal stub environment — no `IResourceProvider`, no network, no Google Sheets:

```fsharp
let stubEnv: AppEnv =
    {
        formulary =
            { getFormulary    = fun form -> async { return Ok { form with Markdown = "stubbed" } }
              getParenteralia = fun par  -> async { return Ok par } }

        orderContext =
            { evaluate = fun _cmd ctx -> async { return Ok ctx } }

        orderPlan =
            { updateOrderPlan = fun tp _   -> async { return Ok tp }
              filterOrderPlan = fun tp     -> async { return Ok tp } }

        nutritionPlan =
            { initNutritionPlan             = fun pat  -> async { return Ok (NutritionPlan.create pat [||]) }
              addNutritionContext            = fun (p,_)-> async { return Ok p }
              removeNutritionContext         = fun (p,_)-> async { return Ok p }
              updateNutritionOrderContext    = fun (p,_,_) -> async { return Ok p }
              selectNutritionOrderScenario   = fun (p,_,_) -> async { return Ok p }
              navigateNutritionOrderContext  = fun (p,_,_,_) -> async { return Ok p } }

        interaction =
            { checkInteractions = fun _ -> async { return Ok [] }
              getDrugNames       = fun () -> async { return Ok [] } }

        requireLoaded = fun () -> None
    }
```

The `requireLoaded` guard can be tested independently:

```fsharp
let notLoadedEnv = { stubEnv with requireLoaded = fun () -> Some [| "resources not ready" |] }
// Any command (except GetDrugNames) should return Error
```

A working demonstration is in `src/Informedica.GenPRES.Server/Scripts/CleanArchitecture.fsx`.

---

## What Remains Unchanged

| Area | Reason |
|---|---|
| `IResourceProvider` | Good port; well-tested; used only inside adapters |
| Domain libraries (`GenOrder`, `GenForm`, `GenSolver`) | Already pure; no changes planned |
| `IServerApi` / `Shared.Api` | Clean presentation boundary via Fable.Remoting; no changes |
| Client-side Elmish MVU | Out of scope |
| `ResourceConfig` record-of-functions | Already Tagless Final style; model for the ports |

---

## Architecture Status Table

| Layer | Before migration | After migration (current) | Next step |
|---|---|---|---|
| Presentation | `IServerApi` (Remoting) | unchanged | unchanged |
| Application entry | `ServerApi.fs` — 1 400 lines | `ServerApi.ApiImpl.fs` — 1 line | unchanged |
| Command Router | buried in `ServerApi.fs` | `ServerApi.Command.fs` | move per-slice as slices are extracted |
| Ports | none (only `IResourceProvider`) | `ServerApi.Ports.fs` — 5 port records + `AppEnv` | move into slice folders |
| Direct Adapters | inline in `ServerApi.fs` | `ServerApi.Adapters.fs` | move into slice folders |
| Agent Adapters | none | `ServerApi.AgentAdapters.fs` — one agent per bounded context | move into slice folders |
| Application Services | inline in `ServerApi.fs` | `ServerApi.Services.fs` | move into slice folders |
| Mappers | inline in `ServerApi.fs` | `ServerApi.Mappers.fs` | unchanged or per-slice |
| Composition Root | implicit in `Server.fs` | `ServerApi.CompositionRoot.fs` | unchanged |
| Domain | `GenOrder`/`GenForm` (pure) | unchanged | unchanged |
| Infrastructure | `ResourceProvider`/`CachedResourceProvider` | unchanged | unchanged |
| Testability | requires full `IResourceProvider` | stub `AppEnv` for unit tests | per-slice stub records |

---

## Demonstration Script

A working demonstration of the implemented architecture is in:

```
src/Informedica.GenPRES.Server/Scripts/CleanArchitecture.fsx
```

The script:

1. Verifies that all port types resolve from the compiled server DLLs.
2. Builds a full stub `AppEnv` with no I/O.
3. Routes `FormularyCmd`, `OrderContextCmd`, and `NutritionPlanCmd` through `Command.processCmd` using the stubs.
4. Tests the `requireLoaded` guard.
5. Tests error propagation from a failing port.

Run it with:

```bash
cd src/Informedica.GenPRES.Server/Scripts
dotnet fsi CleanArchitecture.fsx
```

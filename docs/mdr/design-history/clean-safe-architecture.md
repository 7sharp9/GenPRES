# Clean Safe Architecture Investigation

**Issue**: [Safe Clean Architecture](https://github.com/informedica/GenPRES/issues) — Investigate requirements for adopting a Clean Safe Architecture with Tagless Final style.

**Reference**: <https://rdeneau.gitbook.io/safe-clean-architecture/domain-workflows/1-introduction/5-tagless-final>

**Date**: 2026-03-22

**Issue**: [Safe Clean Architecture #194](https://github.com/informedica/GenPRES/issues/194) — Investigate requirements for adopting a Clean Safe Architecture with Tagless Final style.

## Table of Contents

- [Summary](#summary)
- [Current Architecture](#current-architecture)
  - [Strengths](#strengths)
  - [Gaps](#gaps)
- [Clean Safe Architecture Principles](#clean-safe-architecture-principles)
  - [Tagless Final in F#](#tagless-final-in-f)
- [Gap Analysis](#gap-analysis)
- [Recommended Changes](#recommended-changes)
  - [Phase 1 — Split ServerApi.fs into cohesive layers](#phase-1--split-serverapifs-into-cohesive-layers)
  - [Phase 2 — Introduce Application-Layer Ports](#phase-2--introduce-application-layer-ports)
  - [Phase 3 — Wire via a single Composition Root](#phase-3--wire-via-a-single-composition-root)
  - [Phase 4 — Improve test coverage with stub adapters](#phase-4--improve-test-coverage-with-stub-adapters)
- [What to Leave Alone](#what-to-leave-alone)
- [Architecture Gap Table](#architecture-gap-table)
- [Prototype](#prototype)

---

## Summary

GenPRES already has several Clean Architecture elements in place — notably `IResourceProvider` as a data-access port and pure-functional domain libraries.  The main improvement opportunity is in the **Application layer** (`ServerApi.fs`), which currently mixes DTO mapping, orchestration logic, command routing, and infrastructure setup in a single 1 400-line file.

Adopting the **Tagless Final** style means introducing narrow *application-layer port* types (records of functions) for each workflow area, coding the application services against those abstractions, and resolving the concrete dependencies once in a dedicated **Composition Root**.

The changes are **incremental and low-risk** — they do not touch the domain libraries or the client side.

---

## Current Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  PRESENTATION     IServerApi (Fable.Remoting)                │
├──────────────────────────────────────────────────────────────┤
│  APPLICATION      ServerApi.fs  (1 400 lines)                │
│                   • Mappers — DTO ↔ domain conversions        │
│                   • OrderContext, Formulary, OrderPlan, ...   │
│                   • Command — command router                  │
│                   • ApiImpl — creates IServerApi instance     │
│                   Dependencies: provider + logger threaded    │
│                   through every function call                 │
├──────────────────────────────────────────────────────────────┤
│  PORT             IResourceProvider (GenForm.Lib.Resources)  │
├──────────────────────────────────────────────────────────────┤
│  DOMAIN           GenOrder.Lib  GenForm.Lib  GenSolver.Lib   │
│                   (mostly pure functional, no I/O)           │
├──────────────────────────────────────────────────────────────┤
│  INFRASTRUCTURE   ResourceProvider / CachedResourceProvider  │
│                   (Google Sheets → CSV → domain types)       │
└──────────────────────────────────────────────────────────────┘
```

### Strengths

| Element | Why it is good |
|---|---|
| `IResourceProvider` | Already a well-defined Port; abstracts all resource loading |
| `ResourceConfig` | Uses record-of-functions — this *is* Tagless Final style |
| Domain libraries | Pure F# with no I/O side effects; highly testable |
| `IServerApi` | Clean presentation-layer boundary via Fable.Remoting |
| `Shared.Api.Command` discriminated union | Explicit command model; aligns with CQRS |

### Gaps

| Gap | Impact |
|---|---|
| `ServerApi.fs` is a 1 400-line monolith mixing 4+ concerns | Hard to navigate, test, and extend |
| `logger` and `provider` are threaded through every function | Fragile, verbose; no single wiring point |
| No narrow application-layer ports (only `IResourceProvider`) | Cannot substitute parts for testing without a full provider |
| Effects (`async`/`Result`) composed inconsistently | Some functions return `Result`, others `Async<Result>`, mixing convention |
| No explicit Composition Root | Dependency resolution is scattered across `Server.fs` and `ServerApi.fs` |

---

## Clean Safe Architecture Principles

Clean Architecture in the SAFE Stack context has five concerns:

1. **Domain Core** — pure types and business rules; no I/O.
2. **Application Services** — orchestrate workflows; depend on ports, not concrete infrastructure.
3. **Ports** — abstract interfaces (records of functions in F#) representing external capabilities.
4. **Adapters / Infrastructure** — concrete implementations of ports; depend on external systems.
5. **Composition Root** — the single place that wires everything together.

The dependency rule: inner layers must not depend on outer layers.

```
Domain Core  ←  Application Services  ←  Ports  ←  Adapters  ←  Composition Root
```

### Tagless Final in F#

"Tagless Final" (also called "finally tagless" or the "free monad lite") is a functional programming pattern where abstract behaviours are represented as parameterised *algebras*.  In Haskell this uses type classes; in F# the idiomatic encoding is a **record of functions**:

```fsharp
// The "algebra" — a port — expressed as a record of functions
type IOrderContextPort =
    {
        evaluate :
            Api.OrderContextCommand
            -> OrderContext
            -> Async<Result<OrderContext, string[]>>
    }
```

The application layer codes against these abstract records and never touches a concrete `IResourceProvider`.  Concrete adapters implement the records at the composition root.

The key properties:

- **Testability** — any port can be replaced with a stub record in tests.
- **Composability** — ports are plain F# values; they compose naturally.
- **Single dependency axis** — the application layer only sees the port records, not the infrastructure.

---

## Gap Analysis

| Concern | Current state | Target state |
|---|---|---|
| Application layer size | 1 file, 1 400 lines | 3–4 focused files |
| Application-layer ports | None (only `IResourceProvider`) | `IOrderContextPort`, `IFormularyPort`, `IOrderPlanPort`, `INutritionPlanPort` |
| Effect type consistency | Mixed (`Result` / `Async<Result>`) | Uniform `Async<Result<'T, string[]>>` for all ports |
| Composition Root | Implicit, scattered | Explicit `CompositionRoot.fs` |
| Testability at application layer | Requires real provider | Stub `AppEnv` with in-memory port implementations |
| Domain / Infrastructure coupling | `GenOrder.Api` takes `provider` directly | Already thin — no change needed |

---

## Recommended Changes

### Phase 1 — Split `ServerApi.fs` into cohesive layers

Split the existing file without changing any behaviour:

```
src/Informedica.GenPRES.Server/
├── ServerApi.Mappers.fs      ← extracted Mappers module (pure DTO conversions)
├── ServerApi.Services.fs     ← extracted application-service modules (OrderContext, Formulary, etc.)
├── ServerApi.Command.fs      ← extracted Command router
└── ServerApi.ApiImpl.fs      ← extracted IServerApi implementation + ApiImpl
```

**Risk**: Low — pure refactoring, no logic changes.
**Benefit**: Navigability, clearer ownership, smaller review surface per file.

### Phase 2 — Introduce Application-Layer Ports

Define a set of narrow application-level port types as F# record types.  A prototype is provided in `src/Informedica.GenPRES.Server/Scripts/CleanArchitecture.fsx`.

```fsharp
type IOrderContextPort =
    { evaluate : Api.OrderContextCommand -> OrderContext -> Async<Result<OrderContext, string[]>> }

type IFormularyPort =
    { getDoseRules    : Formulary -> Async<Result<Formulary, string>>
      getParenteralia : Parenteralia -> Async<Result<Parenteralia, string>> }

type IOrderPlanPort =
    { updateOrderPlan : OrderPlan -> (Api.OrderContextCommand * OrderContext) option -> Async<Result<OrderPlan, string[]>>
      filterOrderPlan : OrderPlan -> Async<Result<OrderPlan, string[]>> }

type INutritionPlanPort = { ... }

/// Root environment — the "Reader environment" for all application services
type AppEnv =
    { formulary    : IFormularyPort
      orderContext : IOrderContextPort
      orderPlan    : IOrderPlanPort
      nutritionPlan: INutritionPlanPort }
```

The `ApplicationService.processCommand` function then takes `AppEnv` instead of `provider + logger`:

```fsharp
let processCommand (env: AppEnv) (cmd: Command) : Async<Result<Response, string[]>>
```

**Risk**: Medium — changes the signature of the command processor.
**Benefit**: Clear seam for testing; no concrete infrastructure in application code.

### Phase 3 — Wire via a single Composition Root

Create `Server/CompositionRoot.fs` (or extend `Server.fs`) that:

1. Creates `provider` (already done in `Server.fs`).
2. Creates a `Logger` (currently recreated per-command in `Command.processCmd`).
3. Creates the concrete adapters (`makeOrderContextPort`, etc.).
4. Assembles `AppEnv`.
5. Creates `IServerApi` using `AppEnv`.

```fsharp
// CompositionRoot.fs
let compose (dataUrlId: string) : IServerApi =
    let logger   = resolveLogger ()
    let provider = GenForm.Lib.Api.getCachedProviderWithDataUrlId logger dataUrlId
    let env      = Adapters.makeAppEnv provider logger
    { processCommand = ApplicationService.processCommand env
      testApi        = fun () -> async { return "Hello world!" } }
```

**Risk**: Low — isolates wiring; reduces hidden coupling.
**Benefit**: Single place to read the dependency graph; easier onboarding.

### Phase 4 — Improve test coverage with stub adapters

With `AppEnv` in place, any application-layer test can build a minimal stub environment:

```fsharp
let stubEnv returnCtx =
    { orderContext  = { evaluate = fun _cmd _ctx -> async { return Ok returnCtx } }
      formulary     = { getDoseRules = fun _ -> failwith "not stubbed"
                        getParenteralia = fun _ -> failwith "not stubbed" }
      orderPlan     = { ... }
      nutritionPlan = { ... } }

testAsync "order context command returns Ok" {
    let env    = stubEnv someContext
    let cmd    = OrderContextCmd (UpdateOrderContext, someContext)
    let! resp  = ApplicationService.processCommand env cmd
    resp |> Expect.isOk "should succeed"
}
```

**Risk**: None — additive only.
**Benefit**: Fast, isolated application-layer tests with no I/O or resource loading.

---

## What to Leave Alone

| Area | Reason |
|---|---|
| `IResourceProvider` | Already a good port; well-tested; do not replace |
| Domain libraries (`GenOrder`, `GenForm`, `GenSolver`) | Already pure; no changes needed |
| `IServerApi` / `Shared.Api` | Clean presentation boundary; no changes needed |
| Client-side Elmish MVU | Out of scope; this investigation focuses on the server |
| `ResourceConfig` record-of-functions | Already Tagless Final style; a model to follow |

---

## Architecture Gap Table

| Layer | Current | Clean Safe Architecture target |
|---|---|---|
| Presentation | `IServerApi` (Remoting) | unchanged |
| Application | `ServerApi.fs` — 1 file, 1 400 lines | split into 3–4 focused files |
| Ports | `IResourceProvider` only | `IResourceProvider` + 4 application-level ports |
| Composition Root | implicit in `Server.fs` | explicit `CompositionRoot.fs` |
| Domain | `GenOrder`/`GenForm` (mostly pure) | unchanged |
| Infrastructure | `ResourceProvider`/`CachedResourceProvider` | + narrow `Adapters` module |
| Testability | requires full `IResourceProvider` | stub `AppEnv` for unit tests |

---

## Prototype

A working prototype of the patterns described in this document is provided in:

```
src/Informedica.GenPRES.Server/Scripts/CleanArchitecture.fsx
```

The script demonstrates:

1. Current architecture observations (Section 1)
2. Tagless Final record-of-functions encoding (Section 2)
3. Proposed `AppEnv` port types (Section 3)
4. `ApplicationService.processCommand` coded against `AppEnv` (Section 4)
5. Concrete adapters wiring `AppEnv` from `IResourceProvider` (Section 5)
6. Stub adapters for unit testing (Section 6)
7. Phased migration plan (Section 7)
8. Summary gap table (Section 8)

The prototype uses `#load "load.fsx"` to bring in the compiled domain DLLs and existing `ServerApi.fs` modules, so all type signatures compile against the actual codebase types.

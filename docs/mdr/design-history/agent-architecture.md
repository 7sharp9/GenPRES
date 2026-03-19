# Agent Architecture Design

## Overview

This document describes the proposed agent architecture for the GenPRES domain libraries. The goal is to provide a consistent **Command → Response** pattern across all domain-specific libraries, building on the infrastructure already present in `Informedica.Agents.Lib` and mirroring the existing `IServerApi.processCommand` design used in the Server/Client communication layer.

### Motivation

Currently the core domain libraries expose their services through direct function calls in an `Api.fs` module. While functional, this has several limitations:

- **No async boundary** — callers are coupled to synchronous execution
- **No audit trail** — there is no built-in record of what was requested
- **Hard to extend** — adding a new operation requires changing multiple call sites
- **No event sourcing** — commands and results are not naturally logged

A **MailboxProcessor-based agent** wrapping a **Command DU** solves all of these.

### Existing Pattern

The Server/Client communication in `Informedica.GenPRES.Shared/Api.fs` already uses this pattern:

```fsharp
type IServerApi =
    {
        processCommand: Command -> Async<Result<Response, string[]>>
    }
```

The `Informedica.GenORDER.Lib/Api.fs` `OrderContext` module already defines an internal `Command` DU (lines 213–243). The agent architecture generalises this pattern to all domain libraries.

---

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                   Client / Caller                           │
└──────────────────────────┬──────────────────────────────────┘
                           │  Command (DU value)
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                 Library Agent                               │
│   Agent<Command * AsyncReplyChannel<Response>>              │
│   Built on Informedica.Agents.Lib.Agent<'T>                 │
│                                                             │
│  State: IResourceProvider / loaded data / Logger            │
└──────────────────────────┬──────────────────────────────────┘
                           │  delegates to
                           ▼
┌─────────────────────────────────────────────────────────────┐
│               Library API functions                         │
│   (Api module, resource provider methods, etc.)             │
└──────────────────────────┬──────────────────────────────────┘
                           │  Response (DU value)
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                   Client / Caller                           │
└─────────────────────────────────────────────────────────────┘
```

### Building Blocks

The `Informedica.Agents.Lib` already provides all the required infrastructure:

| Helper | Use case |
|--------|----------|
| `Agent.createSimple<'T>` | Fire-and-forget logging/events |
| `Agent.createStateful<'T,'S>` | State mutation without replies |
| `Agent.createReply<'Req,'Rep>` | Stateless request–reply |
| `Agent.createStatefulReply<'Req,'Rep,'S>` | **Primary pattern** — stateful request–reply |
| `Agent.postAndReply` | Synchronous call |
| `Agent.postAndAsyncReply` | Async call |

The **primary pattern** for domain library agents is `createStatefulReply`, where:

- `'Request` = the library's `Command` DU
- `'Reply`   = the library's `Response` DU
- `'State`   = the library's runtime state (e.g. `IResourceProvider`, loaded data cache)

---

## Library-Specific Designs

### 1. Informedica.ZIndex.Lib

**Purpose**: Low-level medication product database (G-Standaard raw data).

**State**: Pre-loaded product and dose rule data (loaded once, immutable).

```fsharp
[<RequireQualifiedAccess>]
type Command =
    | GetProducts                          // all GenPresProducts
    | FilterByGeneric of generic: string   // products matching a generic name
    | FilterByATC of atcCode: string       // products with a given ATC prefix
    | GetDoseRules                         // all dose rules
    | GetSubstances                        // all substances
    | GetATCGroups                         // all ATC groups

[<RequireQualifiedAccess>]
type Response =
    | Products of GenPresProduct array
    | DoseRules of DoseRule array
    | Substances of Substance array
    | ATCGroups of ATCGroup array
    | Error of string

type State =
    {
        Products: GenPresProduct array
        DoseRules: DoseRule array
        Substances: Substance array
        ATCGroups: ATCGroup array
    }
```

**Usage**:
```fsharp
let agent = ZIndex.Agent.create ()

let resp =
    agent
    |> Agent.postAndReply (Command.FilterByGeneric "paracetamol")

match resp with
| Response.Products prods -> printfn $"Found {prods.Length} products"
| Response.Error msg      -> eprintfn $"Error: {msg}"
| _                       -> ()
```

---

### 2. Informedica.ZForm.Lib

**Purpose**: G-Standaard structured dosing rules (`GStand`).

**State**: ZIndex products (delegates down to ZIndex data).

```fsharp
[<RequireQualifiedAccess>]
type Command =
    | GetDoseRules of config: GStandConfig * generic: string * form: string * route: string
    | GetDoseRulesForPatient of config: GStandConfig * patient: Patient * generic: string * form: string * route: string
    | GetSubstanceDoses of config: GStandConfig * patient: Patient * generic: string * form: string * route: string

[<RequireQualifiedAccess>]
type Response =
    | DoseRules of DoseRule seq
    | SubstanceDoses of SubstanceDose seq
    | Error of string
```

**Usage**:
```fsharp
let agent = ZForm.Agent.create ()

let config = { GPKs = []; IsRate = false; SubstanceUnit = None; TimeUnit = None }

let resp =
    agent
    |> Agent.postAndReply
        (Command.GetDoseRules (config, "paracetamol", "zetpil", "rectaal"))
```

---

### 3. Informedica.GenFORM.Lib

**Purpose**: Operational knowledge — prescription rules, dose rules, solution rules, renal rules. Already implements a `CachedResourceProvider` with TTL-based caching.

**State**: `IResourceProvider` (wraps `CachedResourceProvider`).

```fsharp
[<RequireQualifiedAccess>]
type Command =
    | GetDoseRules
    | GetSolutionRules
    | GetRenalRules
    | GetResourceInfo
    | FilterDoseRules        of filter: DoseRule.Filter
    | FilterPrescriptionRules of filter: PrescriptionRule.Filter
    | GetPrescriptionRules   of patient: PatientCategory
    | ReloadCache

[<RequireQualifiedAccess>]
type Response =
    | DoseRules             of DoseRule array
    | SolutionRules         of SolutionRule array
    | RenalRules            of RenalRule array
    | ResourceInfo          of Resources.ResourceInfo
    | FilteredDoseRules     of DoseRule array                               // filter is pure — never fails
    | PrescriptionRules     of Result<PrescriptionRule array, Message list>
    | CacheReloaded
    | Error                 of string list
```

**Agent creation**:
```fsharp
let create (logger: Logger) (dataUrlId: string) =
    let provider = Api.getCachedProviderWithDataUrlId logger dataUrlId
    Agent.createStatefulReply<Command, Response, IResourceProvider>(
        provider,
        fun state cmd ->
            match cmd with
            | GetDoseRules           -> Response.DoseRules (state.GetDoseRules()), state
            | GetSolutionRules       -> Response.SolutionRules (state.GetSolutionRules()), state
            | GetRenalRules          -> Response.RenalRules (state.GetRenalRules()), state
            | GetResourceInfo        -> Response.ResourceInfo (state.GetResourceInfo()), state
            | FilterDoseRules f      ->
                // filterDoseRules is a pure projection — remove the spurious Ok wrapper
                let res = Api.filterDoseRules state f (state.GetDoseRules())
                Response.FilteredDoseRules res, state
            | FilterPrescriptionRules f ->
                let res = Api.filterPrescriptionRules state f
                Response.PrescriptionRules res, state
            | GetPrescriptionRules p ->
                // getPrescriptionRules returns Result<_,_> — pass it through directly
                let res = Api.getPrescriptionRules state p
                Response.PrescriptionRules res, state
            | ReloadCache ->
                // Wrap I/O in try/with so the reply channel is always answered
                let response =
                    try
                        Api.reloadCache logger state
                        Response.CacheReloaded
                    with ex ->
                        Response.Error [ ex.Message ]
                response, state
    )
```

**Benefits**: The agent serialises all access to the provider, removing the need for the explicit `lock` inside `CachedResourceProvider`. The TTL cache can be fully managed by a `Reload` command instead of expiry logic.

---

### 4. Informedica.GenORDER.Lib

**Purpose**: Clinical order management — scenarios, evaluation, order context navigation.

The `OrderContext.Command` DU already exists in this library (lines 213–243 of `Api.fs`). The agent wraps that DU.

**State**: `Logger * IResourceProvider` (resources + logging, no order state — each command carries its `OrderContext`).

```fsharp
// Command DU already defined:
// type OrderContext.Command = UpdateOrderContext | SelectOrderScenario | ...

[<RequireQualifiedAccess>]
type Response =
    | OrderContextResult of Result<OrderContext, string list>
    | Error of string list

type State =
    {
        Logger:   Logger
        Provider: IResourceProvider
    }
```

**Agent creation**:
```fsharp
let create (logger: Logger) (provider: IResourceProvider) =
    let state = { Logger = logger; Provider = provider }
    Agent.createStatefulReply<OrderContext.Command, Response, State>(
        state,
        fun state cmd ->
            let ctx = OrderContext.Command.get cmd
            let result =
                ctx
                |> OrderContext.evaluate state.Logger state.Provider cmd
                |> Result.mapError (fun m -> [m])
            Response.OrderContextResult result, state
    )
```

---

## Integration with the Server/Client Architecture

The existing `IServerApi.processCommand: Command -> Async<Result<Response, string[]>>` is the top-level version of this pattern. The server-side agents can be plugged in directly:

```
Client
  │  Shared.Api.Command (via Fable.Remoting)
  ▼
GenPRES.Server
  │  creates / holds agent singletons (one per library)
  ├──► GenFORM Agent   (IResourceProvider)
  ├──► GenORDER Agent  (Logger + IResourceProvider)
  └──► ...
       │  LibraryCommand → LibraryResponse
       ▼
   Library API functions
```

The server currently calls library functions directly. Migrating to agents:

1. **Initialise** one agent per library at server startup (replacing `getCachedProviderWithDataUrlId` calls)
2. **Replace** direct function calls with `Agent.postAndAsyncReply` calls
3. **Log** each command/response pair for the audit trail

---

## Event Sourcing

Each command sent to an agent is a discrete, serialisable event. Enabling an audit trail requires only that commands are logged before or after dispatch:

```fsharp
let auditedAgent innerAgent =
    Agent.createReply<Command, Response>(fun cmd ->
        writeInfoMessage $"[AUDIT] {cmd |> Command.toString}"
        // Wrap in try/with: if postAndReply throws (e.g. timeout), we still reply
        // so the caller is never left blocked on an unanswered reply channel.
        try
            let response = innerAgent |> Agent.postAndReply cmd
            writeInfoMessage $"[AUDIT] response received"
            response
        with ex ->
            writeInfoMessage $"[AUDIT] error: {ex.Message}"
            Response.Error [ ex.Message ]
    )
```

Because every operation passes through the agent, a full event log can be reconstructed later to replay state or debug issues.

---

## Async Communication

All agent interactions support async via `Agent.postAndAsyncReply`:

```fsharp
let getFormRules filter = async {
    let! response =
        genFormAgent
        |> Agent.postAndAsyncReply (Command.FilterPrescriptionRules filter)
    return
        match response with
        | Response.PrescriptionRules (Ok rules) -> rules
        | _ -> [||]
}
```

This fits naturally into the `Async<Result<_,_>>` signatures already used in `IServerApi`.

---

## Libraries Excluded from This Pattern

As stated in the issue, the following libraries are **not** covered by this pattern:

| Library | Reason |
|---------|--------|
| `Informedica.Agents.Lib` | Provides the infrastructure |
| `Informedica.Logging.Lib` | Cross-cutting concern; already uses agents internally |
| `Informedica.Utils.Lib` | Pure utility functions, no domain state |
| `Informedica.GenCORE.Lib` | Core types only, no domain service |
| `Informedica.GenPRES.Shared/Server/Client` | Already use `IServerApi` Command pattern |
| `Informedica.GenSOLVER.Lib` | Mathematical solver, pure function API |
| `Informedica.GenUNITS.Lib` | Unit-of-measure utilities, pure functions |

---

## Implementation Approach

Following the repository's **script-based development workflow** (see `AGENTS.md`):

1. **Prototype** Command/Response DUs and agent creation in `.fsx` scripts per library:
   - `src/Informedica.ZIndex.Lib/Scripts/Agent.fsx`
   - `src/Informedica.ZForm.Lib/Scripts/Agent.fsx`
   - `src/Informedica.GenFORM.Lib/Scripts/Agent.fsx`
   - `src/Informedica.GenORDER.Lib/Scripts/Agent.fsx`
2. **Verify** in FSI — run scenarios, check responses
3. **Migrate** to source files once the human reviewer has approved the design:
   - Add `Agent.fs` to each library (containing Command, Response, and `create`)
   - Update `.fsproj` to include the new file and reference `Informedica.Agents.Lib`
   - Update callers (server, other agents) to use `Agent.postAndAsyncReply`

---

## Recommended Libraries

The `Informedica.Agents.Lib` already provides everything needed. No new NuGet packages are required. The only project dependency change is that `GenFORM.Lib`, `ZForm.Lib`, and `ZIndex.Lib` will need to add a `ProjectReference` to `Informedica.Agents.Lib` in their `.fsproj` files when the agent modules are migrated from scripts to source.

`GenORDER.Lib` transitively depends on `Logging.Lib` which already depends on `Agents.Lib`, so `GenORDER.Lib` will already have access without adding a direct reference.

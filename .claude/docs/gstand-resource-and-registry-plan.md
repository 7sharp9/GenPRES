# Plan: serve GStand dose rules from Resources + refactor Resources to an extensible map (OCP)

Two coupled goals:

1. **Move GStand dose rules into the Resources provider** — Check.fs stops reaching
   into ZForm/ZIndex directly; it consumes a `GStandProvider` handed out by the
   resource provider (same way it already consumes `DoseRule[]`, `RouteMapping[]`).
2. **Refactor `ResourceConfig`/loading from a fixed record into a registry (map) of
   resources** so adding a resource is *registration*, not *modification of the
   loading engine* — Open-Closed Principle.

## Current state (verified)

`src/Informedica.GenFORM.Lib/Api.fs`, `module Resources`:

- `ResourceConfig` — closed record of **17 getter functions** (`Api.fs:94-134`).
- `Data` (raw IO) + `ResourceState` (built collections) records.
- `IResourceProvider` — interface with ~14 explicit `Get*` methods (`Api.fs:28-46`).
- `ResourceProvider` (eager) + `CachedResourceProvider` (TTL, thread-safe, `ReloadCache`).
- `defaultResourceConfig dataUrlId` factory; `loadAllResourcesWithConfig` runs every
  getter in a `result {}` CE, in **dependency order** (FormRoutes←UnitMappings;
  Products←many; DoseRules←DoseRuleData+RouteMappings+FormRoutes+Products; etc.),
  builds `ResourceState`. Two special cases: `Totals` is optional (failure →
  Warning + `[||]`); `DoseRules`/`SolutionRules` return warnings alongside values.
- `module Api` exposes consumer accessors: `getDoseRules`, `getRouteMapping`,
  `getSolutionRules`, `getRenalRules`, `getTotals`, `filterDoseRules`,
  `getPrescriptionRules`, … all `IResourceProvider -> …`.

**OCP pain:** adding one resource today edits **8 places** — `Data`, `ResourceState`,
`ResourceConfig`, `IResourceProvider`, both provider impls, `defaultResourceConfig`,
`loadAllResourcesWithConfig`, plus the test `okConfig` fixture.

`src/Informedica.GenFORM.Lib/Check.fs` (compiled **before** Api.fs):

- `type GStandProvider = Patient -> string -> string -> string -> ZForm.DoseRule seq`
- `createDoseRulesWithMapping routeMapping pat gen frm rte` → `GStand.createDoseRules`
  (patient age→months, weight→kg; ZIndex caches are memoised inside ZIndex.Lib;
  `RuleFinder.find` does the per-patient age/weight/BSA filtering — **not** cached).
- `gStandProvider routeMapping : GStandProvider` (live builder).
- `checkDoseRuleWithProvider`, `checkDoseRuleWith`, `matchWithZIndex`, `checkAllWith`
  are already pure given an injected `GStandProvider` (prior DI refactor).
- `checkDoseRule routeMapping pat dr` / `checkAll routeMapping pat drs` wrap with the
  live builder.

`src/Informedica.GenPRES.Server/ServerApi.Services.fs:114-122` — the only production
consumer: `let rm = Api.getRouteMapping provider in dsrs |> Array.map (Check.checkDoseRule rm pat)`.

**Key ordering fact:** Check.fs precedes Api.fs, so `module Resources` in Api.fs can
reference `Check.GStandProvider` and `Check.gStandProvider`. The GStand builder can
**stay in Check.fs**; Resources just registers it. No new file, no move.

**Patient-dependence note:** GStand needs patient age/weight at call time, so the
resource is **not** a flat pre-loaded array. It is a *capability* (a `GStandProvider`
closure) that depends only on the static `RouteMapping[]` resource and defers the
heavy, patient-specific work to ZIndex's own memoised caches. Modelling it as a
**function-valued resource** keeps the clinical filtering (`RuleFinder`) exactly where
it is — no re-derivation, no new validation burden.

---

## Design

### Part A — Resource registry (the OCP core)

New infrastructure (top of `module Resources`, or a small `Resources.fs` before
Api.fs — both legal given ordering; inline is simpler):

```fsharp
/// Phantom-typed handle: names a resource and carries its value type.
type ResourceKey<'T> = internal { Name: string }

module ResourceKey =
    let create<'T> name : ResourceKey<'T> = { Name = name }

/// Available to a loader while loading: resolve other resources (its deps).
[<Interface>]
type IResourceResolver =
    abstract member Get: ResourceKey<'T> -> 'T

/// A loader: given the resolver (for deps), yield the boxed value + non-fatal
/// warnings, or fail with fatal errors. Boxing is the price of a heterogeneous
/// map; it is hidden behind typed keys so call sites stay type-safe.
type ResourceLoader = IResourceResolver -> Result<obj * Message list, Message list>

/// The registry. Adding a resource = add ONE entry here. The engine never changes.
type ResourceRegistry = Map<string, ResourceLoader>
```

Loading engine — lazy, memoised, dependency-ordered, cycle-detecting, warning-collecting:

```fsharp
exception private ResourceLoadError of Message list

type private LoadEngine(registry: ResourceRegistry) =
    let cache = System.Collections.Generic.Dictionary<string, obj>()
    let inProgress = System.Collections.Generic.HashSet<string>()
    let warnings = ResizeArray<Message>()

    member this.Resolve(key: ResourceKey<'T>) : 'T =
        match cache.TryGetValue key.Name with
        | true, v -> v :?> 'T
        | _ ->
            if not (inProgress.Add key.Name) then
                raise (ResourceLoadError [ ErrorMsg($"cyclic resource dependency: {key.Name}", None) ])
            match registry.TryFind key.Name with
            | None -> raise (ResourceLoadError [ ErrorMsg($"resource not registered: {key.Name}", None) ])
            | Some loader ->
                match loader (this :> IResourceResolver) with
                | Ok(v, ws) ->
                    warnings.AddRange ws
                    cache[key.Name] <- v
                    inProgress.Remove key.Name |> ignore
                    v :?> 'T
                | Error es -> raise (ResourceLoadError es)

    member _.Warnings = warnings |> List.ofSeq
    member _.Resolved = cache   // snapshot of every resolved key
    interface IResourceResolver with
        member this.Get key = this.Resolve key
```

- **Dependency order is automatic**: a loader calls `r.Get someDep`; resolution
  recurses and memoises. The hand-ordered `result {}` CE disappears.
- **Lazy memo of FormularyProducts** (today an inline `lazy`) becomes free: it is one
  resource; Parenteral/Enteral `r.Get Keys.formularyProducts` → resolved once.
- **Fatal vs warning** preserved: loaders return `Ok(value, warnings)` or `Error`.
- Internal exception is caught at the boundary and turned back into `Result`.

Typed keys for every resource (replaces the field names):

```fsharp
module Keys =
    let unitMappings      = ResourceKey.create<UnitMapping[]>      "unitMappings"
    let routeMappings     = ResourceKey.create<RouteMapping[]>     "routeMappings"
    let validForms        = ResourceKey.create<string[]>          "validForms"
    let formRoutes        = ResourceKey.create<FormRoute[]>       "formRoutes"
    let formularyProducts = ResourceKey.create<FormularyProduct[]> "formularyProducts"
    let genPresProducts   = ResourceKey.create<GenPresProduct[]>  "genPresProducts"
    let reconstitution    = ResourceKey.create<Reconstitution[]>  "reconstitution"
    let parenteralMeds    = ResourceKey.create<ProductComponent[]> "parenteralMeds"
    let enteralFeeding    = ResourceKey.create<ProductComponent[]> "enteralFeeding"
    let doseRuleData      = ResourceKey.create<DoseRuleData[]>    "doseRuleData"
    let solutionRuleData  = ResourceKey.create<SolutionRuleData[]> "solutionRuleData"
    let renalRuleData     = ResourceKey.create<RenalRuleData[]>   "renalRuleData"
    let totalsData        = ResourceKey.create<TotalsData[]>      "totalsData"
    let products          = ResourceKey.create<ProductComponent[]> "products"
    let doseRules         = ResourceKey.create<DoseRule[]>        "doseRules"
    let solutionRules     = ResourceKey.create<SolutionRule[]>    "solutionRules"
    let renalRules        = ResourceKey.create<RenalRule[]>       "renalRules"
    // Part B:
    let gStandProvider    = ResourceKey.create<Check.GStandProvider> "gStandProvider"
```

`defaultRegistry dataUrlId : ResourceRegistry` — one entry per resource, each a small
loader. Examples (IO leaf, derived-with-deps, optional):

```fsharp
let private ofResult (k: string) (f: unit -> Result<'T,_>) : ResourceLoader =
    fun _ -> f () |> Result.map (fun v -> box v, [])

let defaultRegistry dataUrlId : ResourceRegistry =
    Map [
        Keys.unitMappings.Name,  ofResult "units"  (fun () -> Mapping.getUnitMapping dataUrlId)
        Keys.routeMappings.Name, ofResult "routes" (fun () -> Mapping.getRouteMapping dataUrlId)
        Keys.formRoutes.Name,    (fun r -> Mapping.getFormRoutes dataUrlId (r.Get Keys.unitMappings)
                                            |> Result.map (fun v -> box v, []))
        // FormularyProducts loaded once; Parenteral/Enteral depend on it:
        Keys.parenteralMeds.Name,(fun r -> r.Get Keys.formularyProducts
                                            |> Product.Parenteral.get (r.Get Keys.unitMappings)
                                            |> fun v -> Ok(box v, []))
        // Derived with warnings (DoseRules):
        Keys.doseRules.Name, (fun r ->
            let rules, msgs =
                DoseRuleLoader.fromData (r.Get Keys.routeMappings) (r.Get Keys.formRoutes)
                                        (r.Get Keys.products)      (r.Get Keys.doseRuleData)
            Ok(box rules, msgs))
        // Optional (Totals): own fallback, never fatal:
        Keys.totalsData.Name, (fun _ ->
            match Mapping.getTotals dataUrlId with
            | Ok d -> Ok(box d, [])
            | Error ms -> Ok(box ([||]: TotalsData[]), ms |> List.map (fun m -> Warning $"Totals not loaded: {msgText m}")))
        // … remaining entries 1:1 with today's defaultResourceConfig …
    ]
```

### Part B — GStand as a function-valued resource

```fsharp
    // in defaultRegistry:
    Keys.gStandProvider.Name, (fun r -> Ok(box (Check.gStandProvider (r.Get Keys.routeMappings)), []))
```

- Depends only on `routeMappings`. Resolving it builds the closure (cheap); the heavy
  ZIndex caches stay memoised inside ZIndex.Lib and fire on the first patient call.
- On `ReloadCache`, the closure is rebuilt from fresh `routeMappings`.
  **Caveat (document it):** ZIndex.Lib's internal memoised caches are *not* flushed by
  GenForm resource reload — a separate concern. Note as a known limitation / follow-up.

### Part C — Provider facade (keep blast radius small)

Add **one** generic accessor to `IResourceProvider`; keep existing `Get*` methods as
one-line conveniences so **no current consumer changes**:

```fsharp
type IResourceProvider =
    abstract member Get: ResourceKey<'T> -> 'T            // NEW — the OCP seam
    abstract member GetDoseRules: unit -> DoseRule[]       // kept (= this.Get Keys.doseRules)
    abstract member GetRouteMappings: unit -> RouteMapping[]
    // … existing methods unchanged …
    abstract member GetGStandProvider: unit -> Check.GStandProvider   // NEW convenience
    abstract member GetResourceInfo: unit -> ResourceInfo
```

- `CachedResourceProvider`: cache the engine's **resolved map** (`Dictionary` snapshot)
  + warnings + timestamp instead of (or alongside) `ResourceState`. `Get key` =
  `resolved[key.Name] :?> 'T`; legacy `GetDoseRules()` = `this.Get Keys.doseRules`.
  `ReloadCache` re-runs the engine. `ResourceState` kept as a derived snapshot so
  `GetData`/`ResourceInfo` and existing consumers are untouched.
- `module Api`: add `getGStandProvider provider = provider.GetGStandProvider()` and a
  generic `get key provider = provider.Get key`. Existing accessors unchanged.

After this, **adding a future resource = 1 Key + 1 registry entry** (+ optional 1-line
convenience accessor). The engine, `ResourceState`, both provider impls, and existing
consumers are never touched. OCP achieved where it actually hurt.

### Part D — Rewire the dose check

`ServerApi.Services.fs:114-122`:

```fsharp
let checkDoseRules provider pat (dsrs: DoseRule[]) =
    let gstand = Api.getGStandProvider provider          // was: Api.getRouteMapping provider
    dsrs |> Array.map (Check.checkDoseRuleWithProvider gstand pat)
```

Check.fs: keep `GStandProvider`, `gStandProvider`, `createDoseRulesWithMapping`
(Resources needs the builder). `checkDoseRule routeMapping` / `checkAll routeMapping`
become thin legacy wrappers (still valid) or are marked obsolete — they are no longer
on the production path. No deletion required; `.fsx` scripts keep working.

---

## Staging (each its own PR, ≤~200 LOC per CONTRIBUTING)

1. **PR1 — registry infra, behaviour-preserving.** Add `ResourceKey`/`IResourceResolver`/
   `ResourceLoader`/`ResourceRegistry`/`LoadEngine`/`Keys`. Reimplement loading as
   `defaultRegistry` + engine; derive `ResourceState` from the resolved map.
   `IResourceProvider`, all `Get*`, and every consumer stay identical. Tests must show
   identical loaded output (golden test below). No GStand yet.
2. **PR2 — generic seam.** Add `IResourceProvider.Get` + `Api.get`; reimplement the
   `Get*` methods as conveniences over it. Pure internal; consumers unchanged.
3. **PR3 — GStand resource + rewire.** Add `Keys.gStandProvider` + registry entry +
   `GetGStandProvider`/`getGStandProvider`; switch `ServerApi.Services.checkDoseRules`.
   Demote `Check.checkDoseRule`/`checkAll` to legacy wrappers.
4. **PR4 (optional) — slim facade.** Migrate consumers to `Api.get Keys.x` and drop
   redundant `Get*` methods. Pure churn; defer unless desired.

## Tests

- **Golden equivalence (PR1):** for the offline demo fixtures, assert the registry-loaded
  `ResourceState` equals the pre-refactor one field-by-field (DoseRules count, messages,
  products, etc.). Use the existing `tests/Informedica.GenFORM.Tests` fixture loaders.
- **Engine unit tests:** (a) dependency resolution order — a loader that `Get`s a dep
  receives the resolved value; (b) memoisation — a dep loader runs once even if two
  resources depend on it (counter); (c) cycle detection → `Error`; (d) fatal `Error`
  from a leaf aborts the whole load; (e) optional-resource warning path (Totals-style
  loader returns `Ok([||], [Warning])`).
- **Typed key round-trip:** `Get key` returns the registered value at its static type
  (no cast at call site).
- **GStand resource (PR3):** with a **stub registry** whose `routeMappings` loader
  returns a fixture mapping, resolve `Keys.gStandProvider`, confirm it is a usable
  `GStandProvider`; then feed it to `Check.checkDoseRuleWithProvider` with a fake
  inner provider (reuse the existing DI test's `sampleDoseRule`) — proves the wiring
  without ZIndex IO.
- **Server tests:** update `okConfig` fixture → a stub `ResourceRegistry`; assert
  `checkDoseRules` now sources the provider from `getGStandProvider`.

## Risks / decisions

- **Boxing/cast** inside the engine is unavoidable for a heterogeneous map in F#; it is
  confined to `LoadEngine`/`Get` and guarded by typed keys. Mis-registration (key name
  vs value type) would fail at the cast — covered by the round-trip test and the golden
  test.
- **Generic methods on an F# interface** (`Get: ResourceKey<'T> -> 'T`) are supported;
  `CachedResourceProvider` implements it over the resolved dictionary.
- **Medical-device caution:** Part A is a core-loader rewrite. PR1 is strictly
  behaviour-preserving and gated by the golden test before anything else ships.
- **Script-only policy:** prototype the engine + registry in
  `src/Informedica.GenFORM.Lib/Scripts/*.fsx` first (shadow `module Resources`),
  validate against fixtures in FSI, then the user migrates to source.

## Decisions (confirmed)

1. GStand granularity: **function-valued resource** — `Keys.gStandProvider :
   ResourceKey<GStandProvider>`, loader closes over `routeMappings`, ZIndex caches stay
   memoised, `RuleFinder` filtering untouched. (Part B as written.)
2. Facade strategy: **keep `Get*` + add generic `Get`** — one new seam on
   `IResourceProvider`, existing `Get*` become 1-line conveniences, current consumers
   unchanged. (Part C as written.)
3. Sequencing: **registry first, then GStand** — PR1 (behaviour-preserving registry,
   golden-gated) → PR2 (generic seam) → PR3 (GStand resource + ServerApi rewire) →
   PR4 optional facade slim. (Staging as written.)

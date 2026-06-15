# Implementation plan — approach (a): boxed resource registry + typed keys, with GStand as a resource

Prototype-validated by `src/Informedica.GenFORM.Lib/Scripts/ResourcesRegistryBoxed.fsx`
(8/8 green). This plan ports that engine into `src/Informedica.GenFORM.Lib/Api.fs`
and adds GStand dose rules as a function-valued resource.

## Confirmed decisions

- **Approach (a)**: heterogeneous resources stored boxed (`obj`) in a registry/map,
  retrieved through phantom-typed `ResourceKey<'T>` (one isolated `:?>`).
- **Keep `ResourceState`/`Data`** as a derived snapshot → `IResourceProvider.Get*` and
  every consumer (GenOrder, MCP, ServerApi, agents) stay byte-identical. The map is
  internal; add one generic `Get` seam alongside.
- **GStand = function-valued resource** (`GStandProvider` closure over `routeMappings`;
  ZIndex caches stay memoised in ZIndex.Lib, `RuleFinder` filtering untouched).
- **One combined PR.** Note: this exceeds CONTRIBUTING's ~200 LOC guideline — internal
  steps below are ordered so review can proceed section by section.

## Current shape (Api.fs, `module Resources`) — what changes

| Element | Today | After |
| --- | --- | --- |
| `Data`, `ResourceState`, `ResourceInfo` | typed records | **unchanged** (derived snapshot) |
| `IResourceProvider` | 15 `Get*` methods | **+ `Get : ResourceKey<'T> -> 'T`**, **+ `GetGStandProvider`** |
| `ResourceConfig` (17-field record) | hand-wired getters | **removed**, replaced by `ResourceRegistry` |
| `defaultResourceConfig` | record literal | **`defaultRegistry`** (Map of loaders) |
| `loadAllResourcesWithConfig` | `result {}` CE, hand-ordered | **`loadAllResourcesWithRegistry`** over the engine |
| `loadAllResources dataUrlId` | `-> Result<ResourceState,_>` | `-> Result<LoadedResources,_>` (public sig change; only internal + scratch callers) |
| `ResourceProvider` / `CachedResourceProvider` | hold `ResourceState` | hold `LoadedResources` (state + resolved map) |
| `module Api` accessors | `getDoseRules` … | **+ `get`, + `getGStandProvider`**; rest unchanged |

## Step 1 — registry infrastructure (new, top of `module Resources`)

Direct port of the prototype, plus `ResolveObj`/`ForceAll`/`Resolved` so the cached
provider can serve arbitrary keys (incl. gstand):

```fsharp
type ResourceKey<'T> = { Name: string }
module ResourceKey =
    let create<'T> name : ResourceKey<'T> = { Name = name }

[<Interface>]
type IResourceResolver =
    abstract member Get: ResourceKey<'T> -> 'T

type ResourceLoader = IResourceResolver -> Result<obj * Message list, Message list>
type ResourceRegistry = Map<string, ResourceLoader>

exception private ResourceLoadError of Message list

type LoadEngine(registry: ResourceRegistry) =
    let cache = System.Collections.Generic.Dictionary<string, obj>()
    let inProgress = System.Collections.Generic.HashSet<string>()
    let warnings = ResizeArray<Message>()

    member private this.ResolveObj(name: string) : obj =
        match cache.TryGetValue name with
        | true, v -> v
        | _ ->
            if not (inProgress.Add name) then
                raise (ResourceLoadError [ ErrorMsg($"cyclic resource dependency: {name}", None) ])
            match registry.TryFind name with
            | None -> raise (ResourceLoadError [ ErrorMsg($"resource not registered: {name}", None) ])
            | Some loader ->
                match loader (this :> IResourceResolver) with
                | Ok(v, ws) -> warnings.AddRange ws; cache[name] <- v; inProgress.Remove name |> ignore; v
                | Error es -> raise (ResourceLoadError es)

    member this.Resolve(key: ResourceKey<'T>) : 'T = this.ResolveObj key.Name :?> 'T
    member this.ForceAll() = registry |> Map.iter (fun name _ -> this.ResolveObj name |> ignore)
    member _.Warnings = warnings |> List.ofSeq
    member _.Resolved = cache |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
    interface IResourceResolver with member this.Get key = this.Resolve key

// loader helpers
let private ofResult (f: unit -> Result<'T, Message list>) : ResourceLoader =
    fun _ -> f () |> Result.map (fun v -> box v, [])
let private derive (f: IResourceResolver -> 'T) : ResourceLoader =
    fun r -> Ok(box (f r), [])
let private deriveWith (f: IResourceResolver -> 'T * Message list) : ResourceLoader =
    fun r -> let v, ws = f r in Ok(box v, ws)
```

`exception` may be `private` to the module; `ResourceLoadError` is caught in
`loadAllResourcesWithRegistry`, never surfaced.

## Step 2 — keys (one per resource + gstand)

```fsharp
module Keys =
    let unitMappings      = ResourceKey.create<UnitMapping[]>                          "unitMappings"
    let routeMappings     = ResourceKey.create<RouteMapping[]>                         "routeMappings"
    let validForms        = ResourceKey.create<string[]>                              "validForms"
    let formRoutes        = ResourceKey.create<FormRoute[]>                           "formRoutes"
    let formularyProducts = ResourceKey.create<FormularyProduct[]>                     "formularyProducts"
    let genPresProducts   = ResourceKey.create<Informedica.ZIndex.Lib.Types.GenPresProduct[]> "genPresProducts"
    let reconstitution    = ResourceKey.create<Reconstitution[]>                       "reconstitution"
    let parenteralMeds    = ResourceKey.create<ProductComponent[]>                     "parenteralMeds"
    let enteralFeeding    = ResourceKey.create<ProductComponent[]>                     "enteralFeeding"
    let doseRuleData      = ResourceKey.create<DoseRuleData[]>                        "doseRuleData"
    let solutionRuleData  = ResourceKey.create<SolutionRuleData[]>                     "solutionRuleData"
    let renalRuleData     = ResourceKey.create<RenalRuleData[]>                        "renalRuleData"
    let totalsData        = ResourceKey.create<TotalsData[]>                          "totalsData"
    let products          = ResourceKey.create<ProductComponent[]>                     "products"
    let doseRules         = ResourceKey.create<DoseRule[]>                            "doseRules"
    let solutionRules     = ResourceKey.create<SolutionRule[]>                         "solutionRules"
    let renalRules        = ResourceKey.create<RenalRule[]>                           "renalRules"
    let gStandProvider    = ResourceKey.create<Check.GStandProvider>                   "gStandProvider"
```

(`Check.GStandProvider` is visible — Check.fs compiles before Api.fs.)

## Step 3 — `defaultRegistry` (replaces `defaultResourceConfig`)

One entry per resource, reusing today's exact get functions. Dependency edges become
`r.Get`; the hand-ordered CE disappears (engine orders by demand). Lazy formulary
memo is now intrinsic (it is one resource; parenteral/enteral/products depend on it →
resolved once). Special cases preserved:

```fsharp
let defaultRegistry dataUrlId : ResourceRegistry =
    Map [
        Keys.unitMappings.Name,      ofResult (fun () -> Mapping.getUnitMapping dataUrlId)
        Keys.routeMappings.Name,     ofResult (fun () -> Mapping.getRouteMapping dataUrlId)
        Keys.validForms.Name,        ofResult (fun () -> Mapping.getValidForms dataUrlId)
        Keys.formRoutes.Name,        (fun r -> Mapping.getFormRoutes dataUrlId (r.Get Keys.unitMappings)
                                                |> Result.map (fun v -> box v, []))
        Keys.formularyProducts.Name, ofResult (fun () -> Product.getFormularyProducts dataUrlId)
        Keys.reconstitution.Name,    ofResult (fun () -> Product.Reconstitution.get dataUrlId)
        Keys.parenteralMeds.Name,    derive (fun r -> r.Get Keys.formularyProducts
                                                       |> Product.Parenteral.get (r.Get Keys.unitMappings))
        Keys.enteralFeeding.Name,    derive (fun r -> r.Get Keys.formularyProducts
                                                       |> Product.Enteral.get (r.Get Keys.unitMappings))
        Keys.genPresProducts.Name,   ofResult (fun () ->
            try Informedica.ZIndex.Lib.GenPresProduct.get [] |> Ok
            with exn -> Utils.Result.createError "GetGenPresProducts" exn)
        Keys.doseRuleData.Name,      ofResult (fun () -> DoseRuleLoader.getData dataUrlId)
        Keys.solutionRuleData.Name,  ofResult (fun () -> SolutionRule.getData dataUrlId)
        Keys.renalRuleData.Name,     ofResult (fun () -> RenalRule.getData dataUrlId)
        // optional resource: own fallback, never fatal (preserves current Totals behaviour)
        Keys.totalsData.Name,        (fun _ ->
            match Mapping.getTotals dataUrlId with
            | Ok d -> Ok(box d, [])
            | Error msgs ->
                let text = function Info s | Warning s -> s | ErrorMsg(s,_) -> s
                Ok(box ([||]: TotalsData[]), msgs |> List.map (fun m -> Warning $"Totals resource not loaded: {text m}")))
        // products: narrow GenPresProducts by dose-rule refs BEFORE building (as today)
        Keys.products.Name,          derive (fun r ->
            let filtered =
                r.Get Keys.genPresProducts
                |> Product.filterGenPresProductsByData (r.Get Keys.routeMappings)
                                                       (r.Get Keys.formularyProducts)
                                                       (r.Get Keys.doseRuleData)
            Product.fromGenPresProducts
                (r.Get Keys.unitMappings) (r.Get Keys.routeMappings) (r.Get Keys.validForms)
                (r.Get Keys.formRoutes)  (r.Get Keys.reconstitution) (r.Get Keys.parenteralMeds)
                (r.Get Keys.enteralFeeding) (r.Get Keys.formularyProducts) filtered)
        // derived with warnings (product-matching), surfaced in ResourceState.Messages
        Keys.doseRules.Name,         deriveWith (fun r ->
            DoseRuleLoader.fromData (r.Get Keys.routeMappings) (r.Get Keys.formRoutes)
                                    (r.Get Keys.products) (r.Get Keys.doseRuleData))
        Keys.solutionRules.Name,     (fun r ->
            SolutionRule.map (r.Get Keys.routeMappings) (r.Get Keys.parenteralMeds)
                             (r.Get Keys.products) (r.Get Keys.solutionRuleData)
            |> Result.map (fun v -> box v, []))
        Keys.renalRules.Name,        (fun r -> RenalRule.map (r.Get Keys.renalRuleData)
                                                |> Result.map (fun v -> box v, []))
        // NEW — GStand dose rules as a function-valued resource
        Keys.gStandProvider.Name,    derive (fun r -> Check.gStandProvider (r.Get Keys.routeMappings))
    ]
```

Behaviour parity to verify: same fatal/short-circuit (engine raises on first `Error`,
matching the `result {}` first-error semantics), same Totals fallback, same DoseRules
warnings, same `filteredGpps` narrowing.

## Step 4 — load + materialise the snapshot

```fsharp
type LoadedResources = { State: ResourceState; Resolved: Map<string, obj> }

let loadAllResourcesWithRegistry (registry: ResourceRegistry) : Result<LoadedResources, Message list> =
    try
        let eng = LoadEngine registry
        eng.ForceAll()  // resolve every key incl. gstand; fail-fast on fatal Error
        let state =
            { Data =
                { UnitMappings = eng.Resolve Keys.unitMappings
                  RouteMappings = eng.Resolve Keys.routeMappings
                  ValidForms = eng.Resolve Keys.validForms
                  FormRoutes = eng.Resolve Keys.formRoutes
                  FormularyProducts = eng.Resolve Keys.formularyProducts
                  GenPresProducts = eng.Resolve Keys.genPresProducts
                  DoseRuleData = eng.Resolve Keys.doseRuleData
                  SolutionRuleData = eng.Resolve Keys.solutionRuleData
                  RenalRuleData = eng.Resolve Keys.renalRuleData
                  TotalsData = eng.Resolve Keys.totalsData }
              Reconstitution = eng.Resolve Keys.reconstitution
              ParenteralMeds = eng.Resolve Keys.parenteralMeds
              EnteralFeeding = eng.Resolve Keys.enteralFeeding
              Products = eng.Resolve Keys.products
              DoseRules = eng.Resolve Keys.doseRules
              SolutionRules = eng.Resolve Keys.solutionRules
              RenalRules = eng.Resolve Keys.renalRules
              Messages = eng.Warnings |> List.toArray
              IsLoaded = true
              LastReloaded = DateTime.UtcNow }
        Ok { State = state; Resolved = eng.Resolved }
    with
    | ResourceLoadError es -> Error es
    | exn -> [ ("Failed to load resources", Some exn) |> ErrorMsg ] |> Error

let loadAllResources dataUrlId = loadAllResourcesWithRegistry (defaultRegistry dataUrlId)
```

`Messages` ordering nuance: today it is `doseMsgs @ totalsMsgs`. `eng.Warnings` is
resolution order. If any test asserts exact message order, normalise (sort/compare as
sets) or order `ForceAll` to resolve doseRules before totals; otherwise this is cosmetic.

## Step 5 — provider facade (additive)

```fsharp
type IResourceProvider =
    abstract member Get: ResourceKey<'T> -> 'T            // NEW seam
    abstract member GetGStandProvider: unit -> Check.GStandProvider   // NEW convenience
    // … all existing Get* unchanged …
```

```fsharp
type ResourceProvider(loaded: LoadedResources) =
    interface IResourceProvider with
        member _.Get(key: ResourceKey<'T>) : 'T = loaded.Resolved[key.Name] :?> 'T
        member _.GetGStandProvider() = loaded.Resolved[Keys.gStandProvider.Name] :?> Check.GStandProvider
        member _.GetDoseRules() = loaded.State.DoseRules
        // … remaining Get* read loaded.State (identical to today) …

type CachedResourceProvider(loadAll: unit -> Result<LoadedResources, Message list>, ttlMinutes: int option) =
    // cache (LoadedResources * DateTime); loadFresh error branch -> { State = emptyState; Resolved = Map.empty }
    // getFromCache (selector: LoadedResources -> 'T)
    member _.Get(key: ResourceKey<'T>) : 'T = getFromCache (fun l -> l.Resolved[key.Name] :?> 'T)
    member _.GetGStandProvider() = getFromCache (fun l -> l.Resolved[Keys.gStandProvider.Name] :?> Check.GStandProvider)
    // existing Get* -> getFromCache (fun l -> l.State.X)
```

`getCachedProviderWithDataUrlId` unchanged in spirit: `CachedResourceProvider((fun () ->
loadAllResources dataUrlId), None)` — `loadAllResources` now returns `LoadedResources`.

`module Api` additions (existing accessors untouched):

```fsharp
let get (key: ResourceKey<'T>) (provider: IResourceProvider) : 'T = provider.Get key
let getGStandProvider (provider: IResourceProvider) = provider.GetGStandProvider()
```

## Step 6 — route the dose check through the resource

`src/Informedica.GenPRES.Server/ServerApi.Services.fs:114-122`:

```fsharp
let checkDoseRules provider pat (dsrs: DoseRule[]) =
    let gstand = Api.getGStandProvider provider          // was: Api.getRouteMapping provider
    dsrs |> Array.map (Check.checkDoseRuleWithProvider gstand pat)
```

`Check.gStandProvider` / `createDoseRulesWithMapping` stay in Check.fs (the registry
loader uses them). `Check.checkDoseRule`/`checkAll` (routeMapping-based) remain as
legacy wrappers — off the production path, still used by `.fsx` scripts; no edit.

## Step 7 — tests

`tests/Informedica.GenFORM.Tests/Tests.fs` — new `module ResourceRegistryTests`:

- engine: dependency resolution passes resolved deps; **memoisation** (shared dep
  loaded once via a counter); **cycle** → `Error`; **fatal leaf** aborts whole load;
  **optional-resource** loader returns `Ok([||], [Warning])` and load still succeeds.
- **typed round-trip**: `Get key` returns the registered value at static type `'T`.
- **GStand resource**: build a **stub registry** whose `routeMappings` loader returns a
  fixture mapping and whose `gStandProvider` loader returns a *fake* `GStandProvider`
  (no ZIndex IO); wrap in a provider; assert `Api.getGStandProvider` returns it and
  `Check.checkDoseRuleWithProvider` consumes it (reuse existing `sampleDoseRule`).
- keep the existing DI test (`checkDoseRuleWithProvider` with fake provider).

`tests/Informedica.GenPRES.Server.Tests/Tests.fs` — migrate the `ResourceConfig` suite:

- `okConfig: ResourceConfig` (L17-36) → `okRegistry: ResourceRegistry` of stub loaders
  returning empty `Ok`.
- error-propagation tests (L82-119): `{ okConfig with GetX = fun _ -> Error … }`
  becomes `okRegistry |> Map.add Keys.x.Name (fun _ -> Error …)`; assert
  `loadAllResourcesWithRegistry` returns `Error`. (Fatal-first semantics preserved.)
- exception-in-loader test → loader that `failwith`s; assert caught → `Error`.
- success-path tests (L139, L250) → `loadAllResourcesWithRegistry okRegistry` builds a
  `LoadedResources`; assert `.State` fields.

## Step 8 — build & verify

1. `dotnet run build` — confirms the generic interface method, both provider impls,
   ServerApi rewire, and Check reference compile.
2. `dotnet run servertests` (GenFORM + Server test projects).
3. Manual: a known selection that previously produced dose-check signals still does
   (gstand now sourced from the provider).

## Risks / notes

- **One isolated unsafe cast** (`:?>` in `LoadEngine`/providers), guarded by typed keys.
  Mis-registration (key name vs value type) fails at resolve — covered by the typed
  round-trip + golden materialisation (every `eng.Resolve Keys.x` in Step 4 is itself a
  cast check at the right type, so a wrong registration throws during load, loudly).
- **Public sig change**: `loadAllResources` now returns `LoadedResources`. Only internal
  callers + non-built `.fsx` scratch (NLP.Lib, GenFORM scripts) reference it — update
  scratch opportunistically, not blocking.
- **`ResourceConfig` removed**: scratch scripts referencing it won't compile if ever run
  in FSI; not in the solution build.
- **Message ordering** (Step 4) — verify against any order-sensitive assertion.
- **ZIndex memo not flushed on reload**: `ReloadCache` rebuilds the gstand closure from
  fresh `routeMappings`, but ZIndex.Lib's internal memoised caches persist. Pre-existing
  behaviour; document as a known limitation / follow-up.
- **Script-only policy**: engine + `defaultRegistry` should be prototyped/extended in
  `Scripts/*.fsx` (the boxed prototype already covers the engine) before the user
  migrates to `Api.fs`.
- **PR size**: combined PR > 200 LOC (engine + 18 loaders + facade + rewire + tests).
  Chosen deliberately; review section-by-section per the steps above.
```

namespace Informedica.GenForm.Lib


module Resources =

    open System
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open FsToolkit.ErrorHandling.ResultCE


    /// Resource provider abstraction. Returns the fully built, in-memory
    /// resource collections. Product collections are v2 `ProductComponent`s.
    type IResourceProvider =
        abstract member GetUnitMappings: unit -> UnitMapping[]
        abstract member GetRouteMappings: unit -> RouteMapping[]
        abstract member GetValidForms: unit -> string[]
        abstract member GetFormRoutes: unit -> FormRoute[]
        abstract member GetFormularyProducts: unit -> FormularyProduct[]
        abstract member GetReconstitution: unit -> Reconstitution[]
        abstract member GetParenteralMeds: unit -> ProductComponent[]
        abstract member GetEnteralFeeding: unit -> ProductComponent[]
        abstract member GetProducts: unit -> ProductComponent[]
        abstract member GetDoseRules: unit -> DoseRule[]
        abstract member GetSolutionRules: unit -> SolutionRule[]
        abstract member GetRenalRules: unit -> RenalRule[]
        abstract member GetResourceInfo: unit -> ResourceInfo

    and ResourceInfo =
        {
            Messages: Message[]
            LastUpdated: DateTime
            IsLoaded: bool
        }


    /// Standalone v2 resource state. The raw, source-loaded data lives in the
    /// nested `Data` record; the built domain collections live at the top level.
    type ResourceState =
        {
            Data: Data
            Reconstitution: Reconstitution[]
            ParenteralMeds: ProductComponent[]
            EnteralFeeding: ProductComponent[]
            Products: ProductComponent[]
            DoseRules: DoseRule[]
            SolutionRules: SolutionRule[]
            RenalRules: RenalRule[]
            Messages: Message[]
            IsLoaded: bool
            LastReloaded: DateTime
        }

    and Data =
        {
            UnitMappings: UnitMapping[]
            RouteMappings: RouteMapping[]
            ValidForms: string[]
            FormRoutes: FormRoute[]
            FormularyProducts: FormularyProduct[]
            GenPresProducts: Informedica.ZIndex.Lib.Types.GenPresProduct[]
            DoseRuleData: DoseRuleData[]
            SolutionRuleData: SolutionRuleData[]
            RenalRuleData: RenalRuleData[]
        }


    let private emptyData =
        {
            UnitMappings = [||]
            RouteMappings = [||]
            ValidForms = [||]
            FormRoutes = [||]
            FormularyProducts = [||]
            GenPresProducts = [||]
            DoseRuleData = [||]
            SolutionRuleData = [||]
            RenalRuleData = [||]
        }


    let private resourceInfo (state: ResourceState) =
        {
            Messages = state.Messages
            LastUpdated = state.LastReloaded
            IsLoaded = state.IsLoaded
        }


    type ResourceConfig =
        {
            GetUnitMappings: unit -> Result<UnitMapping[], Message list>
            GetRouteMappings: unit -> Result<RouteMapping[], Message list>
            GetValidForms: unit -> Result<string[], Message list>
            GetFormRoutes: UnitMapping[] -> Result<FormRoute[], Message list>
            GetFormularyProducts: unit -> Result<FormularyProduct[], Message list>
            GetGenPresProducts: unit -> Result<Informedica.ZIndex.Lib.Types.GenPresProduct[], Message list>
            GetDoseRuleData: unit -> Result<DoseRuleData[], Message list>
            GetSolutionRuleData: unit -> Result<SolutionRuleData[], Message list>
            GetRenalRuleData: unit -> Result<RenalRuleData[], Message list>
            GetReconstitution: unit -> Result<Reconstitution[], Message list>
            GetParenteralMeds: UnitMapping[] -> Result<ProductComponent[], Message list>
            GetEnteralFeeding: UnitMapping[] -> Result<ProductComponent[], Message list>
            // Pure: builds the products from already-loaded raw data (the ZIndex
            // GenPresProducts are the last parameter — read at the impure edge).
            GetProducts:
                UnitMapping[]
                    -> RouteMapping[]
                    -> string[]
                    -> FormRoute[]
                    -> Reconstitution[]
                    -> ProductComponent[]
                    -> ProductComponent[]
                    -> FormularyProduct[]
                    -> Informedica.ZIndex.Lib.Types.GenPresProduct[]
                    -> ProductComponent[]
            // Pure: builds the rules from already-loaded raw *Data.
            // Returns the built DoseRules together with the product-matching
            // warnings, which the shell surfaces in ResourceState.Messages.
            GetDoseRules:
                DoseRuleData[]
                    -> RouteMapping[]
                    -> FormRoute[]
                    -> ProductComponent[]
                    -> Result<DoseRule[] * Message list, Message list>
            GetSolutionRules:
                SolutionRuleData[]
                    -> RouteMapping[]
                    -> ProductComponent[]
                    -> ProductComponent[]
                    -> Result<SolutionRule[], Message list>
            GetRenalRules: RenalRuleData[] -> Result<RenalRule[], Message list>
        }


    /// Default resource configuration using the standard v2 get functions.
    ///
    /// Every sheet/network load is deferred into the thunks below so that it
    /// happens inside loadAllResourcesWithConfig's guarded result CE, not
    /// eagerly at config-construction time.
    ///
    /// `lazy` memoises the formulary products so the shared value is fetched
    /// only once across GetFormularyProducts / GetParenteralMeds /
    /// GetEnteralFeeding, while still loading lazily on first access.
    let defaultResourceConfig dataUrlId =
        let formProds = lazy (Product.getFormularyProducts dataUrlId)

        {
            GetUnitMappings = fun () -> Mapping.getUnitMapping dataUrlId
            GetRouteMappings = fun () -> Mapping.getRouteMapping dataUrlId
            GetValidForms = fun () -> Mapping.getValidForms dataUrlId
            GetFormRoutes = Mapping.getFormRoutes dataUrlId
            GetFormularyProducts = fun () -> formProds.Value
            GetReconstitution = fun () -> Product.Reconstitution.get dataUrlId
            GetParenteralMeds =
                fun mapping ->
                    formProds.Value
                    |> Result.map (fun prods -> prods |> Product.Parenteral.get mapping)
            GetEnteralFeeding =
                fun mapping ->
                    formProds.Value
                    |> Result.map (fun prods -> prods |> Product.Enteral.get mapping)
            // IO edge: read raw source data once.
            GetGenPresProducts =
                fun () ->
                    try
                        Informedica.ZIndex.Lib.GenPresProduct.get [] |> Ok
                    with exn ->
                        Utils.Result.createError "GetGenPresProducts" exn
            GetDoseRuleData = fun () -> DoseRule.getData dataUrlId
            GetSolutionRuleData = fun () -> SolutionRule.getData dataUrlId
            GetRenalRuleData = fun () -> RenalRule.getData dataUrlId
            // Pure transforms: operate on the already-loaded raw data.
            GetProducts = Product.fromGenPresProducts
            GetDoseRules = fun data rm fr prods -> DoseRule.fromDataWithWarnings rm fr prods data
            GetSolutionRules = fun data rm parenteral prods -> SolutionRule.map rm parenteral prods data
            GetRenalRules = fun data -> RenalRule.map data
        }


    /// Load all resources at once using the provided configuration.
    let loadAllResourcesWithConfig (config: ResourceConfig) =
        try
            result {
                // ── IO edge: load all raw data up front ──
                let! unitMappings = config.GetUnitMappings()
                let! routeMappings = config.GetRouteMappings()
                let! validForms = config.GetValidForms()
                let! formRoutes = config.GetFormRoutes unitMappings
                let! formularyProducts = config.GetFormularyProducts()
                let! reconstitution = config.GetReconstitution()
                let! parenteralMeds = config.GetParenteralMeds unitMappings
                let! enteralFeeding = config.GetEnteralFeeding unitMappings
                let! genPresProducts = config.GetGenPresProducts()
                let! doseRuleData = config.GetDoseRuleData()
                let! solutionRuleData = config.GetSolutionRuleData()
                let! renalRuleData = config.GetRenalRuleData()

                // ── pure core: domain create functions on plain data ──
                // narrow the raw ZIndex products to those referenced by dose rules
                // (ID overrides name) BEFORE building, so discarded products are
                // never constructed.
                let filteredGpps =
                    genPresProducts
                    |> Product.filterGenPresProductsByData routeMappings formularyProducts doseRuleData

                let products =
                    config.GetProducts
                        unitMappings
                        routeMappings
                        validForms
                        formRoutes
                        reconstitution
                        parenteralMeds
                        enteralFeeding
                        formularyProducts
                        filteredGpps

                let! doseRules, doseMsgs = config.GetDoseRules doseRuleData routeMappings formRoutes products
                let! solutionRules = config.GetSolutionRules solutionRuleData routeMappings parenteralMeds products
                let! renalRules = config.GetRenalRules renalRuleData

                // ── assemble nested ResourceState ──
                return
                    {
                        Data =
                            {
                                UnitMappings = unitMappings
                                RouteMappings = routeMappings
                                ValidForms = validForms
                                FormRoutes = formRoutes
                                FormularyProducts = formularyProducts
                                GenPresProducts = genPresProducts
                                DoseRuleData = doseRuleData
                                SolutionRuleData = solutionRuleData
                                RenalRuleData = renalRuleData
                            }
                        Reconstitution = reconstitution
                        ParenteralMeds = parenteralMeds
                        EnteralFeeding = enteralFeeding
                        Products = products
                        DoseRules = doseRules
                        SolutionRules = solutionRules
                        RenalRules = renalRules
                        Messages = doseMsgs |> List.toArray
                        LastReloaded = DateTime.UtcNow
                        IsLoaded = true
                    }
            }
        with exn ->
            [ ("Failed to load resources", Some exn) |> ErrorMsg ] |> Error


    /// Load all resources at once using the default configuration.
    let loadAllResources dataUrlId =
        loadAllResourcesWithConfig (defaultResourceConfig dataUrlId)


    /// A plain provider over an already-built ResourceState.
    type ResourceProvider(state: ResourceState) =
        interface IResourceProvider with
            member _.GetUnitMappings() = state.Data.UnitMappings
            member _.GetRouteMappings() = state.Data.RouteMappings
            member _.GetValidForms() = state.Data.ValidForms
            member _.GetFormRoutes() = state.Data.FormRoutes
            member _.GetFormularyProducts() = state.Data.FormularyProducts
            member _.GetReconstitution() = state.Reconstitution
            member _.GetParenteralMeds() = state.ParenteralMeds
            member _.GetEnteralFeeding() = state.EnteralFeeding
            member _.GetProducts() = state.Products
            member _.GetDoseRules() = state.DoseRules
            member _.GetSolutionRules() = state.SolutionRules
            member _.GetRenalRules() = state.RenalRules
            member _.GetResourceInfo() = resourceInfo state


    /// Create a cached resource provider with an optional TTL (in minutes).
    type CachedResourceProvider(loadAllResources: unit -> Result<ResourceState, Message list>, ttlMinutes: int option) =
        let mutable cachedState: (ResourceState * DateTime) option = None
        let lockObj = obj ()

        let isExpired (timestamp: DateTime) =
            match ttlMinutes with
            | None -> false // No expiration if ttl is not set
            | Some ttlMinutes -> DateTime.UtcNow.Subtract(timestamp).TotalMinutes > float ttlMinutes

        let loadFresh () =
            match loadAllResources () with
            | Ok state ->
                cachedState <- Some(state, DateTime.UtcNow)
                state
            | Error msgs ->
                writeErrorMessage $"Failed to load resources: {msgs}"
                // Return empty state on error and cache it to prevent retry on every request
                let emptyState =
                    {
                        Data = emptyData
                        Reconstitution = [||]
                        ParenteralMeds = [||]
                        EnteralFeeding = [||]
                        Products = [||]
                        DoseRules = [||]
                        SolutionRules = [||]
                        RenalRules = [||]
                        Messages = [| yield! msgs |> List.toArray |]
                        LastReloaded = DateTime.MinValue
                        IsLoaded = false
                    }

                cachedState <- Some(emptyState, DateTime.UtcNow)
                emptyState

        member private _.getFromCache(selector: ResourceState -> 'T) =
            lock
                lockObj
                (fun () ->
                    match cachedState with
                    | Some(state, timestamp) when not (isExpired timestamp) -> selector state
                    | _ -> selector (loadFresh ())
                )

        member _.ReloadCache() =
            lock
                lockObj
                (fun () ->
                    cachedState <- None // Invalidate cache
                    loadFresh () |> ignore // Load fresh data
                )

        interface IResourceProvider with
            member this.GetUnitMappings() = this.getFromCache _.Data.UnitMappings
            member this.GetRouteMappings() = this.getFromCache _.Data.RouteMappings
            member this.GetValidForms() = this.getFromCache _.Data.ValidForms
            member this.GetFormRoutes() = this.getFromCache _.Data.FormRoutes

            member this.GetFormularyProducts() =
                this.getFromCache _.Data.FormularyProducts

            member this.GetReconstitution() = this.getFromCache _.Reconstitution
            member this.GetEnteralFeeding() = this.getFromCache _.EnteralFeeding
            member this.GetParenteralMeds() = this.getFromCache _.ParenteralMeds
            member this.GetProducts() = this.getFromCache _.Products
            member this.GetDoseRules() = this.getFromCache _.DoseRules
            member this.GetSolutionRules() = this.getFromCache _.SolutionRules
            member this.GetRenalRules() = this.getFromCache _.RenalRules
            member this.GetResourceInfo() = this.getFromCache resourceInfo


module Api =

    open Informedica.Logging.Lib

    open Resources


    let private logGenFormMessages (logger: Logger) (provider: IResourceProvider) =
        provider.GetResourceInfo().Messages
        |> Array.iter (fun m ->
            match m with
            | Info _ -> Logging.logInfo logger m
            | Warning _ -> Logging.logWarning logger m
            | ErrorMsg _ -> Logging.logError logger m
        )


    let getCachedProviderWithDataUrlId (logger: Logger) dataUrlId : IResourceProvider =
        let provider = CachedResourceProvider((fun () -> loadAllResources dataUrlId), None)

        (provider :> IResourceProvider) |> logGenFormMessages logger
        provider


    let reloadCache (logger: Logger) (provider: IResourceProvider) =
        match provider with
        | :? CachedResourceProvider as cachedProvider ->
            cachedProvider.ReloadCache()
            (cachedProvider :> IResourceProvider) |> logGenFormMessages logger
        | _ -> failwith "Provider is not a CachedResourceProvider instance"


    let getRouteMapping (provider: IResourceProvider) = provider.GetRouteMappings()


    let getDoseRules (provider: IResourceProvider) = provider.GetDoseRules()


    let getSolutionRules (provider: IResourceProvider) = provider.GetSolutionRules()


    let getRenalRules (provider: IResourceProvider) = provider.GetRenalRules()


    // Filtering functions using cached mappings


    let filterDoseRules (provider: IResourceProvider) filter doseRules =
        let routeMappings = getRouteMapping provider
        DoseRule.filter routeMappings filter doseRules


    let getPrescriptionRules (provider: IResourceProvider) =
        let doseRules = getDoseRules provider
        let solutionRules = getSolutionRules provider
        let routeMappings = getRouteMapping provider
        let renalRules = getRenalRules provider

        PrescriptionRule.getForPatient doseRules solutionRules renalRules routeMappings


    let filterPrescriptionRules (provider: IResourceProvider) filter : Result<PrescriptionRule array, Message list> =
        let doseRules = getDoseRules provider
        let solutionRules = getSolutionRules provider
        let routeMappings = getRouteMapping provider
        let renalRules = getRenalRules provider

        let chunkSize =
            let c = (doseRules |> Array.length) / 12
            if c > 0 then c else 1

        doseRules
        |> Array.chunkBySize chunkSize
        |> Array.map (fun rules ->
            async { return PrescriptionRule.filter rules solutionRules renalRules routeMappings filter }
        )
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Utils.Result.foldResults

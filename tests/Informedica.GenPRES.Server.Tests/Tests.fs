namespace Informedica.GenPRES.Server.Tests


open Expecto
open Expecto.Flip

open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources


module Helpers =


    let errMsg s : Message = ErrorMsg (s, None)


    let okConfig : ResourceConfig =
        {
            GetUnitMappings = fun () -> Ok [||]
            GetRouteMappings = fun () -> Ok [||]
            GetValidForms = fun () -> Ok [||]
            GetFormRoutes = fun _ -> Ok [||]
            GetFormularyProducts = fun () -> Ok [||]
            GetReconstitution = fun () -> Ok [||]
            GetParenteralMeds = fun _ -> Ok [||]
            GetEnteralFeeding = fun _ -> Ok [||]
            GetProducts = fun _ _ _ _ _ _ _ _ -> [||]
            GetDoseRules = fun _ _ _ -> Ok [||]
            GetSolutionRules = fun _ _ _ -> Ok [||]
            GetRenalRules = fun () -> Ok [||]
        }


    let emptyFormulary : Shared.Types.Formulary =
        {
            Generics = [||]
            Indications = [||]
            Routes = [||]
            Forms = [||]
            DoseTypes = [||]
            PatientCategories = [||]
            Products = [||]
            Generic = None
            Indication = None
            Route = None
            Form = None
            DoseType = None
            PatientCategory = None
            Patient = None
            Markdown = ""
        }


    let emptyParenteralia : Shared.Types.Parenteralia =
        {
            Generics = [||]
            Forms = [||]
            Routes = [||]
            PatientCategories = [||]
            Generic = None
            Form = None
            Route = None
            PatientCategory = None
            Markdown = ""
        }


module ResourceErrorTests =


    open Helpers


    let errorPropagationTests =
        testList "loadAllResourcesWithConfig error propagation" [

            test "first getter (GetUnitMappings) returns Error propagates" {
                let config =
                    { okConfig with
                        GetUnitMappings = fun () -> Error [ errMsg "unit mapping failed" ]
                    }

                config
                |> loadAllResourcesWithConfig
                |> Result.isError
                |> Expect.isTrue "should be Error"
            }

            test "middle getter (GetFormRoutes) returns Error propagates" {
                let config =
                    { okConfig with
                        GetFormRoutes = fun _ -> Error [ errMsg "form routes failed" ]
                    }

                config
                |> loadAllResourcesWithConfig
                |> Result.isError
                |> Expect.isTrue "should be Error"
            }

            test "last getter (GetRenalRules) returns Error propagates" {
                let config =
                    { okConfig with
                        GetRenalRules = fun () -> Error [ errMsg "renal rules failed" ]
                    }

                config
                |> loadAllResourcesWithConfig
                |> Result.isError
                |> Expect.isTrue "should be Error"
            }

            test "getter throws exception is caught and returned as Error" {
                let config =
                    { okConfig with
                        GetUnitMappings = fun () -> failwith "unexpected crash"
                    }

                let result = config |> loadAllResourcesWithConfig

                result
                |> Result.isError
                |> Expect.isTrue "should be Error"

                match result with
                | Error msgs ->
                    msgs
                    |> List.exists (fun m ->
                        match m with
                        | ErrorMsg (s, _) -> s.Contains("Failed to load resources")
                        | _ -> false
                    )
                    |> Expect.isTrue "should contain 'Failed to load resources' message"
                | Ok _ -> failwith "expected Error"
            }
        ]


    let successPathTests =
        testList "loadAllResourcesWithConfig success path" [

            test "all getters succeed returns Ok with IsLoaded = true" {
                let result = okConfig |> loadAllResourcesWithConfig

                result
                |> Result.isOk
                |> Expect.isTrue "should be Ok"

                match result with
                | Ok state ->
                    state.IsLoaded
                    |> Expect.isTrue "IsLoaded should be true"

                    state.Messages
                    |> Expect.equal "Messages should be empty" [||]
                | Error _ -> failwith "expected Ok"
            }
        ]


    let cachedProviderErrorStateTests =
        testList "CachedResourceProvider error state" [

            test "loader returns Error, GetResourceInfo shows IsLoaded = false" {
                let provider =
                    CachedResourceProvider(
                        (fun () -> Error [ errMsg "load failed" ]),
                        None
                    )

                let info = (provider :> IResourceProvider).GetResourceInfo()

                info.IsLoaded
                |> Expect.isFalse "IsLoaded should be false"

                info.Messages
                |> Array.isEmpty
                |> Expect.isFalse "Messages should not be empty"
            }

            test "all resource getters return empty arrays when loader failed" {
                let provider =
                    CachedResourceProvider(
                        (fun () -> Error [ errMsg "load failed" ]),
                        None
                    )

                (provider :> IResourceProvider).GetUnitMappings()
                |> Expect.equal "UnitMappings should be empty" [||]

                (provider :> IResourceProvider).GetDoseRules()
                |> Expect.equal "DoseRules should be empty" [||]

                (provider :> IResourceProvider).GetProducts()
                |> Expect.equal "Products should be empty" [||]

                (provider :> IResourceProvider).GetRenalRules()
                |> Expect.equal "RenalRules should be empty" [||]
            }
        ]


    let cachingBehaviorTests =
        testList "CachedResourceProvider caching behavior" [

            test "after error, second call does NOT re-invoke loader" {
                let mutable callCount = 0

                let provider =
                    CachedResourceProvider(
                        (fun () ->
                            callCount <- callCount + 1
                            Error [ errMsg "load failed" ]
                        ),
                        None
                    )

                (provider :> IResourceProvider).GetResourceInfo() |> ignore

                callCount
                |> Expect.equal "loader should be called once" 1

                (provider :> IResourceProvider).GetResourceInfo() |> ignore

                callCount
                |> Expect.equal "loader should still be called once" 1
            }

            test "ReloadCache re-invokes loader" {
                let mutable callCount = 0

                let provider =
                    CachedResourceProvider(
                        (fun () ->
                            callCount <- callCount + 1
                            Error [ errMsg "load failed" ]
                        ),
                        None
                    )

                (provider :> IResourceProvider).GetResourceInfo() |> ignore

                callCount
                |> Expect.equal "loader called once after first access" 1

                provider.ReloadCache()

                callCount
                |> Expect.equal "loader called twice after ReloadCache" 2
            }

            test "loader fails first then succeeds, after ReloadCache IsLoaded = true" {
                let mutable callCount = 0

                let provider =
                    CachedResourceProvider(
                        (fun () ->
                            callCount <- callCount + 1
                            if callCount = 1 then
                                Error [ errMsg "first attempt failed" ]
                            else
                                okConfig |> loadAllResourcesWithConfig
                        ),
                        None
                    )

                let info1 = (provider :> IResourceProvider).GetResourceInfo()

                info1.IsLoaded
                |> Expect.isFalse "should not be loaded after first attempt"

                provider.ReloadCache()

                let info2 = (provider :> IResourceProvider).GetResourceInfo()

                info2.IsLoaded
                |> Expect.isTrue "should be loaded after ReloadCache"
            }
        ]


    let processCmdGuardTests =
        testList "processCmd IsLoaded guard" [

            test "FormularyCmd returns Error when provider IsLoaded = false" {
                let provider =
                    CachedResourceProvider(
                        (fun () -> Error [ errMsg "resources unavailable" ]),
                        None
                    )

                let cmd =
                    Shared.Api.FormularyCmd emptyFormulary

                let result =
                    ServerApi.Command.processCmd (ServerApi.Adapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for FormularyCmd when not loaded"
            }

            test "ParenteraliaCmd returns Error when provider IsLoaded = false" {
                let provider =
                    CachedResourceProvider(
                        (fun () -> Error [ errMsg "resources unavailable" ]),
                        None
                    )

                let cmd =
                    Shared.Api.ParenteraliaCmd emptyParenteralia

                let result =
                    ServerApi.Command.processCmd (ServerApi.Adapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for ParenteraliaCmd when not loaded"
            }
        ]


    let agentAdapterGuardTests =
        testList "processCmd IsLoaded guard (AgentAdapters)" [

            test "FormularyCmd returns Error when provider IsLoaded = false (agent)" {
                let provider =
                    CachedResourceProvider(
                        (fun () -> Error [ errMsg "resources unavailable" ]),
                        None
                    )

                let cmd =
                    Shared.Api.FormularyCmd emptyFormulary

                let result =
                    ServerApi.Command.processCmd (ServerApi.AgentAdapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for FormularyCmd when not loaded (agent)"
            }

            test "ParenteraliaCmd returns Error when provider IsLoaded = false (agent)" {
                let provider =
                    CachedResourceProvider(
                        (fun () -> Error [ errMsg "resources unavailable" ]),
                        None
                    )

                let cmd =
                    Shared.Api.ParenteraliaCmd emptyParenteralia

                let result =
                    ServerApi.Command.processCmd (ServerApi.AgentAdapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for ParenteraliaCmd when not loaded (agent)"
            }
        ]


    [<Tests>]
    let tests =
        testList "Resource Error Handling Tests" [
            errorPropagationTests
            successPathTests
            cachedProviderErrorStateTests
            cachingBehaviorTests
            processCmdGuardTests
            agentAdapterGuardTests
        ]


module StubAdapterTests =

    open Expecto
    open Shared
    open Shared.Types
    open ServerApi


    /// Stub adapters for isolated application-layer testing.
    /// No IResourceProvider, no network, no data loading.
    module StubAdapters =

        let private notStubbed _ = failwith "not stubbed"


        let formularyAlwaysOk (returnForm: Formulary) : FormularyPort =
            {
                getFormulary = fun _ -> async { return Ok returnForm }
                getParenteralia = fun _ -> async { return Ok Helpers.emptyParenteralia }
            }


        let formularyAlwaysFails (msgs: string []) : FormularyPort =
            {
                getFormulary = fun _ -> async { return Error msgs }
                getParenteralia = fun _ -> async { return Error msgs }
            }


        let orderContextAlwaysOk (returnCtx: OrderContext) : OrderContextPort =
            {
                evaluate = fun _ _ -> async { return Ok returnCtx }
            }


        let orderContextAlwaysFails (msgs: string []) : OrderContextPort =
            {
                evaluate = fun _ _ -> async { return Error msgs }
            }


        let orderPlanAlwaysOk (returnPlan: OrderPlan) : OrderPlanPort =
            {
                updateOrderPlan = fun _ _ -> async { return Ok returnPlan }
                filterOrderPlan = fun _ -> async { return Ok returnPlan }
            }


        let nutritionPlanAlwaysOk (returnPlan: NutritionPlan) : NutritionPlanPort =
            {
                initNutritionPlan = fun _ -> async { return Ok returnPlan }
                addNutritionContext = fun _ -> async { return Ok returnPlan }
                removeNutritionContext = fun _ -> async { return Ok returnPlan }
                updateNutritionOrderContext = fun _ -> async { return Ok returnPlan }
                selectNutritionOrderScenario = fun _ -> async { return Ok returnPlan }
                navigateNutritionOrderContext = fun _ -> async { return Ok returnPlan }
            }


        let makeEnv
            (formulary: FormularyPort)
            (orderContext: OrderContextPort)
            (orderPlan: OrderPlanPort)
            (nutritionPlan: NutritionPlanPort)
            : AppEnv =
            {
                formulary = formulary
                orderContext = orderContext
                orderPlan = orderPlan
                nutritionPlan = nutritionPlan
                requireLoaded = fun () -> None
            }


        let makeEnvNotLoaded (msgs: string []) : AppEnv =
            {
                formulary = formularyAlwaysFails [| "not loaded" |]
                orderContext = orderContextAlwaysFails [| "not loaded" |]
                orderPlan =
                    {
                        updateOrderPlan = fun _ _ -> async { return Error [| "not loaded" |] }
                        filterOrderPlan = fun _ -> async { return Error [| "not loaded" |] }
                    }
                nutritionPlan =
                    {
                        initNutritionPlan = fun _ -> async { return Error [| "not loaded" |] }
                        addNutritionContext = fun _ -> async { return Error [| "not loaded" |] }
                        removeNutritionContext = fun _ -> async { return Error [| "not loaded" |] }
                        updateNutritionOrderContext = fun _ -> async { return Error [| "not loaded" |] }
                        selectNutritionOrderScenario = fun _ -> async { return Error [| "not loaded" |] }
                        navigateNutritionOrderContext = fun _ -> async { return Error [| "not loaded" |] }
                    }
                requireLoaded = fun () -> Some msgs
            }


    open StubAdapters

    let emptyCtx = Models.OrderContext.empty
    let emptyPlan : OrderPlan =
        {
            Patient = Models.Patient.empty
            Scenarios = [||]
            Selected = None
            Filtered = [||]
            Totals = Models.Totals.empty
        }
    let emptyNutritionPlan = Models.NutritionPlan.create Models.Patient.empty [||]


    let commandRoutingTests =
        testList "Stub adapter command routing" [

            testAsync "FormularyCmd dispatches to formulary.getFormulary" {
                let returnForm = { Helpers.emptyFormulary with Markdown = "stubbed" }
                let env = makeEnv (formularyAlwaysOk returnForm) (orderContextAlwaysOk emptyCtx) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                match result with
                | Ok (Api.FormularyResp f) ->
                    Expect.equal f.Markdown "stubbed" "should return stubbed formulary"
                | other ->
                    Tests.failtest $"expected Ok FormularyResp, got {other}"
            }

            testAsync "ParenteraliaCmd dispatches to formulary.getParenteralia" {
                let env = makeEnv (formularyAlwaysOk Helpers.emptyFormulary) (orderContextAlwaysOk emptyCtx) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.ParenteraliaCmd Helpers.emptyParenteralia)

                match result with
                | Ok (Api.ParenteraliaResp _) -> ()
                | other -> Tests.failtest $"expected Ok ParenteraliaResp, got {other}"
            }

            testAsync "OrderContextCmd dispatches to orderContext.evaluate" {
                let env = makeEnv (formularyAlwaysOk Helpers.emptyFormulary) (orderContextAlwaysOk emptyCtx) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.OrderContextCmd (Api.UpdateOrderContext, emptyCtx))

                match result with
                | Ok (Api.OrderContextResp (Api.OrderContextResult _)) -> ()
                | other -> Tests.failtest $"expected Ok OrderContextResp, got {other}"
            }

            testAsync "NutritionPlanCmd InitNutritionPlan dispatches to nutritionPlan port" {
                let env = makeEnv (formularyAlwaysOk Helpers.emptyFormulary) (orderContextAlwaysOk emptyCtx) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.NutritionPlanCmd (Api.InitNutritionPlan Models.Patient.empty))

                match result with
                | Ok (Api.NutritionPlanResp (Api.NutritionPlanInitialised _)) -> ()
                | other -> Tests.failtest $"expected Ok NutritionPlanInitialised, got {other}"
            }

            testAsync "OrderPlanCmd FilterOrderPlan dispatches to orderPlan port" {
                let env = makeEnv (formularyAlwaysOk Helpers.emptyFormulary) (orderContextAlwaysOk emptyCtx) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.OrderPlanCmd (Api.FilterOrderPlan emptyPlan))

                match result with
                | Ok (Api.OrderPlanResp (Api.OrderPlanFiltered _)) -> ()
                | other -> Tests.failtest $"expected Ok OrderPlanFiltered, got {other}"
            }
        ]


    let errorPropagationTests =
        testList "Stub adapter error propagation" [

            testAsync "FormularyCmd propagates port error" {
                let env = makeEnv (formularyAlwaysFails [| "test error" |]) (orderContextAlwaysOk emptyCtx) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                match result with
                | Error msgs ->
                    Expect.equal msgs [| "test error" |] "should propagate error messages"
                | Ok _ ->
                    Tests.failtest "expected Error, got Ok"
            }

            testAsync "OrderContextCmd propagates port error" {
                let env = makeEnv (formularyAlwaysOk Helpers.emptyFormulary) (orderContextAlwaysFails [| "ctx error" |]) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.OrderContextCmd (Api.UpdateOrderContext, emptyCtx))

                match result with
                | Error msgs ->
                    Expect.equal msgs [| "ctx error" |] "should propagate order context error"
                | Ok _ ->
                    Tests.failtest "expected Error, got Ok"
            }
        ]


    let requireLoadedTests =
        testList "Stub adapter requireLoaded guard" [

            testAsync "requireLoaded returns Error when not loaded" {
                let env = makeEnvNotLoaded [| "not ready" |]

                let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                match result with
                | Error msgs ->
                    Expect.equal msgs [| "not ready" |] "should return requireLoaded error"
                | Ok _ ->
                    Tests.failtest "expected Error from requireLoaded, got Ok"
            }

            testAsync "requireLoaded passes when loaded" {
                let env = makeEnv (formularyAlwaysOk Helpers.emptyFormulary) (orderContextAlwaysOk emptyCtx) (orderPlanAlwaysOk emptyPlan) (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.FormularyCmd Helpers.emptyFormulary)

                match result with
                | Ok _ -> ()
                | Error msgs -> Tests.failtest $"expected Ok, got Error {msgs}"
            }
        ]


    [<Tests>]
    let tests =
        testList "Stub Adapter Tests" [
            commandRoutingTests
            errorPropagationTests
            requireLoadedTests
        ]

namespace ServerApi


module Adapters =

    open Shared.Api
    open Informedica.GenForm.Lib


    let private interactionJsonCache =
        lazy
            (let path =
                System.IO.Path.Combine(Logging.getServerDataPath (), "data", "cache", "interactions", "Data.JSON")
                |> System.IO.Path.GetFullPath

             if System.IO.File.Exists(path) then
                 System.IO.File.ReadAllText(path) |> Some
             else
                 None)


    let loadInteractionJson () = interactionJsonCache.Value


    let toSharedDrugInteraction (di: Informedica.GenInteract.Lib.DrugInteraction) : Shared.Types.DrugInteraction =
        {
            Name = di.Name
            Drug1 = di.Drug1
            Drug2 = di.Drug2
        }


    let private resolveLogger () =
        match Logging.loggingLevel with
        | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
        | Some level ->
            let agent = Logging.getLogger level Logging.OrderLogger
            (Some agent, agent.Logger)


    let private setComponentName name agent =
        async {
            match agent with
            | Some a -> do! a |> Logging.setComponentName (Some name)
            | None -> ()
        }


    let private makeFormularyPort (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form -> async { return form |> FormularyService.get provider }

            getParenteralia =
                fun par -> async { return par |> ParenteraliaService.get provider |> Result.mapError Array.singleton }
        }


    let private makeOrderContextPort agent logger (provider: Resources.IResourceProvider) : OrderContextPort =
        {
            evaluate =
                fun ctxCmd ctx ->
                    async {
                        do! setComponentName "OrderContext" agent

                        return ctx |> OrderContextService.evaluate logger provider ctxCmd
                    }
        }


    let private makeOrderPlanPort agent (orderCtxPort: OrderContextPort) : OrderPlanPort =
        {
            updateOrderPlan =
                fun tp cmdOpt ->
                    async {
                        do! setComponentName "TreatmentPlan" agent

                        let! updated = OrderPlanService.updateOrderPlan orderCtxPort tp cmdOpt
                        return updated |> OrderPlanService.calculateTotals |> Ok
                    }

            filterOrderPlan = fun tp -> async { return tp |> OrderPlanService.calculateTotals |> Ok }
        }


    let private makeNutritionPlanPort
        (orderCtxPort: OrderContextPort)
        logger
        (provider: Resources.IResourceProvider)
        : NutritionPlanPort
        =
        {
            initNutritionPlan =
                fun patient -> async { return NutritionPlanService.initNutritionPlan logger provider patient }

            addNutritionContext =
                fun (plan, category) -> NutritionPlanService.addNutritionContext orderCtxPort (plan, category)

            removeNutritionContext =
                fun (plan, id) -> async { return NutritionPlanService.removeNutritionContext (plan, id) }

            updateNutritionOrderContext =
                fun (plan, label, ctx) ->
                    NutritionPlanService.updateNutritionOrderContext orderCtxPort (plan, label, ctx)

            selectNutritionOrderScenario =
                fun (plan, label, ctx) ->
                    NutritionPlanService.selectNutritionOrderScenario orderCtxPort (plan, label, ctx)

            navigateNutritionOrderContext =
                fun (plan, label, ctxCmd, ctx) ->
                    NutritionPlanService.navigateNutritionOrderContext orderCtxPort (plan, label, ctxCmd, ctx)
        }


    let makeAppEnv (provider: Resources.IResourceProvider) : AppEnv =
        let agent, logger = resolveLogger ()
        let orderCtxPort = makeOrderContextPort agent logger provider

        {
            formulary = makeFormularyPort provider
            orderContext = orderCtxPort
            orderPlan = makeOrderPlanPort agent orderCtxPort
            nutritionPlan = makeNutritionPlanPort orderCtxPort logger provider
            interaction =
                {
                    checkInteractions =
                        fun drugs ->
                            async {
                                try
                                    let result =
                                        Informedica.GenInteract.Lib.Api.checkInteractions (loadInteractionJson ()) drugs
                                        |> List.map toSharedDrugInteraction

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            requireLoaded =
                fun () ->
                    let info = provider.GetResourceInfo()

                    if info.IsLoaded then
                        None
                    else
                        info.Messages |> Array.map (fun msg -> FormLogging.formatMessage msg) |> Some
        }

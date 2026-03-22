namespace ServerApi


module Adapters =

    open Shared.Api
    open Informedica.GenForm.Lib


    let private resolveLogger () =
        match Logging.loggingLevel with
        | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
        | Some level ->
            let agent = Logging.getLogger level Logging.OrderLogger
            (Some agent, agent.Logger)


    let private setComponentName name (agent, logger) =
        async {
            match agent with
            | Some a -> do! a |> Logging.setComponentName (Some name)
            | None -> ()
        },
        logger


    let makeFormularyPort (provider: Resources.IResourceProvider) : IFormularyPort =
        {
            getDoseRules = fun form ->
                async {
                    return
                        form
                        |> Formulary.get provider
                }

            getParenteralia = fun par ->
                async {
                    return
                        par
                        |> Parenteralia.get provider
                        |> Result.mapError Array.singleton
                }
        }


    let makeOrderContextPort (provider: Resources.IResourceProvider) : IOrderContextPort =
        {
            evaluate = fun ctxCmd ctx ->
                async {
                    let agent, logger = resolveLogger ()
                    let setName, logger = setComponentName "OrderContext" (agent, logger)
                    do! setName

                    return
                        ctx
                        |> OrderContextService.evaluate logger provider ctxCmd
                }
        }


    let makeOrderPlanPort (provider: Resources.IResourceProvider) : IOrderPlanPort =
        {
            updateOrderPlan = fun tp cmdOpt ->
                async {
                    let agent, logger = resolveLogger ()
                    let setName, logger = setComponentName "TreatmentPlan" (agent, logger)
                    do! setName

                    return
                        OrderPlan.updateOrderPlan logger provider tp cmdOpt
                        |> OrderPlan.calculateTotals
                        |> Ok
                }

            filterOrderPlan = fun tp ->
                async {
                    return
                        tp
                        |> OrderPlan.calculateTotals
                        |> Ok
                }
        }


    let makeNutritionPlanPort (provider: Resources.IResourceProvider) : INutritionPlanPort =
        {
            initNutritionPlan = fun patient ->
                async {
                    let _, logger = resolveLogger ()
                    return NutritionPlan.initNutritionPlan logger provider patient
                }

            addNutritionContext = fun (plan, category) ->
                async {
                    let _, logger = resolveLogger ()
                    return NutritionPlan.addNutritionContext logger provider (plan, category)
                }

            removeNutritionContext = fun (plan, id) ->
                async {
                    return NutritionPlan.removeNutritionContext (plan, id)
                }

            updateNutritionOrderContext = fun (plan, label, ctx) ->
                async {
                    let _, logger = resolveLogger ()
                    return NutritionPlan.updateNutritionOrderContext logger provider (plan, label, ctx)
                }

            selectNutritionOrderScenario = fun (plan, label, ctx) ->
                async {
                    let _, logger = resolveLogger ()
                    return NutritionPlan.selectNutritionOrderScenario logger provider (plan, label, ctx)
                }

            navigateNutritionOrderContext = fun (plan, label, ctxCmd, ctx) ->
                async {
                    let _, logger = resolveLogger ()
                    return NutritionPlan.navigateNutritionOrderContext logger provider (plan, label, ctxCmd, ctx)
                }
        }


    let makeAppEnv (provider: Resources.IResourceProvider) : AppEnv =
        {
            formulary = makeFormularyPort provider
            orderContext = makeOrderContextPort provider
            orderPlan = makeOrderPlanPort provider
            nutritionPlan = makeNutritionPlanPort provider
            requireLoaded = fun () ->
                let info = provider.GetResourceInfo()
                if info.IsLoaded then None
                else
                    info.Messages
                    |> Array.map (fun msg -> FormLogging.formatMessage msg)
                    |> Error
                    |> Some
        }

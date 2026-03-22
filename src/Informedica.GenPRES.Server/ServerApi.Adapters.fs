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


    let private setComponentName name agent =
        async {
            match agent with
            | Some a -> do! a |> Logging.setComponentName (Some name)
            | None -> ()
        }


    let private makeFormularyPort (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form ->
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


    let private makeOrderContextPort agent logger (provider: Resources.IResourceProvider) : OrderContextPort =
        {
            evaluate = fun ctxCmd ctx ->
                async {
                    do! setComponentName "OrderContext" agent

                    return
                        ctx
                        |> OrderContextService.evaluate logger provider ctxCmd
                }
        }


    let private makeOrderPlanPort agent logger (provider: Resources.IResourceProvider) : OrderPlanPort =
        {
            updateOrderPlan = fun tp cmdOpt ->
                async {
                    do! setComponentName "TreatmentPlan" agent

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


    let private makeNutritionPlanPort logger (provider: Resources.IResourceProvider) : NutritionPlanPort =
        {
            initNutritionPlan = fun patient ->
                async {
                    return NutritionPlan.initNutritionPlan logger provider patient
                }

            addNutritionContext = fun (plan, category) ->
                async {
                    return NutritionPlan.addNutritionContext logger provider (plan, category)
                }

            removeNutritionContext = fun (plan, id) ->
                async {
                    return NutritionPlan.removeNutritionContext (plan, id)
                }

            updateNutritionOrderContext = fun (plan, label, ctx) ->
                async {
                    return NutritionPlan.updateNutritionOrderContext logger provider (plan, label, ctx)
                }

            selectNutritionOrderScenario = fun (plan, label, ctx) ->
                async {
                    return NutritionPlan.selectNutritionOrderScenario logger provider (plan, label, ctx)
                }

            navigateNutritionOrderContext = fun (plan, label, ctxCmd, ctx) ->
                async {
                    return NutritionPlan.navigateNutritionOrderContext logger provider (plan, label, ctxCmd, ctx)
                }
        }


    let makeAppEnv (provider: Resources.IResourceProvider) : AppEnv =
        let agent, logger = resolveLogger ()

        {
            formulary = makeFormularyPort provider
            orderContext = makeOrderContextPort agent logger provider
            orderPlan = makeOrderPlanPort agent logger provider
            nutritionPlan = makeNutritionPlanPort logger provider
            requireLoaded = fun () ->
                let info = provider.GetResourceInfo()
                if info.IsLoaded then None
                else
                    info.Messages
                    |> Array.map (fun msg -> FormLogging.formatMessage msg)
                    |> Some
        }

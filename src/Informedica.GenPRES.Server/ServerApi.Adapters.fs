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
                        |> FormularyService.get provider
                }

            getParenteralia = fun par ->
                async {
                    return
                        par
                        |> ParenteraliaService.get provider
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
                        OrderPlanService.updateOrderPlan logger provider tp cmdOpt
                        |> OrderPlanService.calculateTotals
                        |> Ok
                }

            filterOrderPlan = fun tp ->
                async {
                    return
                        tp
                        |> OrderPlanService.calculateTotals
                        |> Ok
                }
        }


    let private makeNutritionPlanPort logger (provider: Resources.IResourceProvider) : NutritionPlanPort =
        {
            initNutritionPlan = fun patient ->
                async {
                    return NutritionPlanService.initNutritionPlan logger provider patient
                }

            addNutritionContext = fun (plan, category) ->
                async {
                    return NutritionPlanService.addNutritionContext logger provider (plan, category)
                }

            removeNutritionContext = fun (plan, id) ->
                async {
                    return NutritionPlanService.removeNutritionContext (plan, id)
                }

            updateNutritionOrderContext = fun (plan, label, ctx) ->
                async {
                    return NutritionPlanService.updateNutritionOrderContext logger provider (plan, label, ctx)
                }

            selectNutritionOrderScenario = fun (plan, label, ctx) ->
                async {
                    return NutritionPlanService.selectNutritionOrderScenario logger provider (plan, label, ctx)
                }

            navigateNutritionOrderContext = fun (plan, label, ctxCmd, ctx) ->
                async {
                    return NutritionPlanService.navigateNutritionOrderContext logger provider (plan, label, ctxCmd, ctx)
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

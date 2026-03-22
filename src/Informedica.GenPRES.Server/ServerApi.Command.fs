namespace ServerApi


module Command =

    open Shared.Api
    open Informedica.GenForm.Lib

    /// Check if resources are loaded. Returns Error with messages if not.
    let requireLoaded (provider: Resources.IResourceProvider) =
        let info = provider.GetResourceInfo()
        if info.IsLoaded then None
        else
            info.Messages
            |> Array.map (fun msg -> FormLogging.formatMessage msg)
            |> Error
            |> Some


    let processCmd provider cmd =
        let agent, logger =
            match Logging.loggingLevel with
            | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
            | Some level ->
                let agent =
                    Logging.getLogger level Logging.OrderLogger
                (Some agent, agent.Logger)

        match requireLoaded provider with
        | Some err -> async { return err }
        | None ->
            match cmd with
            | OrderContextCmd (ctxCmd, ctx) ->
                async {
                    if agent.IsSome then
                        do! agent.Value |> Logging.setComponentName (Some "OrderContext")

                    return
                        ctx
                        |> OrderContextService.evaluate logger provider ctxCmd
                        |> Result.map (OrderContextResult >> OrderContextResp)
                }

            | OrderPlanCmd (UpdateOrderPlan (tp, cmdOpt)) ->
                async {
                    if agent.IsSome then
                        do! agent.Value |> Logging.setComponentName (Some "TreatmentPlan")
                    return
                        OrderPlan.updateOrderPlan logger provider tp cmdOpt
                        |> OrderPlan.calculateTotals
                        |> OrderPlanUpdated
                        |> OrderPlanResp
                        |> Ok
                }

            | OrderPlanCmd (FilterOrderPlan tp) ->
                async {
                    return
                        tp
                        |> OrderPlan.calculateTotals
                        |> OrderPlanFiltered
                        |> OrderPlanResp
                        |> Ok
                }

            | FormularyCmd form ->
                async {
                    return
                        form
                        |> Formulary.get provider
                        |> Result.map FormularyResp
                }

            | ParenteraliaCmd par ->
                async {
                    return
                        par
                        |> Parenteralia.get provider
                        |> Result.mapError Array.singleton
                        |> Result.map ParenteraliaResp
                }

            | NutritionPlanCmd (InitNutritionPlan patient) ->
                async {
                    return
                        NutritionPlan.initNutritionPlan logger provider patient
                        |> Result.map (NutritionPlanInitialised >> NutritionPlanResp)
                }

            | NutritionPlanCmd (UpdateNutritionOrderContext (plan, label, ctx)) ->
                async {
                    return
                        NutritionPlan.updateNutritionOrderContext logger provider (plan, label, ctx)
                        |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }

            | NutritionPlanCmd (SelectNutritionOrderScenario (plan, label, ctx)) ->
                async {
                    return
                        NutritionPlan.selectNutritionOrderScenario logger provider (plan, label, ctx)
                        |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }

            | NutritionPlanCmd (NavigateNutritionOrderContext (plan, label, ctxCmd, ctx)) ->
                async {
                    return
                        NutritionPlan.navigateNutritionOrderContext logger provider (plan, label, ctxCmd, ctx)
                        |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }

            | NutritionPlanCmd (AddNutritionContext (plan, category)) ->
                async {
                    return
                        NutritionPlan.addNutritionContext logger provider (plan, category)
                        |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }

            | NutritionPlanCmd (RemoveNutritionContext (plan, id)) ->
                async {
                    return
                        NutritionPlan.removeNutritionContext (plan, id)
                        |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }

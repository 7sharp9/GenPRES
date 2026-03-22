namespace ServerApi

/// Agent-backed adapter implementations for the AppEnv ports.
/// Wraps all service calls through a MailboxProcessor for:
///   - Serialized access to the IResourceProvider (prevents concurrent mutation)
///   - Audit logging of each command/response pair
///   - Async boundary for all callers
module AgentAdapters =

    open Shared.Api
    open Informedica.GenForm.Lib
    open Informedica.Agents.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    // Open Shared.Types last so its Patient/OrderContext shadow GenForm's
    open Shared.Types


    /// Internal command DU covering all port operations.
    /// Each case carries the arguments needed by the corresponding service function.
    [<RequireQualifiedAccess>]
    type ServerCommand =
        // Formulary
        | GetFormulary of Formulary
        | GetParenteralia of Parenteralia
        // OrderContext
        | EvaluateOrderContext of OrderContextCommand * OrderContext
        // OrderPlan
        | UpdateOrderPlan of OrderPlan * (OrderContextCommand * OrderContext) option
        | FilterOrderPlan of OrderPlan
        // NutritionPlan
        | InitNutritionPlan of Patient
        | AddNutritionContext of NutritionPlan * NutritionCategory
        | RemoveNutritionContext of NutritionPlan * string
        | UpdateNutritionOrderContext of NutritionPlan * string * OrderContext
        | SelectNutritionOrderScenario of NutritionPlan * string * OrderContext
        | NavigateNutritionOrderContext of NutritionPlan * string * OrderContextCommand * OrderContext


    /// Internal response DU matching each command case.
    [<RequireQualifiedAccess>]
    type ServerResponse =
        | Formulary of Result<Formulary, string []>
        | Parenteralia of Result<Parenteralia, string []>
        | OrderContext of Result<OrderContext, string []>
        | OrderPlan of Result<OrderPlan, string []>
        | NutritionPlan of Result<NutritionPlan, string []>


    let private commandToString = function
        | ServerCommand.GetFormulary _ -> "GetFormulary"
        | ServerCommand.GetParenteralia _ -> "GetParenteralia"
        | ServerCommand.EvaluateOrderContext _ -> "EvaluateOrderContext"
        | ServerCommand.UpdateOrderPlan _ -> "UpdateOrderPlan"
        | ServerCommand.FilterOrderPlan _ -> "FilterOrderPlan"
        | ServerCommand.InitNutritionPlan _ -> "InitNutritionPlan"
        | ServerCommand.AddNutritionContext _ -> "AddNutritionContext"
        | ServerCommand.RemoveNutritionContext _ -> "RemoveNutritionContext"
        | ServerCommand.UpdateNutritionOrderContext _ -> "UpdateNutritionOrderContext"
        | ServerCommand.SelectNutritionOrderScenario _ -> "SelectNutritionOrderScenario"
        | ServerCommand.NavigateNutritionOrderContext _ -> "NavigateNutritionOrderContext"


    /// Create the command processor that dispatches to existing service functions.
    let private processCommand
        logAgent
        (logger: Informedica.Logging.Lib.Logger)
        (provider: Resources.IResourceProvider)
        (cmd: ServerCommand)
        : ServerResponse =

        let setComponent name =
            match logAgent with
            | Some a -> a |> Logging.setComponentName (Some name) |> Async.RunSynchronously
            | None -> ()

        match cmd with
        | ServerCommand.GetFormulary form ->
            form
            |> Formulary.get provider
            |> ServerResponse.Formulary

        | ServerCommand.GetParenteralia par ->
            par
            |> Parenteralia.get provider
            |> Result.mapError Array.singleton
            |> ServerResponse.Parenteralia

        | ServerCommand.EvaluateOrderContext (ctxCmd, ctx) ->
            setComponent "OrderContext"
            ctx
            |> OrderContextService.evaluate logger provider ctxCmd
            |> ServerResponse.OrderContext

        | ServerCommand.UpdateOrderPlan (tp, cmdOpt) ->
            setComponent "TreatmentPlan"
            OrderPlan.updateOrderPlan logger provider tp cmdOpt
            |> OrderPlan.calculateTotals
            |> Ok
            |> ServerResponse.OrderPlan

        | ServerCommand.FilterOrderPlan tp ->
            tp
            |> OrderPlan.calculateTotals
            |> Ok
            |> ServerResponse.OrderPlan

        | ServerCommand.InitNutritionPlan patient ->
            NutritionPlan.initNutritionPlan logger provider patient
            |> ServerResponse.NutritionPlan

        | ServerCommand.AddNutritionContext (plan, category) ->
            NutritionPlan.addNutritionContext logger provider (plan, category)
            |> ServerResponse.NutritionPlan

        | ServerCommand.RemoveNutritionContext (plan, id) ->
            NutritionPlan.removeNutritionContext (plan, id)
            |> ServerResponse.NutritionPlan

        | ServerCommand.UpdateNutritionOrderContext (plan, label, ctx) ->
            NutritionPlan.updateNutritionOrderContext logger provider (plan, label, ctx)
            |> ServerResponse.NutritionPlan

        | ServerCommand.SelectNutritionOrderScenario (plan, label, ctx) ->
            NutritionPlan.selectNutritionOrderScenario logger provider (plan, label, ctx)
            |> ServerResponse.NutritionPlan

        | ServerCommand.NavigateNutritionOrderContext (plan, label, ctxCmd, ctx) ->
            NutritionPlan.navigateNutritionOrderContext logger provider (plan, label, ctxCmd, ctx)
            |> ServerResponse.NutritionPlan


    /// Create the MailboxProcessor agent that serializes all service calls.
    let private createServerAgent logAgent logger provider =
        Agent.createReply<ServerCommand, ServerResponse>(fun cmd ->
            try
                writeDebugMessage $"[AGENT] <- {cmd |> commandToString}"
                let response = processCommand logAgent logger provider cmd
                writeDebugMessage $"[AGENT] -> {cmd |> commandToString} completed"
                response
            with ex ->
                writeErrorMessage $"[AGENT] error in {cmd |> commandToString}: {ex}"
                match cmd with
                | ServerCommand.GetFormulary _
                | ServerCommand.GetParenteralia _ ->
                    ServerResponse.Formulary (Error [| ex.Message |])
                | ServerCommand.EvaluateOrderContext _ ->
                    ServerResponse.OrderContext (Error [| ex.Message |])
                | ServerCommand.UpdateOrderPlan _
                | ServerCommand.FilterOrderPlan _ ->
                    ServerResponse.OrderPlan (Error [| ex.Message |])
                | ServerCommand.InitNutritionPlan _
                | ServerCommand.AddNutritionContext _
                | ServerCommand.RemoveNutritionContext _
                | ServerCommand.UpdateNutritionOrderContext _
                | ServerCommand.SelectNutritionOrderScenario _
                | ServerCommand.NavigateNutritionOrderContext _ ->
                    ServerResponse.NutritionPlan (Error [| ex.Message |])
        )


    /// Helper: post a command to the agent and extract the expected response case.
    let private postAsync (agent: Agent<_>) cmd extract =
        async {
            let! response = agent |> Agent.postAndAsyncReply cmd
            return extract response
        }


    let private extractFormulary = function
        | ServerResponse.Formulary r -> r
        | other -> Error [| $"Unexpected response: {other}" |]

    let private extractParenteralia = function
        | ServerResponse.Parenteralia r -> r
        | other -> Error [| $"Unexpected response: {other}" |]

    let private extractOrderContext = function
        | ServerResponse.OrderContext r -> r
        | other -> Error [| $"Unexpected response: {other}" |]

    let private extractOrderPlan = function
        | ServerResponse.OrderPlan r -> r
        | other -> Error [| $"Unexpected response: {other}" |]

    let private extractNutritionPlan = function
        | ServerResponse.NutritionPlan r -> r
        | other -> Error [| $"Unexpected response: {other}" |]


    /// Build an AppEnv backed by a single MailboxProcessor agent.
    /// All service calls are serialized through the agent, providing
    /// thread-safe access to the provider and audit logging.
    let makeAppEnv (provider: Resources.IResourceProvider) : AppEnv =
        let logAgent, logger =
            match Logging.loggingLevel with
            | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
            | Some level ->
                let a = Logging.getLogger level Logging.OrderLogger
                (Some a, a.Logger)

        let agent = createServerAgent logAgent logger provider

        {
            formulary =
                {
                    getFormulary = fun form ->
                        postAsync agent (ServerCommand.GetFormulary form) extractFormulary
                    getParenteralia = fun par ->
                        postAsync agent (ServerCommand.GetParenteralia par) extractParenteralia
                }

            orderContext =
                {
                    evaluate = fun ctxCmd ctx ->
                        postAsync agent (ServerCommand.EvaluateOrderContext (ctxCmd, ctx)) extractOrderContext
                }

            orderPlan =
                {
                    updateOrderPlan = fun tp cmdOpt ->
                        postAsync agent (ServerCommand.UpdateOrderPlan (tp, cmdOpt)) extractOrderPlan
                    filterOrderPlan = fun tp ->
                        postAsync agent (ServerCommand.FilterOrderPlan tp) extractOrderPlan
                }

            nutritionPlan =
                {
                    initNutritionPlan = fun patient ->
                        postAsync agent (ServerCommand.InitNutritionPlan patient) extractNutritionPlan
                    addNutritionContext = fun (plan, category) ->
                        postAsync agent (ServerCommand.AddNutritionContext (plan, category)) extractNutritionPlan
                    removeNutritionContext = fun (plan, id) ->
                        postAsync agent (ServerCommand.RemoveNutritionContext (plan, id)) extractNutritionPlan
                    updateNutritionOrderContext = fun (plan, label, ctx) ->
                        postAsync agent (ServerCommand.UpdateNutritionOrderContext (plan, label, ctx)) extractNutritionPlan
                    selectNutritionOrderScenario = fun (plan, label, ctx) ->
                        postAsync agent (ServerCommand.SelectNutritionOrderScenario (plan, label, ctx)) extractNutritionPlan
                    navigateNutritionOrderContext = fun (plan, label, ctxCmd, ctx) ->
                        postAsync agent (ServerCommand.NavigateNutritionOrderContext (plan, label, ctxCmd, ctx)) extractNutritionPlan
                }

            requireLoaded = fun () ->
                let info = provider.GetResourceInfo()
                if info.IsLoaded then None
                else
                    info.Messages
                    |> Array.map (fun msg -> FormLogging.formatMessage msg)
                    |> Some
        }

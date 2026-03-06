namespace Shared


module Api =


    open Types


    type Command =
        | OrderContextCmd of OrderContextCommand
        | OrderPlanCmd of OrderPlanCommand
        | FormularyCmd of Formulary
        | ParenteraliaCmd of Parenteralia

    and OrderContextCommand =
        | UpdateOrderContext of OrderContext
        | SelectOrderScenario of OrderContext
        | UpdateOrderScenario of OrderContext
        | ResetOrderScenario of OrderContext
        | ReloadResources of OrderContext
        // Frequency property commands
        | DecreaseScheduleFrequencyProperty of OrderContext
        | IncreaseScheduleFrequencyProperty of OrderContext
        | SetMinScheduleFrequencyProperty of OrderContext
        | SetMaxScheduleFrequencyProperty of OrderContext
        | SetMedianScheduleFrequencyProperty of OrderContext
        // DoseQuantity property commands (ntimes = number of times to adjust)
        | DecreaseOrderableDoseQuantityProperty of OrderContext * ntimes: int
        | IncreaseOrderableDoseQuantityProperty of OrderContext * ntimes: int
        | SetMinOrderableDoseQuantityProperty of OrderContext
        | SetMaxOrderableDoseQuantityProperty of OrderContext
        | SetMedianOrderableDoseQuantityProperty of OrderContext
        // DoseRate property commands (ntimes = number of times to adjust)
        | DecreaseOrderableDoseRateProperty of OrderContext * ntimes: int
        | IncreaseOrderableDoseRateProperty of OrderContext * ntimes: int
        | SetMinOrderableDoseRateProperty of OrderContext
        | SetMaxOrderableDoseRateProperty of OrderContext
        | SetMedianOrderableDoseRateProperty of OrderContext
        // Component Quantity property commands (cmp = component, ntimes = number of times to adjust)
        | DecreaseComponentOrderableQuantityProperty of OrderContext * cmp: string * ntimes: int
        | IncreaseComponentOrderableQuantityProperty of OrderContext * cmp: string * ntimes: int
        | SetMinComponentOrderableQuantityProperty of OrderContext * cmp: string
        | SetMaxComponentOrderableQuantityProperty of OrderContext * cmp: string
        | SetMedianComponentOrderableQuantityProperty of OrderContext * cmp: string

    and OrderPlanCommand =
        | UpdateOrderPlan of OrderPlan
        | FilterOrderPlan of OrderPlan

    type Response =
        | OrderContextResp of OrderContextResponse
        | OrderPlanResp of OrderPlanResponse
        | FormularyResp of Formulary
        | ParentaraliaResp of Parenteralia

    and OrderContextResponse =
        | OrderContextSelected of OrderContext
        | OrderContextUpdated of OrderContext
        | OrderContextRefreshed of OrderContext
        | ResourcesReloaded of OrderContext

    and OrderPlanResponse =
        | OrderPlanFiltered of OrderPlan
        | OrderPlanUpdated of OrderPlan


    module Command =

        let toString = function
            | OrderContextCmd (UpdateOrderContext _) -> "UpdateOrderContext"
            | OrderContextCmd (SelectOrderScenario _) -> "SelectOrderScenario"
            | OrderContextCmd (UpdateOrderScenario _) -> "UpdateOrderScenario"
            | OrderContextCmd (ResetOrderScenario _) -> "ResetOrderScenario"
            | OrderContextCmd (ReloadResources _) -> "ReloadResources"
            | OrderContextCmd (DecreaseScheduleFrequencyProperty _) -> "DecreaseScheduleFrequencyProperty"
            | OrderContextCmd (IncreaseScheduleFrequencyProperty _) -> "IncreaseScheduleFrequencyProperty"
            | OrderContextCmd (SetMinScheduleFrequencyProperty _) -> "SetMinScheduleFrequencyProperty"
            | OrderContextCmd (SetMaxScheduleFrequencyProperty _) -> "SetMaxScheduleFrequencyProperty"
            | OrderContextCmd (SetMedianScheduleFrequencyProperty _) -> "SetMedianScheduleFrequencyProperty"
            | OrderContextCmd (DecreaseOrderableDoseQuantityProperty (_, ntimes)) -> $"DecreaseOrderableDoseQuantityProperty ntimes={ntimes}"
            | OrderContextCmd (IncreaseOrderableDoseQuantityProperty (_, ntimes)) -> $"IncreaseOrderableDoseQuantityProperty ntimes={ntimes}"
            | OrderContextCmd (SetMinOrderableDoseQuantityProperty _) -> "SetMinOrderableDoseQuantityProperty"
            | OrderContextCmd (SetMaxOrderableDoseQuantityProperty _) -> "SetMaxOrderableDoseQuantityProperty"
            | OrderContextCmd (SetMedianOrderableDoseQuantityProperty _) -> "SetMedianOrderableDoseQuantityProperty"
            | OrderContextCmd (DecreaseOrderableDoseRateProperty (_, ntimes)) -> $"DecreaseOrderableDoseRateProperty ntimes={ntimes}"
            | OrderContextCmd (IncreaseOrderableDoseRateProperty (_, ntimes)) -> $"IncreaseOrderableDoseRateProperty ntimes={ntimes}"
            | OrderContextCmd (SetMinOrderableDoseRateProperty _) -> "SetMinOrderableDoseRateProperty"
            | OrderContextCmd (SetMaxOrderableDoseRateProperty _) -> "SetMaxOrderableDoseRateProperty"
            | OrderContextCmd (SetMedianOrderableDoseRateProperty _) -> "SetMedianOrderableDoseRateProperty"
            | OrderContextCmd (DecreaseComponentOrderableQuantityProperty (_, cmp, ntimes)) -> $"DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes}"
            | OrderContextCmd (IncreaseComponentOrderableQuantityProperty (_, cmp, ntimes)) -> $"IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes}"
            | OrderContextCmd (SetMinComponentOrderableQuantityProperty (_, cmp)) -> $"SetMinComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd (SetMaxComponentOrderableQuantityProperty (_, cmp)) -> $"SetMaxComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd (SetMedianComponentOrderableQuantityProperty (_, cmp)) -> $"SetMedianComponentQuantityProperty cmp={cmp}"

            | OrderPlanCmd (UpdateOrderPlan _) -> "UpdatedOrderPlan"
            | OrderPlanCmd (FilterOrderPlan _) -> "FilterOrderPlan"
            | FormularyCmd _ -> "FormularyCmd"
            | ParenteraliaCmd _ -> "ParenteraliaCmd"


    /// Defines how routes are generated on server and mapped from the client
    let routerPaths typeName method = $"/api/%s{typeName}/%s{method}"


    /// A type that specifies the communication protocol between client and server
    /// to learn more read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            processCommand: Command -> Async<Result<Response, string[]>>
            testApi: unit -> Async<string>
        }

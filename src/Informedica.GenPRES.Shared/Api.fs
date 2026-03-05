namespace Shared


module Api =


    open Types


    type Command =
        | OrderContextCmd of OrderContextCommand
        | NutritionContextCmd of OrderContextCommand
        | TreatmentPlanCmd of TreatmentPlanCommand
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


    and TreatmentPlanCommand =
        | UpdateTreatmentPlan of TreatmentPlan
        | FilterTreatmentPlan of TreatmentPlan

    type Response =
        | OrderContextResp of OrderContextResponse
        | TreatmentPlanResp of TreatmentPlanResponse
        | FormularyResp of Formulary
        | ParentaraliaResp of Parenteralia

    and OrderContextResponse =
        | OrderContextSelected of OrderContext
        | OrderContextUpdated of OrderContext
        | OrderContextRefreshed of OrderContext
        | ResourcesReloaded of OrderContext

    and TreatmentPlanResponse =
        | TreatmentPlanFiltered of TreatmentPlan
        | TreatmentPlanUpdated of TreatmentPlan


    module Command =

        let toString = function
            | OrderContextCmd (UpdateOrderContext _) -> "OrderContext-UpdateOrderContext"
            | OrderContextCmd (SelectOrderScenario _) -> "OrderContext-SelectOrderScenario"
            | OrderContextCmd (UpdateOrderScenario _) -> "OrderContext-UpdateOrderScenario"
            | OrderContextCmd (ResetOrderScenario _) -> "OrderContext-ResetOrderScenario"
            | OrderContextCmd (ReloadResources _) -> "OrderContext-ReloadResources"
            | OrderContextCmd (DecreaseScheduleFrequencyProperty _) -> "OrderContext-DecreaseScheduleFrequencyProperty"
            | OrderContextCmd (IncreaseScheduleFrequencyProperty _) -> "OrderContext-IncreaseScheduleFrequencyProperty"
            | OrderContextCmd (SetMinScheduleFrequencyProperty _) -> "OrderContext-SetMinScheduleFrequencyProperty"
            | OrderContextCmd (SetMaxScheduleFrequencyProperty _) -> "OrderContext-SetMaxScheduleFrequencyProperty"
            | OrderContextCmd (SetMedianScheduleFrequencyProperty _) -> "OrderContext-SetMedianScheduleFrequencyProperty"
            | OrderContextCmd (DecreaseOrderableDoseQuantityProperty (_, ntimes)) -> $"OrderContext-DecreaseOrderableDoseQuantityProperty ntimes={ntimes}"
            | OrderContextCmd (IncreaseOrderableDoseQuantityProperty (_, ntimes)) -> $"OrderContext-IncreaseOrderableDoseQuantityProperty ntimes={ntimes}"
            | OrderContextCmd (SetMinOrderableDoseQuantityProperty _) -> "OrderContext-SetMinOrderableDoseQuantityProperty"
            | OrderContextCmd (SetMaxOrderableDoseQuantityProperty _) -> "OrderContext-SetMaxOrderableDoseQuantityProperty"
            | OrderContextCmd (SetMedianOrderableDoseQuantityProperty _) -> "OrderContext-SetMedianOrderableDoseQuantityProperty"
            | OrderContextCmd (DecreaseOrderableDoseRateProperty (_, ntimes)) -> $"OrderContext-DecreaseOrderableDoseRateProperty ntimes={ntimes}"
            | OrderContextCmd (IncreaseOrderableDoseRateProperty (_, ntimes)) -> $"OrderContext-IncreaseOrderableDoseRateProperty ntimes={ntimes}"
            | OrderContextCmd (SetMinOrderableDoseRateProperty _) -> "OrderContext-SetMinOrderableDoseRateProperty"
            | OrderContextCmd (SetMaxOrderableDoseRateProperty _) -> "OrderContext-SetMaxOrderableDoseRateProperty"
            | OrderContextCmd (SetMedianOrderableDoseRateProperty _) -> "OrderContext-SetMedianOrderableDoseRateProperty"
            | OrderContextCmd (DecreaseComponentOrderableQuantityProperty (_, cmp, ntimes)) -> $"OrderContext-DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes}"
            | OrderContextCmd (IncreaseComponentOrderableQuantityProperty (_, cmp, ntimes)) -> $"OrderContext-IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes}"
            | OrderContextCmd (SetMinComponentOrderableQuantityProperty (_, cmp)) -> $"OrderContext-SetMinComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd (SetMaxComponentOrderableQuantityProperty (_, cmp)) -> $"OrderContext-SetMaxComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd (SetMedianComponentOrderableQuantityProperty (_, cmp)) -> $"OrderContext-SetMedianComponentQuantityProperty cmp={cmp}"

            | NutritionContextCmd (UpdateOrderContext _) -> "NutritionContext-UpdateOrderContext"
            | NutritionContextCmd (SelectOrderScenario _) -> "NutritionContext-SelectOrderScenario"
            | NutritionContextCmd (UpdateOrderScenario _) -> "NutritionContext-UpdateOrderScenario"
            | NutritionContextCmd (ResetOrderScenario _) -> "NutritionContext-ResetOrderScenario"
            | NutritionContextCmd (ReloadResources _) -> "NutritionContext-ReloadResources"
            | NutritionContextCmd (DecreaseScheduleFrequencyProperty _) -> "NutritionContext-DecreaseScheduleFrequencyProperty"
            | NutritionContextCmd (IncreaseScheduleFrequencyProperty _) -> "NutritionContext-IncreaseScheduleFrequencyProperty"
            | NutritionContextCmd (SetMinScheduleFrequencyProperty _) -> "NutritionContext-SetMinScheduleFrequencyProperty"
            | NutritionContextCmd (SetMaxScheduleFrequencyProperty _) -> "NutritionContext-SetMaxScheduleFrequencyProperty"
            | NutritionContextCmd (SetMedianScheduleFrequencyProperty _) -> "NutritionContext-SetMedianScheduleFrequencyProperty"
            | NutritionContextCmd (DecreaseOrderableDoseQuantityProperty (_, ntimes)) -> $"NutritionContext-DecreaseOrderableDoseQuantityProperty ntimes={ntimes}"
            | NutritionContextCmd (IncreaseOrderableDoseQuantityProperty (_, ntimes)) -> $"NutritionContext-IncreaseOrderableDoseQuantityProperty ntimes={ntimes}"
            | NutritionContextCmd (SetMinOrderableDoseQuantityProperty _) -> "NutritionContext-SetMinOrderableDoseQuantityProperty"
            | NutritionContextCmd (SetMaxOrderableDoseQuantityProperty _) -> "NutritionContext-SetMaxOrderableDoseQuantityProperty"
            | NutritionContextCmd (SetMedianOrderableDoseQuantityProperty _) -> "NutritionContext-SetMedianOrderableDoseQuantityProperty"
            | NutritionContextCmd (DecreaseOrderableDoseRateProperty (_, ntimes)) -> $"NutritionContext-DecreaseOrderableDoseRateProperty ntimes={ntimes}"
            | NutritionContextCmd (IncreaseOrderableDoseRateProperty (_, ntimes)) -> $"NutritionContext-IncreaseOrderableDoseRateProperty ntimes={ntimes}"
            | NutritionContextCmd (SetMinOrderableDoseRateProperty _) -> "NutritionContext-SetMinOrderableDoseRateProperty"
            | NutritionContextCmd (SetMaxOrderableDoseRateProperty _) -> "NutritionContext-SetMaxOrderableDoseRateProperty"
            | NutritionContextCmd (SetMedianOrderableDoseRateProperty _) -> "NutritionContext-SetMedianOrderableDoseRateProperty"
            | NutritionContextCmd (DecreaseComponentOrderableQuantityProperty (_, cmp, ntimes)) -> $"NutritionContext-DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes}"
            | NutritionContextCmd (IncreaseComponentOrderableQuantityProperty (_, cmp, ntimes)) -> $"NutritionContext-IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes}"
            | NutritionContextCmd (SetMinComponentOrderableQuantityProperty (_, cmp)) -> $"NutritionContext-SetMinComponentQuantityProperty cmp={cmp}"
            | NutritionContextCmd (SetMaxComponentOrderableQuantityProperty (_, cmp)) -> $"NutritionContext-SetMaxComponentQuantityProperty cmp={cmp}"
            | NutritionContextCmd (SetMedianComponentOrderableQuantityProperty (_, cmp)) -> $"NutritionContext-SetMedianComponentQuantityProperty cmp={cmp}"

            | TreatmentPlanCmd _ -> "TreatmentPlanCmd"
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

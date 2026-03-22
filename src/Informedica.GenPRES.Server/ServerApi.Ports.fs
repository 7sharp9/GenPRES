namespace ServerApi

open Shared.Types
open Shared.Api


type IFormularyPort =
    {
        getDoseRules : Formulary -> Async<Result<Formulary, string []>>
        getParenteralia : Parenteralia -> Async<Result<Parenteralia, string []>>
    }


type IOrderContextPort =
    {
        evaluate : OrderContextCommand -> OrderContext -> Async<Result<OrderContext, string []>>
    }


type IOrderPlanPort =
    {
        updateOrderPlan : OrderPlan -> (OrderContextCommand * OrderContext) option -> Async<Result<OrderPlan, string []>>
        filterOrderPlan : OrderPlan -> Async<Result<OrderPlan, string []>>
    }


type INutritionPlanPort =
    {
        initNutritionPlan : Patient -> Async<Result<NutritionPlan, string []>>
        addNutritionContext : NutritionPlan * NutritionCategory -> Async<Result<NutritionPlan, string []>>
        removeNutritionContext : NutritionPlan * string -> Async<Result<NutritionPlan, string []>>
        updateNutritionOrderContext : NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string []>>
        selectNutritionOrderScenario : NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string []>>
        navigateNutritionOrderContext : NutritionPlan * string * OrderContextCommand * OrderContext -> Async<Result<NutritionPlan, string []>>
    }


type AppEnv =
    {
        formulary : IFormularyPort
        orderContext : IOrderContextPort
        orderPlan : IOrderPlanPort
        nutritionPlan : INutritionPlanPort
        requireLoaded : unit -> Option<Result<Response, string []>>
    }

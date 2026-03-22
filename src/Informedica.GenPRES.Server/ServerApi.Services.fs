namespace ServerApi


module Formulary =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types
    open Shared


    let mapFormularyToFilter (form: Formulary)=
        { Filter.doseFilter with
            Generic = form.Generic
            Indication = form.Indication
            Route = form.Route
            Form = form.Form
            DoseType = form.DoseType |> Option.map Mappers.mapFromSharedDoseTypeToOrderDoseType
            Patient =
                form.Patient
                |> Option.map Mappers.mapFromSharedPatient
                |> Option.defaultValue Patient.patient
        }
        |> Filter.calcPMAge


    let selectIfOne sel xs =
        match sel, xs with
        | None, [|x|] -> Some x
        | _ -> sel


    let checkDoseRules provider pat (dsrs : DoseRule []) =
        let routeMapping = Api.getRouteMapping provider

        let empt, rs =
            dsrs
            |> Array.distinctBy (fun dr -> dr.Generic, dr.Form, dr.Route, dr.DoseType)
            |> Array.map (Check.checkDoseRule routeMapping pat)
            |> Array.partition (fun c ->
                c.didPass |> Array.isEmpty &&
                c.didNotPass |> Array.isEmpty
            )

        rs
        |> Array.filter (_.didNotPass >> Array.isEmpty >> not)
        |> Array.collect _.didNotPass
        |> Array.filter String.notEmpty
        |> Array.distinct
        |> function
            | [||] ->
                [|
                    for e in empt do
                        $"geen doseer bewaking gevonden voor {e.doseRule.Generic}"
                |]
                |> Array.distinct

            | xs -> xs


    let get provider (form : Formulary) =
        let filter = form |> mapFormularyToFilter

        $"""

Formulary filter:
Patient: {filter.Patient |> Patient.toString}
Indication: {filter.Indication |> Option.defaultValue ""}
Generic: {filter.Generic |> Option.defaultValue ""}
Route: {filter.Route |> Option.defaultValue ""}
Shape: {filter.Form |> Option.defaultValue ""}
DoseType : {filter.DoseType |> Option.map DoseType.toDescription |> Option.defaultValue ""}

"""
        |> writeDebugMessage

        let dsrs = Formulary.getDoseRules provider filter

        writeDebugMessage $"Found: {dsrs |> Array.length} formulary dose rules"
        let form =
            { form with
                Generics = dsrs |> DoseRule.generics
                Indications = dsrs |> DoseRule.indications
                Routes = dsrs |> DoseRule.routes
                Forms = dsrs |> DoseRule.forms
                DoseTypes = dsrs |> DoseRule.doseTypes |> Array.map Mappers.mapFromOrderDoseTypeToSharedDoseType
                PatientCategories = dsrs |> DoseRule.patientCategories
            }
            |> fun form ->
                { form with
                    Generic = form.Generics |> selectIfOne form.Generic
                    Indication = form.Indications |> selectIfOne form.Indication
                    Route = form.Routes |> selectIfOne form.Route
                    Form = form.Forms |> selectIfOne form.Form
                    DoseType = form.DoseTypes |> selectIfOne form.DoseType
                    PatientCategory = form.PatientCategories |> selectIfOne form.PatientCategory
                }
            |> fun form ->
                { form with
                    Markdown =
                        match form.Generic, form.Indication, form.Route with
                        | Some _, Some _, Some _ ->
                            writeDebugMessage $"start checking {dsrs |> Array.length} rules"

                            let s =
                                dsrs
                                |> checkDoseRules provider filter.Patient
                                |> Array.map (fun s ->
                                    match s |> String.split "\t" with
                                    | [| s1; _; p; s2 |] ->
                                        if dsrs |> Array.length = 1 then $"{s1} {s2}"
                                        else
                                            $"{s1} {p} {s2}"
                                    | _ -> s
                                )
                                |> Array.map (fun s -> $"* {s}")
                                |> String.concat "\n"
                                |> fun s -> if s |> String.isNullOrWhiteSpace then "Ok!" else s

                            writeDebugMessage $"finished checking {dsrs |> Array.length} rules"

                            dsrs
                            |> DoseRule.Print.toMarkdown
                            |> fun md ->
                                $"{md}\n\n### Doseer controle volgens de G-Standaard\n\n{s}"

                        | _ -> ""
                }
        $"""

Formulary:
Patients: {form.PatientCategories |> Array.length}
Indication: {form.Indications |> Array.length}
Generic: {form.Generics |> Array.length}
Route: {form.Routes |> Array.length}
Shapes: {form.Forms |> Array.length}
DoseTypes: {form.DoseTypes |> Array.length}

"""
        |> writeDebugMessage

        Ok form


module Parenteralia =

    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib

    type Parenteralia = Shared.Types.Parenteralia


    let get provider (par : Parenteralia) : Result<Parenteralia, string> =
        writeInfoMessage $"getting parenteralia for {par.Generic}"

        let srs =
            Formulary.getSolutionRules provider
                par.Generic
                par.Form
                par.Route

        let gens = srs |> SolutionRule.generics
        let shps = srs |> SolutionRule.forms
        let rtes = srs |> SolutionRule.routes

        { par with
            Generics = gens
            Forms = shps
            Routes = rtes
            Generic =
                if gens |> Array.length = 1 then Some gens[0]
                else
                    par.Generic
            Form =
                if shps |> Array.length = 1 then Some shps[0]
                else
                    par.Form
            Route =
                if rtes |> Array.length = 1 then Some rtes[0]
                else
                    par.Route

            Markdown =
                if par.Generic |> Option.isNone then ""
                else
                    srs
                    |> SolutionRule.Print.toMarkdown ""
        }
        |> Ok


module Order =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types


    let getTotals age wghtInGram (ords: Order []) =
        let wghtInKg =
            wghtInGram
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Weight.gram)
            |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

        let age =
            age
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)

        ords
        |> Array.map Mappers.Order.mapFromSharedToOrder
        |> Totals.getTotals age wghtInKg
        |> Mappers.mapToTotals


module OrderContext =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared
    open Shared.Types
    open Mappers


    let setDemoVersion ctx =
        { ctx with
            DemoVersion =
                Env.getItem "GENPRES_PROD"
                |> Option.map (fun v -> v <> "1")
                |> Option.defaultValue true
        }


    let updateIntake (ctx : OrderContext) =
        { ctx with
            Intake =
                let w = ctx.Patient |> Models.Patient.getWeight |> Option.map int
                let a =
                    ctx.Patient
                    |> Models.Patient.getAgeInDays
                    |> Option.map int

                ctx.Scenarios
                |> Array.map _.Order
                |> Order.getTotals a w
        }


    let private extractServerCtx = function
        | OrderContext.UpdateOrderContext ctx
        | OrderContext.SelectOrderScenario ctx
        | OrderContext.UpdateOrderScenario ctx
        | OrderContext.ResetOrderScenario ctx
        | OrderContext.ReloadResources ctx
        // Frequency property commands
        | OrderContext.DecreaseScheduleFrequencyProperty ctx
        | OrderContext.IncreaseScheduleFrequencyProperty ctx
        | OrderContext.SetMinScheduleFrequencyProperty ctx
        | OrderContext.SetMaxScheduleFrequencyProperty ctx
        | OrderContext.SetMedianScheduleFrequencyProperty ctx
        // DoseQuantity property commands
        | OrderContext.SetMinOrderableDoseQuantityProperty ctx
        | OrderContext.SetMaxOrderableDoseQuantityProperty ctx
        | OrderContext.SetMedianOrderableDoseQuantityProperty ctx
        // DoseRate property commands
        | OrderContext.SetMinOrderableDoseRateProperty ctx
        | OrderContext.SetMaxOrderableDoseRateProperty ctx
        | OrderContext.SetMedianOrderableDoseRateProperty ctx -> ctx
        | OrderContext.DecreaseOrderableDoseQuantityProperty (ctx, _, _)
        | OrderContext.IncreaseOrderableDoseQuantityProperty (ctx, _, _)
        | OrderContext.DecreaseOrderableDoseRateProperty (ctx, _, _)
        | OrderContext.IncreaseOrderableDoseRateProperty (ctx, _, _) -> ctx
        | OrderContext.DecreaseComponentQuantityProperty (ctx, _, _, _)
        | OrderContext.IncreaseComponentQuantityProperty (ctx, _, _, _) -> ctx
        | OrderContext.SetMinComponentQuantityProperty (ctx, _)
        | OrderContext.SetMaxComponentQuantityProperty (ctx, _)
        | OrderContext.SetMedianComponentQuantityProperty (ctx, _) -> ctx


    let evaluate logger provider (cmd: Api.OrderContextCommand) (ctx: OrderContext) : Result<OrderContext, string []> =
        let map = mapToShared ctx >> updateIntake >> setDemoVersion

        let pat =
            ctx.Patient
            |> mapFromSharedPatient
            |> Patient.calcPMAge

        let toServerCmd serverCtx =
            match cmd with
            | Api.UpdateOrderContext -> serverCtx |> OrderContext.UpdateOrderContext
            | Api.SelectOrderScenario -> serverCtx |> OrderContext.SelectOrderScenario
            | Api.UpdateOrderScenario -> serverCtx |> OrderContext.UpdateOrderScenario
            | Api.ResetOrderScenario -> serverCtx |> OrderContext.ResetOrderScenario
            | Api.ReloadResources _ -> serverCtx |> OrderContext.ReloadResources
            // Frequency property commands
            | Api.DecreaseScheduleFrequencyProperty -> serverCtx |> OrderContext.DecreaseScheduleFrequencyProperty
            | Api.IncreaseScheduleFrequencyProperty -> serverCtx |> OrderContext.IncreaseScheduleFrequencyProperty
            | Api.SetMinScheduleFrequencyProperty -> serverCtx |> OrderContext.SetMinScheduleFrequencyProperty
            | Api.SetMaxScheduleFrequencyProperty -> serverCtx |> OrderContext.SetMaxScheduleFrequencyProperty
            | Api.SetMedianScheduleFrequencyProperty -> serverCtx |> OrderContext.SetMedianScheduleFrequencyProperty
            // DoseQuantity property commands
            | Api.DecreaseOrderableDoseQuantityProperty (ntimes, useCalc) -> OrderContext.DecreaseOrderableDoseQuantityProperty (serverCtx, ntimes, useCalc)
            | Api.IncreaseOrderableDoseQuantityProperty (ntimes, useCalc) -> OrderContext.IncreaseOrderableDoseQuantityProperty (serverCtx, ntimes, useCalc)
            | Api.SetMinOrderableDoseQuantityProperty -> OrderContext.SetMinOrderableDoseQuantityProperty serverCtx
            | Api.SetMaxOrderableDoseQuantityProperty -> OrderContext.SetMaxOrderableDoseQuantityProperty serverCtx
            | Api.SetMedianOrderableDoseQuantityProperty -> OrderContext.SetMedianOrderableDoseQuantityProperty serverCtx
            // DoseRate property commands
            | Api.DecreaseOrderableDoseRateProperty (ntimes, useCalc) -> OrderContext.DecreaseOrderableDoseRateProperty (serverCtx, ntimes, useCalc)
            | Api.IncreaseOrderableDoseRateProperty (ntimes, useCalc) -> OrderContext.IncreaseOrderableDoseRateProperty (serverCtx, ntimes, useCalc)
            | Api.SetMinOrderableDoseRateProperty -> serverCtx |> OrderContext.SetMinOrderableDoseRateProperty
            | Api.SetMaxOrderableDoseRateProperty -> serverCtx |> OrderContext.SetMaxOrderableDoseRateProperty
            | Api.SetMedianOrderableDoseRateProperty -> serverCtx |> OrderContext.SetMedianOrderableDoseRateProperty
            // Component Quantity property commands
            | Api.DecreaseComponentOrderableQuantityProperty (cmp, ntimes, useCalc) -> OrderContext.DecreaseComponentQuantityProperty (serverCtx, cmp, ntimes, useCalc)
            | Api.IncreaseComponentOrderableQuantityProperty (cmp, ntimes, useCalc) -> OrderContext.IncreaseComponentQuantityProperty (serverCtx, cmp, ntimes, useCalc)
            | Api.SetMinComponentOrderableQuantityProperty cmp -> OrderContext.SetMinComponentQuantityProperty (serverCtx, cmp)
            | Api.SetMaxComponentOrderableQuantityProperty cmp -> OrderContext.SetMaxComponentQuantityProperty (serverCtx, cmp)
            | Api.SetMedianComponentOrderableQuantityProperty cmp -> OrderContext.SetMedianComponentQuantityProperty (serverCtx, cmp)

        match cmd with
        | Api.ReloadResources password
            when Informedica.Utils.Lib.Env.getItem "GENPRES_RELOAD_PASSWORD"
                 |> Option.map (fun expected -> password <> expected)
                 |> Option.defaultValue true ->  // no env var = always reject
            Error [| "Invalid password" |]
        | _ ->
            try
                ctx
                |> mapFromShared logger provider pat
                |> toServerCmd
                |> OrderContext.logOrderContext logger "start eval"
                |> OrderContext.evaluate logger provider
                |> Result.get
                |> OrderContext.logOrderContext logger "finish eval"
                |> extractServerCtx
                |> map
                |> Ok
            with
            | e ->
                writeErrorMessage $"errored:\n{e}"
                Error [| e.Message |]


module OrderPlan =

    open Shared
    open Shared.Types

    module OrderLogger = Informedica.GenOrder.Lib.OrderLogging


    let updateOrderPlan logger provider (tp : OrderPlan) (cmdOpt: (Api.OrderContextCommand * OrderContext) option) =
        match cmdOpt with
        | None -> tp
        | Some (cmd, ctx) ->
            ctx
            |> OrderContext.evaluate logger provider cmd
            |> Result.map (fun newCtx ->
                let newOsc = newCtx.Scenarios |> Array.tryExactlyOne

                { tp with
                    Selected = newOsc
                    Scenarios =
                        match newOsc with
                        | None -> tp.Scenarios
                        | Some newOsc ->
                            tp.Scenarios
                            |> Array.map (fun sc ->
                                if sc |> Models.OrderScenario.eqs newOsc then newOsc
                                else sc
                            )
                }
            )
            |> Result.defaultValue tp


    let calculateTotals (tp : OrderPlan) =
        { tp with
            Totals =
                let w = tp.Patient |> Models.Patient.getWeight |> Option.map int
                let a = tp.Patient |> Models.Patient.getAgeInDays |> Option.map int

                let scs =
                    if tp.Filtered |> Array.isEmpty then tp.Scenarios
                    else
                        tp.Scenarios
                        |> Array.filter (fun sc -> tp.Filtered |> Array.exists ((=) sc))

                scs
                |> Array.map _.Order
                |> Order.getTotals a w
        }


module NutritionPlan =

    open Shared
    open Shared.Types

    type NutritionDoseRuleSet = {
        Label: string
        Indications: string []
        Generics: string []
        DoseTypes: (string * string) []
    }

    let tpnDoseRuleSet =
        {
            Label = "Totale Parenterale Voeding"
            Indications = [|
                "Standaard Totale Parenterale Voeding"
                "Variabele Totale Parenterale Voeding"
                "Neonatale Parenterale Voeding"
                "Totale Parenterale Voeding"
            |]
            Generics = [|
                "Primene"
                "NICU Mix"
                "Samenstelling B"
                "Samenstelling C"
                "Samenstelling D"
                "Samenstelling E"
                "Numeta G13%E 2CZ"
                "Numeta G13%E 3CZ"
                "Numeta G16%E 2CZ"
                "Numeta G16%E 3CZ"
                "Numeta G19%E 2CZ"
                "Numeta G19%E 3CZ"
            |]
            DoseTypes = [||]
        }


    let enteralFeedingDoseRuleSet =
        {
            Label = "Enterale Voeding"
            Indications = [|
                "Enterale voeding"
            |]
            Generics = [|
                "Infatrini"
                "Nutrini"
                "Nutrini Energy"
                "Nutrini Energy Multi Fibre"
                "Nutrini Multi Fibre"
                "Nutrison"
                "Nutrison Energy"
                "Nutrison Energy Multi Fibre"
                "Nutrison Multi Fibre"
                "Nutrison Protein Plus"
                "Nutrison Protein Plus Multi Fibre"
                "Peptisorb"
                "Peptisorb Plus"
                "Moedermelk"
                "Nutrilon Premature"
                "Nutrilon Nenatal Start"
                "Nutrilon Nenatal 1"
            |]
            DoseTypes = [||]
        }


    let enteralSupplementDoseRuleSet =
        {
            Label = "Enteraal Supplement"
            Indications = [|
                "Enterale toevoeging"
            |]
            Generics = [|
                "Calogen neutraal pdr"
                "Fantomalt pdr"
                "Hero Baby 1 NS Comfort pdr"
                "Hero Baby 1 NS Pep pdr"
                "Hero Baby 1 NS Standaard pdr"
                "Hero Baby 2 NS Comfort pdr"
                "Hero Baby 2 NS Pep pdr"
                "Hero Baby 2 NS Standaard pdr"
                "Liquigen pdr"
                "Neocate Junior pdr"
                "Neocate LCP pdr"
                "Nutramigen 1 LGG pdr"
                "Nutramigen 2 LGG pdr"
                "Nutrilon 1 pdr"
                "Nutrilon Hypoallergeen 1 pdr"
                "Nutrilon Nenatal 1 pdr"
                "Nutrilon Nenatal BMF pdr"
                "Nutrilon Nenatal Protein Fortifier pdr"
                "Nutrilon Nenatal Start pdr"
                "Nutrilon Pepti 1 pdr"
                "Nutrilon Pepti Junior pdr"
                "Nutrison Advanced Peptisorb pdr"
                "Nutriton pdr"
            |]
            DoseTypes = [||]
        }


    let lipidDoseRuleSet =
        {
            Label = "Vetten"
            Indications = [|
                "Parenterale vetten"
            |]
            Generics = [|
                "Intralipid 20%"
                "SMOFlipid 20%"
            |]
            DoseTypes = [||]
        }


    let electrolyteGlucoseDoseRuleSet =
        {
            Label = "Elektrolyten/Glucose"
            Indications = [|
                "Parenterale suppletie"
            |]
            Generics = [|
                "NaCl 0,9%"
                "NaCl 3%"
                "Glucose 5%"
                "Glucose 10%"
                "Glucose 20%"
                "Glucose 50%"
                "calciumglubionat/calciumgluconaat"
                "KCl 7,4%"
                "KCl"
                "NaCl"
                "magnesiumsulfaat"
                "fosfaat"
                "calciumgluconaat"
            |]
            DoseTypes = [||]
        }


    let getDoseRuleSet = function
        | NutritionCategory.EnteralFeeding -> enteralFeedingDoseRuleSet
        | NutritionCategory.EnteralSupplement -> enteralSupplementDoseRuleSet
        | NutritionCategory.TPN -> tpnDoseRuleSet
        | NutritionCategory.Lipid -> lipidDoseRuleSet
        | NutritionCategory.ElectrolyteGlucose -> electrolyteGlucoseDoseRuleSet


    let calculateNutritionTotals (plan: NutritionPlan) =
        { plan with
            Totals =
                let w = plan.Patient |> Models.Patient.getWeight |> Option.map int
                let a = plan.Patient |> Models.Patient.getAgeInDays |> Option.map int
                plan.NutritionContexts
                |> Array.collect (fun nc -> nc.OrderContext.Scenarios |> Array.map _.Order)
                |> Order.getTotals a w
        }


    /// Discovers available filter options for a given OrderContext.
    /// Evaluates the context and intersects the resolved options with the configured values.
    let discoverFilterOptions logger provider ctx =
        match OrderContext.evaluate logger provider Api.UpdateOrderContext ctx with
        | Ok resolved ->
            { resolved with
                Filter =
                    { resolved.Filter with
                        Indications =
                            resolved.Filter.Indications
                            |> Array.filter (fun i -> ctx.Filter.Indications |> Array.exists ((=) i))
                        Generics =
                            resolved.Filter.Generics
                            |> Array.filter (fun g -> ctx.Filter.Generics |> Array.exists ((=) g))
                        DoseTypes =
                            resolved.Filter.DoseTypes
                            |> Array.filter (fun dt -> ctx.Filter.DoseTypes |> Array.exists ((=) dt))
                    }
            }
            |> Some
        | Error _ -> None


    let initNutritionPlan _logger _provider (patient: Patient) : Result<NutritionPlan, string[]> =
        [||]
        |> Models.NutritionPlan.create patient
        |> calculateNutritionTotals
        |> Ok


    /// Filters a resolved OrderContext's filter arrays against the configured
    /// dose rule set. If a configured array is non-empty, only matching values
    /// are kept; if empty, no restriction is applied.
    let filterByDoseRuleSet (drs: NutritionDoseRuleSet) (resolved: OrderContext) =
        { resolved with
            Filter =
                { resolved.Filter with
                    Indications =
                        if drs.Indications |> Array.isEmpty then resolved.Filter.Indications
                        else
                            resolved.Filter.Indications
                            |> Array.filter (fun i -> drs.Indications |> Array.contains i)
                    Generics =
                        if drs.Generics |> Array.isEmpty then resolved.Filter.Generics
                        else
                            resolved.Filter.Generics
                            |> Array.filter (fun g -> drs.Generics |> Array.contains g)
                    DoseTypes = resolved.Filter.DoseTypes
                }
        }


    let updateContext id resolved (plan: NutritionPlan) =
        let updatedContexts =
            plan.NutritionContexts
            |> Array.map (fun nc ->
                if nc.Id = id then
                    let drs = getDoseRuleSet nc.Category
                    { nc with OrderContext = resolved |> filterByDoseRuleSet drs }
                else nc
            )
        { plan with NutritionContexts = updatedContexts }
        |> calculateNutritionTotals


    let updateNutritionOrderContext logger provider (plan: NutritionPlan, id: string, ctx: OrderContext) : Result<NutritionPlan, string[]> =
        match OrderContext.evaluate logger provider Api.UpdateOrderContext ctx with
        | Ok resolved ->
            plan |> updateContext id resolved |> Ok
        | Error errs -> Error errs


    let navigateNutritionOrderContext logger provider (plan: NutritionPlan, id: string, ctxCmd: Api.OrderContextCommand, ctx: OrderContext) : Result<NutritionPlan, string[]> =
        match OrderContext.evaluate logger provider ctxCmd ctx with
        | Ok resolved ->
            plan |> updateContext id resolved |> Ok
        | Error errs -> Error errs


    let selectNutritionOrderScenario logger provider (plan: NutritionPlan, id: string, ctx: OrderContext) : Result<NutritionPlan, string[]> =
        match OrderContext.evaluate logger provider Api.SelectOrderScenario ctx with
        | Ok resolved ->
            plan |> updateContext id resolved |> Ok
        | Error errs -> Error errs


    let addNutritionContext logger provider (plan: NutritionPlan, category: NutritionCategory) : Result<NutritionPlan, string[]> =
        let drs = getDoseRuleSet category
        let ctx =
            Models.OrderContext.empty
            |> Models.OrderContext.setPatient plan.Patient
            |> fun c ->
                { c with
                    Filter =
                        { c.Filter with
                            Indications = drs.Indications
                            Generics = drs.Generics
                        }
                }
        match discoverFilterOptions logger provider ctx with
        | Some resolved ->
            let id = System.Guid.NewGuid().ToString()
            let nc = Models.NutritionContext.create id drs.Label category true resolved
            { plan with NutritionContexts = Array.append plan.NutritionContexts [| nc |] }
            |> calculateNutritionTotals
            |> Ok
        | None -> Error [| "Could not discover filter options for nutrition context" |]


    let removeNutritionContext (plan: NutritionPlan, id: string) : Result<NutritionPlan, string[]> =
        let removedCtx = plan.NutritionContexts |> Array.tryFind (fun nc -> nc.Id = id)
        let cascadeRemoveSupplements =
            removedCtx
            |> Option.map (fun nc -> nc.Category = NutritionCategory.EnteralFeeding)
            |> Option.defaultValue false
        { plan with
            NutritionContexts =
                plan.NutritionContexts
                |> Array.filter (fun nc ->
                    nc.Id <> id &&
                    (not cascadeRemoveSupplements || nc.Category <> NutritionCategory.EnteralSupplement)
                )
        }
        |> calculateNutritionTotals
        |> Ok

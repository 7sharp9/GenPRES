module App

open System
open Fable.Core
open Browser
open Fable.React
open Elmish
open Feliz.Router
open Fable.Remoting.Client
open Utils
open Shared
open Shared.Types
open Shared.Models
open Global


module private Elmish =


    type State =
        {
            Page : Global.Pages
            Patient: Patient option
            NormalValues : Deferred<NormalValues>
            BolusMedication: Deferred<BolusMedication list>
            ContinuousMedication: Deferred<ContinuousMedication list>
            Products: Deferred<Product list>
            OrderContext: Deferred<OrderContext>
            TreatmentPlan: Deferred<OrderPlan>
            NutritionPlan: Deferred<OrderPlan>
            Formulary: Deferred<Formulary>
            Parenteralia: Deferred<Parenteralia>
            Localization: Deferred<string [][]>
            Hospitals: Deferred<string []>
            Context: Context
            ShowDisclaimer: bool
            IsDemo: bool
            SnackbarMsg : string
            SnackbarOpen : bool
        }



    type Msg =
        | UrlChanged of string list
        | AcceptDisclaimer

        | UpdatePage of Global.Pages
        | UpdatePatient of Patient option

        | LoadNormalValues of AsyncOperationStatus<Result<NormalValues, string>>

        | LoadBolusMedication of AsyncOperationStatus<Result<BolusMedication list, string>>
        | LoadContinuousMedication of AsyncOperationStatus<Result<ContinuousMedication list, string>>
        | LoadProducts of AsyncOperationStatus<Result<Product list, string>>
        | OnSelectContinuousMedicationItem of string

        | OrderContextCommand of Api.OrderContextCommand * OrderContext
        | LoadResourcesReloaded of ApiResponse
        | LoadUpdatedOrderContext of ApiResponse
        | LoadSelectedOrderScenario of ApiResponse
        | LoadUpdatedOrderScenario of ApiResponse
        | LoadRefreshedOrderScenario of ApiResponse
        | LoadOrderContextPropertyChange of Api.OrderContextCommand * ApiResponse

        | TreatmentPlanCommand of Api.OrderPlanCommand
        | LoadUpdatedTreatmentPlan of ApiResponse
        | LoadFilteredTreatmentPlan of ApiResponse

        | UpdateFormulary of Formulary
        | LoadFormulary of ApiResponse

        | UpdateParenteralia of Parenteralia
        | LoadParenteralia of ApiResponse

        | UpdateLanguage of Localization.Locales
        | LoadLocalization of AsyncOperationStatus<Result<string [] [], string>>

        | UpdateHospital of string
        | CloseSnackbar


    and ApiResponse = AsyncOperationStatus<Result<Api.Response, string []>>


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    let createApiMsg msg cmd =
        async {
            let! result = serverApi.processCommand cmd
            return Finished result |> msg
        }
        |> Cmd.fromAsync


    let processApiMsg state msg =
        match msg with
        | Api.OrderContextResp (Api.OrderContextResult ctx) ->
            { state with
                OrderContext = Resolved ctx
            }, Cmd.none
        | Api.OrderPlanResp (Api.OrderPlanFiltered tp)
        | Api.OrderPlanResp (Api.OrderPlanUpdated tp) ->
            {  state with
                TreatmentPlan = Resolved tp
            }, Cmd.none
        | Api.FormularyResp form ->
            { state with
                Formulary = Resolved form
            }, Cmd.none
        | Api.ParentaraliaResp par ->
            { state with
                Parenteralia = Resolved par
            }, Cmd.none


    let loadOrderContext resp = Api.OrderContextCmd >> createApiMsg resp


    let loadTreatmentPlan resp = Api.OrderPlanCmd >> createApiMsg resp


    let loadFormuarly = Api.FormularyCmd >> createApiMsg LoadFormulary


    let loadParenteralia = Api.ParenteraliaCmd >> createApiMsg LoadParenteralia


    // url needs to be in format: http://localhost:8080/#patient?by=2&bm=0&bd=1
    // * pg : el (emergency list) cm (continuous medication) pr (prescribe)
    // * ad: age in days
    // * by: birth year
    // * bm: birth month
    // * bd: birth day
    // * wt: weight (gram)
    // * ht: height (cm)
    // * gw: gestational age weeks
    // * gd: gestational age days
    // * la: language (en; du; fr; ge; sp; it; ch)
    // * dc: show disclaimer (n;_)
    // * cv: central venous line (y;_)
    // * dp: department
    // * md: medication
    // * rt: route
    // * fr: form
    // * in: indication
    // * dt: dosetype
    let parseUrl sl =
        match sl with
        | [] -> None, None, None, true, None
        | [ "patient"; Route.Query queryParams ] ->
            let paramsMap = Map.ofList queryParams

            let pat =
                match Map.tryFind "by" paramsMap, Map.tryFind "ad" paramsMap with
                | Some (Route.Int year), _ ->
                    // birthday year is required
                    let month =
                        match Map.tryFind "bm" paramsMap with
                        | Some (Route.Int months) -> months
                        | _ -> 1 // january is the default

                    let day =
                        match Map.tryFind "bd" paramsMap with
                        | Some (Route.Int days) -> days
                        | _ -> 1 // first day of the month is the default

                    let weight =
                        match Map.tryFind "wt" paramsMap with
                        | Some (Route.Int weight) -> Some weight
                        | _ -> None

                    let height =
                        match Map.tryFind "ht" paramsMap with
                        | Some (Route.Int height) -> Some height
                        | _ -> None

                    let gaWeeks =
                        match Map.tryFind "gw" paramsMap with
                        | Some (Route.Int weeks) -> weeks |> Some
                        | _ -> None

                    let gaDays =
                        match Map.tryFind "gd" paramsMap with
                        | Some (Route.Int days) -> days |> Some
                        | _ -> None

                    let cvl =
                        match Map.tryFind "cv" paramsMap with
                        | Some s when s = "y" -> true
                        | _ -> false

                    let dep =  Map.tryFind "dp"paramsMap

                    let age = Patient.Age.fromBirthDate DateTime.Now (DateTime(year, month, day))

                    let patient =
                        Patient.create
                            (Some age.Years)
                            (Some age.Months)
                            (Some age.Weeks)
                            (Some age.Days)
                            weight
                            height
                            gaWeeks
                            gaDays
                            UnknownGender
                            [ if cvl then CVL ]
                            None
                            dep

                    Logging.log "parsed: " patient
                    patient
                | _, Some (Route.Int days) ->
                    let weight =
                        match Map.tryFind "wt" paramsMap with
                        | Some (Route.Int weight) -> Some weight
                        | _ -> None

                    let height =
                        match Map.tryFind "ht" paramsMap with
                        | Some (Route.Int height) -> Some height
                        | _ -> None

                    let gaWeeks =
                        match Map.tryFind "gw" paramsMap with
                        | Some (Route.Int weeks) -> weeks |> Some
                        | _ -> None

                    let gaDays =
                        match Map.tryFind "gd" paramsMap with
                        | Some (Route.Int days) -> days |> Some
                        | _ -> None

                    let cvl =
                        match Map.tryFind "cv" paramsMap with
                        | Some s when s = "y" -> [ CVL ]
                        | _ -> []

                    let dep =  Map.tryFind "dp"paramsMap

                    let age = Patient.Age.fromDays days

                    let patient =
                        Patient.create
                            (Some age.Years)
                            (Some age.Months)
                            (Some age.Weeks)
                            (Some age.Days)
                            weight
                            height
                            gaWeeks
                            gaDays
                            UnknownGender
                            cvl
                            None
                            dep

                    patient

                | _ ->
                    Logging.warning "could not parse url to patient" (sl |> String.concat ";")
                    None

            let page =
                match paramsMap |> Map.tryFind "pg" with
                | Some s when s = "el" -> Some LifeSupport
                | Some s when s = "cm" -> Some ContinuousMeds
                | Some s when s = "pr" -> Some Prescribe
                | Some s when s = "fm" -> Some Formulary
                | Some s when s = "pe" -> Some Parenteralia
                | _ -> None

            let lang =
                match paramsMap |> Map.tryFind "la" with
                | Some s when s = "en" -> Some Localization.English
                | Some s when s = "du" -> Some Localization.Dutch
                | Some s when s = "fr" -> Some Localization.French
                | Some s when s = "gr" -> Some Localization.German
                | Some s when s = "sp" -> Some Localization.Spanish
                | Some s when s = "it" -> Some Localization.Italian
//                | Some s when s = "ch" -> Some Localization.Chinees // refact: to Chinese
                | _ -> None

            let discl =
                match paramsMap |> Map.tryFind "dc" with
                | Some s when s = "n" -> false
                | _ -> true

            let med =
                {|
                    indication = paramsMap |> Map.tryFind "in"
                    medication = paramsMap |> Map.tryFind "md"
                    route = paramsMap |> Map.tryFind "rt"
                    form = paramsMap |> Map.tryFind "fr"
                    dosetype =
                        paramsMap
                        |> Map.tryFind "dt"
                        |> Option.map DoseType.doseTypeFromString
                |}
                |> Some

            pat, page, lang, discl, med

        | _ ->
            sl
            |> String.concat ""
            |> Logging.warning "could not parse url"

            None, None, None, true, None


    let initialState pat page lang discl (med : {| indication: string option; medication: string option; route: string option; form: string option; dosetype: DoseType option |} option) =
        {
            ShowDisclaimer = discl
            Page = page |> Option.defaultValue LifeSupport
            Patient = pat
            NormalValues = HasNotStartedYet
            BolusMedication = HasNotStartedYet
            ContinuousMedication = HasNotStartedYet
            Products = HasNotStartedYet
            OrderContext =
                match med with
                | None -> HasNotStartedYet
                | Some m ->
                    OrderContext.empty |> OrderContext.setMedication m.indication m.medication m.route m.form m.dosetype
                    |> Resolved
            TreatmentPlan =
                match pat with
                | None -> HasNotStartedYet
                | Some p -> OrderPlan.create p [||] |> Resolved
            NutritionPlan =
                match pat with
                | None -> HasNotStartedYet
                | Some p -> OrderPlan.create p [||] |> Resolved
            Formulary = HasNotStartedYet
            Parenteralia = HasNotStartedYet
            Localization = HasNotStartedYet
            Hospitals = HasNotStartedYet
            Context =
                {
                    Localization = lang |> Option.defaultValue Localization.Dutch
                    Hospital = "UMCU"
                }
            IsDemo = false
            SnackbarMsg = ""
            SnackbarOpen = false
        }


    let init () : State * Cmd<Msg> =
        let pat, page, lang, discl, med = Router.currentUrl () |> parseUrl

        let cmds =
            Cmd.batch [
                //Cmd.ofMsg (pat |> UpdatePatient)
                Cmd.ofMsg (LoadNormalValues Started)
                Cmd.ofMsg (LoadBolusMedication Started)
                Cmd.ofMsg (LoadContinuousMedication Started)
                Cmd.ofMsg (LoadProducts Started)
                Cmd.ofMsg (LoadLocalization Started)
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]

        initialState pat page lang discl med
        , cmds


    let applyNormalValues (normalValues: Deferred<NormalValues>) (pat : Patient option) =
        match normalValues, pat with
        | Resolved nv, Some p ->
            p
            |> Patient.applyNormalValues
                (Some nv.Weights)
                (Some nv.Heights)
                (Some nv.NeoWeights)
                (Some nv.NeoHeights)
            |> Some
        | _ -> pat



    module CommandHandlers =


        let updateOrderContext state (ctx : OrderContext) =
                let ctx =
                    { ctx with
                        Patient =
                            state.Patient
                            |> Option.defaultValue ctx.Patient
                    }

                { state with
                    OrderContext = Resolved ctx
                    Formulary =
                        state.Formulary
                        |> Deferred.map (fun form ->
                            { form with
                                Indication = ctx.Filter.Indication
                                Generic = ctx.Filter.Generic
                                Route = ctx.Filter.Route
                                Form = ctx.Filter.Form
                                DoseType = ctx.Filter.DoseType
                            }
                        )
                    Parenteralia =
                        state.Parenteralia
                        |> Deferred.map (fun par ->
                            { par with
                                Generic = ctx.Filter.Generic
                                Form = ctx.Filter.Form
                                Route = ctx.Filter.Route
                            }
                        )
                },
                Cmd.batch [
                    Cmd.ofMsg (LoadUpdatedOrderContext Started)
                    Cmd.ofMsg (LoadFormulary Started)
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        let selectOrderScenario state (ctx : OrderContext) =
                { state with
                    OrderContext = Resolved ctx
                },
                Cmd.batch [
                    Cmd.ofMsg (LoadSelectedOrderScenario Started)
                ]


        let updateOrderScenario state (ctx : OrderContext) =
                { state with
                    OrderContext = Resolved ctx
                },
                Cmd.batch [
                    Cmd.ofMsg (LoadUpdatedOrderScenario Started)
                ]


        let refreshOrderScenario state (ctx : OrderContext) =
                { state with
                    OrderContext = Resolved ctx
                },
                Cmd.batch [
                    Cmd.ofMsg (LoadRefreshedOrderScenario Started)
                ]






    let update (msg: Msg) (state: State) =
        let processOk = processApiMsg state
        let processError err (state, cmd) =
            Logging.error "error" err
            { state with
                SnackbarMsg = "Er ging iets mis, herladen"
                SnackbarOpen = true
            }, cmd

        match msg with
        | CloseSnackbar ->
            { state with
                SnackbarMsg = ""
                SnackbarOpen = false
            }, Cmd.none

        | AcceptDisclaimer ->
            { state with
                ShowDisclaimer = false
            },
            Cmd.none

        | UpdateLanguage lang ->
            { state with
                ShowDisclaimer = true
                State.Context.Localization = lang
            }, Cmd.none

        | UpdateHospital hosp ->
            { state with
                ShowDisclaimer = true
                State.Context.Hospital = hosp
            }, Cmd.none

        | UpdatePage page ->
            // make sure that the order context is not in use
            // i.e. the order context should be "fresh"
            if page = ContinuousMeds &&
               state.OrderContext |> Deferred.map(fun ctx -> ctx.Filter.Generic |> Option.isSome) |> Deferred.defaultValue true
                then
                { state with
                    Page = page
                    OrderContext = HasNotStartedYet
                }, Cmd.ofMsg (LoadUpdatedOrderContext Started)
            else
                if page = Settings then
                    { state with
                        OrderContext = OrderContext.empty |> Resolved
                    }
                    , Cmd.ofMsg (LoadResourcesReloaded Started)
                else
                    { state with
                        Page = page
                    }, Cmd.none

        | UpdatePatient pat ->
            let pat = pat |> applyNormalValues state.NormalValues

            { state with
                Patient = pat
                OrderContext =
                    match pat with
                    | None -> HasNotStartedYet
                    | Some p ->
                        match state.OrderContext with
                        | Resolved ctx -> ctx
                        | _ -> OrderContext.empty
                        |> OrderContext.setPatient p
                        |> Resolved
                TreatmentPlan =
                    match pat with
                    | None -> HasNotStartedYet
                    | Some p ->
                        let tp = OrderPlan.create p [||]
                        state.TreatmentPlan
                        |> Deferred.map (fun tp ->
                            { tp with Patient = p }
                        )
                        |> Deferred.defaultValue tp
                        |> Resolved
                Formulary =
                    { Formulary.empty with Patient = pat }
                    |> Resolved
                Parenteralia =
                    Parenteralia.empty
                    |> Resolved
            },
            Cmd.batch [
                Cmd.ofMsg (LoadUpdatedOrderContext Started)
                Cmd.ofMsg (LoadUpdatedTreatmentPlan Started)
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]

        | UrlChanged sl ->
            let pat, page, lang, discl, med = sl |> parseUrl

            { state with
                ShowDisclaimer = discl
                Page = page |> Option.defaultValue LifeSupport
                Patient =  pat
                OrderContext =
                    match med with
                    | None -> state.OrderContext
                    | Some m ->
                        match state.OrderContext with
                        | InProgress -> state.OrderContext
                        | HasNotStartedYet ->
                            OrderContext.empty
                            |> OrderContext.setMedication m.indication m.medication m.route m.form m.dosetype
                            |> Resolved
                        | Resolved ctx ->
                            ctx
                            |> OrderContext.setMedication m.indication m.medication m.route m.form m.dosetype
                            |> Resolved
                Context =
                    { state.Context with
                        Localization =
                            lang |> Option.defaultValue Localization.English
                    }
            },
            Cmd.ofMsg (pat |> UpdatePatient)

        | LoadLocalization Started ->
            { state with
                Localization = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadLocalization LoadLocalization)

        | LoadLocalization (Finished (Ok terms)) ->

            { state with
                Localization = terms |> Resolved
            },
            Cmd.none

        | LoadLocalization (Finished (Error s)) ->
            Logging.error "cannot load localization" s
            state, Cmd.none

        | LoadNormalValues Started ->
            { state with
                NormalValues = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadNormalValues LoadNormalValues)

        | LoadNormalValues (Finished (Ok normalValues)) ->
            { state with
                NormalValues = normalValues |> Resolved
            },
            Cmd.ofMsg (UpdatePatient state.Patient)

        | LoadNormalValues (Finished (Error s)) ->
            Logging.error "cannot load normal values" s
            state, Cmd.none


        | LoadBolusMedication Started ->
            { state with
                BolusMedication = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadBolusMedication LoadBolusMedication)

        | LoadBolusMedication (Finished (Ok meds)) ->
            { state with
                BolusMedication = meds |> Resolved
                Hospitals =
                    meds
                    |> List.map _.Hospital
                    |> List.distinct
                    |> List.filter (String.isNullOrWhiteSpace >> not)
                    |> List.toArray
                    |> Resolved
            },
            Cmd.none

        | LoadBolusMedication (Finished (Error s)) ->
            Logging.error "cannot load emergency treatment" s
            state, Cmd.none

        | LoadContinuousMedication Started ->
            { state with
                ContinuousMedication = InProgress
            },
            Cmd.fromAsync (
                GoogleDocs.loadContinuousMedication LoadContinuousMedication
            )

        | LoadContinuousMedication (Finished (Ok meds)) ->

            { state with
                ContinuousMedication = meds |> Resolved
            },
            Cmd.none

        | LoadContinuousMedication (Finished (Error s)) ->
            Logging.error "cannot load continuous medication" s
            state, Cmd.none

        | OnSelectContinuousMedicationItem item ->
            Logging.log $"selected continuous medication item" item
            match state.ContinuousMedication with
            | Resolved meds ->
                match meds |> List.tryFind (fun m -> item.Contains m.Medication) with
                | None ->
                    Logging.warning $"could not find continuous medication with item: {item}" item
                    state, Cmd.none
                | Some selected ->
                    let ctx =
                        match state.OrderContext with
                        | HasNotStartedYet -> OrderContext.empty
                        | InProgress -> OrderContext.empty
                        | Resolved ctx -> ctx
                        |> fun ctx ->
                            { ctx with
                                OrderContext.Filter.Indication =
                                    if selected.Indication = "" then None else Some selected.Indication
                                OrderContext.Filter.Generic = Some selected.Generic
                                OrderContext.Filter.Route = Some "INTRAVENEUS"
                                OrderContext.Filter.DoseType =
                                    if selected.DoseType = "" then None
                                    else Some (DoseType.doseTypeFromString selected.DoseType)
                            }
                    { state with
                        Page = Prescribe
                        OrderContext = ctx |> Resolved
                    }, Cmd.ofMsg (OrderContextCommand (Api.UpdateOrderContext, ctx))
            | _ -> state, Cmd.none

        | LoadProducts Started ->
            { state with Products = InProgress },
            Cmd.fromAsync (GoogleDocs.loadProducts LoadProducts)

        | LoadProducts (Finished (Ok prods)) ->

            { state with
                Products = prods |> Resolved
            },
            Cmd.none

        | LoadProducts (Finished (Error s)) ->
            Logging.error "cannot load products" s
            state, Cmd.none

        | OrderContextCommand (ctxCmd, ctx) ->
            match ctxCmd with
            | Api.UpdateOrderContext -> ctx |> CommandHandlers.updateOrderContext state
            | Api.SelectOrderScenario -> ctx |> CommandHandlers.selectOrderScenario state
            | Api.UpdateOrderScenario -> ctx |> CommandHandlers.updateOrderScenario state
            | Api.ResetOrderScenario -> ctx |> CommandHandlers.refreshOrderScenario state
            | Api.ReloadResources -> ctx |> CommandHandlers.updateOrderContext state
            | propertyCmd ->
                { state with OrderContext = Resolved ctx },
                Cmd.ofMsg (LoadOrderContextPropertyChange (propertyCmd, Started))

        | LoadResourcesReloaded Started ->
            match state.Patient with
            | None ->
                { state with OrderContext = HasNotStartedYet },
                (Api.ReloadResources, OrderContext.empty)
                |> loadOrderContext LoadResourcesReloaded
            | Some pat ->
                match state.OrderContext with
                | HasNotStartedYet ->
                        { state with OrderContext = InProgress },
                        (Api.ReloadResources, OrderContext.empty |> OrderContext.setPatient pat)
                        |> loadOrderContext LoadResourcesReloaded
                | InProgress -> state, Cmd.none
                | Resolved ctx ->
                    let ctx = { ctx with Patient = pat }

                    { state with OrderContext = InProgress },
                    (Api.ReloadResources, ctx)
                    |> loadOrderContext LoadResourcesReloaded

        | LoadResourcesReloaded (Finished (Ok msg)) -> msg |> processOk
        | LoadResourcesReloaded (Finished (Error err)) ->
            (state, Cmd.none)
            |> processError err

        | LoadUpdatedOrderContext Started ->
            match state.Patient with
            | None -> { state with OrderContext = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.OrderContext with
                | HasNotStartedYet ->
                        { state with OrderContext = InProgress },
                        (Api.UpdateOrderContext, OrderContext.empty |> OrderContext.setPatient pat)
                        |> loadOrderContext LoadUpdatedOrderContext
                | InProgress -> state, Cmd.none
                | Resolved ctx ->
                    let ctx = { ctx with Patient = pat }

                    { state with OrderContext = InProgress },
                    (Api.UpdateOrderContext, ctx)
                    |> loadOrderContext LoadUpdatedOrderContext

        | LoadUpdatedOrderContext (Finished (Ok msg)) -> msg |> processOk
        | LoadUpdatedOrderContext (Finished (Error err)) ->
            Logging.error "error in LoadUpdatedOrderContext" err
            ({ state with OrderContext = HasNotStartedYet },
            LoadUpdatedOrderContext Started |> Cmd.ofMsg)
            |> processError err

        | LoadSelectedOrderScenario Started ->
            match state.OrderContext with
            | HasNotStartedYet ->
                Logging.error "select order scenario doesn't match state" state
                state, Cmd.none
            | InProgress -> state, Cmd.none
            | Resolved ctx ->
                { state with OrderContext = InProgress },
                (Api.SelectOrderScenario, ctx)
                |> loadOrderContext LoadSelectedOrderScenario

        | LoadSelectedOrderScenario (Finished (Ok msg)) -> msg |> processOk
        | LoadSelectedOrderScenario (Finished (Error err)) ->
            ({ state with OrderContext = HasNotStartedYet },
            LoadUpdatedOrderContext Started |> Cmd.ofMsg)
            |> processError err

        | LoadUpdatedOrderScenario Started ->
            match state.OrderContext with
            | HasNotStartedYet ->
                Logging.error "select order scenario doesn't match state" state
                state, Cmd.none
            | InProgress -> state, Cmd.none
            | Resolved ctx ->
                { state with OrderContext = InProgress },
                (Api.UpdateOrderScenario, ctx)
                |> loadOrderContext LoadUpdatedOrderScenario

        | LoadUpdatedOrderScenario (Finished (Ok msg)) -> msg |> processOk
        | LoadUpdatedOrderScenario (Finished (Error err)) ->
            ({ state with OrderContext = HasNotStartedYet },
            LoadUpdatedOrderContext Started |> Cmd.ofMsg)
            |> processError err

        | LoadRefreshedOrderScenario Started ->
            match state.OrderContext with
            | HasNotStartedYet ->
                state, Cmd.none
            | InProgress -> state, Cmd.none
            | Resolved ctx ->
                { state with OrderContext = InProgress },
                (Api.ResetOrderScenario, ctx)
                |> loadOrderContext LoadRefreshedOrderScenario

        | LoadRefreshedOrderScenario (Finished (Ok msg)) -> msg |> processOk
        | LoadRefreshedOrderScenario (Finished (Error err)) ->
            ({ state with OrderContext = HasNotStartedYet },
            LoadUpdatedOrderContext Started |> Cmd.ofMsg)
            |> processError err

        | LoadOrderContextPropertyChange (cmd, Started) ->
            match state.OrderContext with
            | HasNotStartedYet | InProgress -> state, Cmd.none
            | Resolved ctx ->
                { state with OrderContext = InProgress },
                (cmd, ctx)
                |> loadOrderContext (fun resp -> LoadOrderContextPropertyChange (cmd, resp))

        | LoadOrderContextPropertyChange (_, Finished (Ok msg)) -> msg |> processOk

        | LoadOrderContextPropertyChange (_, Finished (Error err)) ->
            ({ state with OrderContext = HasNotStartedYet },
            LoadUpdatedOrderContext Started |> Cmd.ofMsg)
            |> processError err


        | TreatmentPlanCommand tpCmd ->
            match tpCmd with
            | Api.UpdateOrderPlan (tp, Some (ctxCmd, ctx)) ->
                { state with TreatmentPlan = InProgress },
                Api.OrderPlanCmd (Api.UpdateOrderPlan (tp, Some (ctxCmd, ctx)))
                |> createApiMsg LoadUpdatedTreatmentPlan
            | Api.UpdateOrderPlan (tp, None) ->
                let onlySetOrderContext =
                    state.TreatmentPlan
                    |> Deferred.map (fun st -> st.Selected.IsNone && tp.Selected.IsSome)
                    |> Deferred.defaultValue false

                let cmd =
                    if state.Page = TreatmentPlan then
                        if onlySetOrderContext then Cmd.none else Cmd.ofMsg (LoadUpdatedTreatmentPlan Started)
                    else
                        Cmd.batch [
                            Cmd.ofMsg (OrderContextCommand (Api.UpdateOrderContext, OrderContext.empty))
                            Cmd.ofMsg (LoadUpdatedTreatmentPlan Started)
                        ]

                { state with
                    Page = TreatmentPlan
                    TreatmentPlan = Resolved tp
                }, cmd
            | Api.FilterOrderPlan tp ->
                { state with
                    TreatmentPlan = Resolved tp
                }, Cmd.ofMsg (LoadFilteredTreatmentPlan Started)

        | LoadUpdatedTreatmentPlan Started ->
            match state.Patient with
            | None -> { state with TreatmentPlan = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.TreatmentPlan with
                | HasNotStartedYet ->
                    { state with TreatmentPlan = InProgress },
                    Api.UpdateOrderPlan (OrderPlan.create pat [||], None)
                    |> loadTreatmentPlan LoadUpdatedTreatmentPlan
                | InProgress -> state, Cmd.none
                | Resolved tp ->
                    { state with TreatmentPlan = InProgress },
                    Api.UpdateOrderPlan (tp, None)
                    |> loadTreatmentPlan LoadUpdatedTreatmentPlan

        | LoadUpdatedTreatmentPlan (Finished (Ok msg)) -> msg |> processOk
        | LoadUpdatedTreatmentPlan (Finished (Error err)) ->
            ({ state with TreatmentPlan = HasNotStartedYet },
            LoadUpdatedTreatmentPlan Started |> Cmd.ofMsg)
            |> processError err

        | LoadFilteredTreatmentPlan Started ->
            match state.Patient with
            | None -> { state with TreatmentPlan = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.TreatmentPlan with
                | HasNotStartedYet ->
                    { state with TreatmentPlan = InProgress },
                    Api.FilterOrderPlan (OrderPlan.create pat [||])
                    |> loadTreatmentPlan LoadFilteredTreatmentPlan
                | InProgress -> state, Cmd.none
                | Resolved tp ->
                    { state with TreatmentPlan = InProgress },
                    Api.FilterOrderPlan tp
                    |> loadTreatmentPlan LoadFilteredTreatmentPlan

        | LoadFilteredTreatmentPlan (Finished (Ok msg)) -> msg |> processOk
        | LoadFilteredTreatmentPlan (Finished (Error err)) ->
            ({ state with TreatmentPlan = HasNotStartedYet },
            LoadUpdatedTreatmentPlan Started |> Cmd.ofMsg)
            |> processError err

        | LoadFormulary Started ->
            let form =
                match state.Formulary with
                | Resolved form ->
                    { form with
                        Patient =
                            state.Patient
                    }
                | _ -> Formulary.empty

            let cmd = form |> loadFormuarly

            { state with Formulary = InProgress }, cmd

        | LoadFormulary (Finished (Ok msg)) -> processOk msg

        | LoadFormulary (Finished(Error err)) ->
            Logging.error "LoadFormulary error:" err
            state,
            Cmd.none

        | UpdateFormulary form ->
            let state =
                { state with
                    Formulary = Resolved form
                    OrderContext =
                        state.OrderContext
                        |> Deferred.map (fun ctx ->
                            { ctx with
                                Filter =
                                    { ctx.Filter with
                                        Indication = form.Indication
                                        Generic = form.Generic
                                        Form = form.Form
                                        Route = form.Route
                                        DoseType = form.DoseType
                                        Diluent =
                                            if form.Indication = ctx.Filter.Indication &&
                                               form.Generic = ctx.Filter.Generic &&
                                               form.Route = ctx.Filter.Route &&
                                               form.Form = ctx.Filter.Form then ctx.Filter.Diluent
                                            else None
                                        SelectedComponents =
                                            if form.Indication = ctx.Filter.Indication &&
                                               form.Generic = ctx.Filter.Generic &&
                                               form.Route = ctx.Filter.Route &&
                                               form.Form = ctx.Filter.Form then ctx.Filter.SelectedComponents
                                            else [||]

                                    }
                                Scenarios = [||]
                            }
                        )
                    Parenteralia =
                        state.Parenteralia
                        |> Deferred.map (fun par ->
                            { par with
                                Generic = form.Generic
                                Route = form.Route
                                Form = form.Form
                            }
                        )
                }
            state,
            Cmd.batch [
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadUpdatedOrderContext Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]

        | LoadParenteralia Started ->
            let cmd =
                let par =
                    state.Parenteralia
                    |> Deferred.defaultValue Parenteralia.empty

                loadParenteralia par
            { state with Parenteralia = InProgress }, cmd

        | LoadParenteralia (Finished(Ok msg)) -> msg |> processOk

        | LoadParenteralia (Finished (Error err)) ->
            Logging.error "LoadParenteralia finished with error:" err
            state, Cmd.none

        | UpdateParenteralia par ->
            let state =
                { state with
                    Parenteralia = Resolved par
                    Formulary =
                        state.Formulary
                        |> Deferred.map (fun form ->
                            { form with
                                Indication = None
                                Generic = par.Generic
                                Route = par.Route
                                Form = par.Form
                                DoseType = None
                            }
                        )
                    OrderContext =
                        state.OrderContext
                        |> Deferred.map (fun ctx ->
                            { ctx with
                                Filter =
                                    { ctx.Filter with
                                        Indication = None
                                        Generic = par.Generic
                                        Form = par.Form
                                        Route = par.Route
                                        DoseType = None
                                        Diluent =
                                            if par.Generic = ctx.Filter.Generic &&
                                               par.Route = ctx.Filter.Route &&
                                               par.Form = ctx.Filter.Form then ctx.Filter.Diluent
                                            else None
                                        SelectedComponents =
                                            if par.Generic = ctx.Filter.Generic &&
                                               par.Route = ctx.Filter.Route &&
                                               par.Form = ctx.Filter.Form then ctx.Filter.SelectedComponents
                                            else [||]

                                    }
                                Scenarios = [||]
                            }
                        )

                }
            state,
            Cmd.batch [
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadUpdatedOrderContext Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]


    let calculatInterventions calc meds pat =
        meds
        |> Deferred.bind (fun xs ->
            match pat with
            | None -> InProgress
            | Some p ->
                let a = p |> Patient.getAgeInYears
                let w = p |> Patient.getWeightInKg
                xs |> calc a w |> Resolved
        )


open Elmish


[<Literal>]
let private themeDef = """
responsiveFontSizes(createTheme({ typography: { fontSize : 14, } }), { factor : 2 })
"""


[<Import("createTheme", from="@mui/material/styles")>]
[<Emit(themeDef)>]
let private theme : obj = jsNative


[<Literal>]
let private mobileDef = """
responsiveFontSizes(createTheme({ typography: { fontSize : 12, } }), { factor : 2 })
"""


[<Import("createTheme", from="@mui/material/styles")>]
[<Emit(mobileDef)>]
let private mobile : obj = jsNative


// Entry point must be in a separate file
// for Vite Hot Reload to work
[<JSX.Component>]
let View () =
    let state, dispatch = React.useElmish (init, update, [||])
    let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

    let handleClose = fun _ -> CloseSnackbar |> dispatch

    let bm =
        calculatInterventions
            EmergencyTreatment.calculate
            state.BolusMedication
            state.Patient

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculatInterventions calc state.ContinuousMedication state.Patient

    let sx =
        if isMobile
        then
            {| height= "100vh"; overflowY = "hidden"; mb=5 |}
        else
            {| height= "100vh"; overflowY = "hidden"; mb=0 |}

    let theme = if isMobile then mobile else theme

    JSX.jsx
        $"""
    import {{ ThemeProvider }} from '@mui/material/styles';
    import {{ responsiveFontSizes }} from '@mui/material/styles';
    import CssBaseline from '@mui/material/CssBaseline';
    import React from "react";
    import Box from '@mui/material/Box';
    import Snackbar from '@mui/material/Snackbar';
    import IconButton from '@mui/material/IconButton';
    import CloseIcon from '@mui/icons-material/Close';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={ sx }>
                <CssBaseline />
                {
                    Components.Router.View {| onUrlChanged = UrlChanged >> dispatch |}
                }
                {
                    Pages.GenPres.View({|
                        showDisclaimer = state.ShowDisclaimer
                        isDemo = state.IsDemo
                        acceptDisclaimer = fun _ -> AcceptDisclaimer |> dispatch
                        patient = state.Patient
                        updatePage = UpdatePage >> dispatch
                        updatePatient = UpdatePatient >> dispatch
                        bolusMedication = bm
                        continuousMedication = cm
                        onSelectContinuousMedicationItem = OnSelectContinuousMedicationItem >> dispatch
                        products = state.Products
                        orderContext = state.OrderContext
                        updateOrderContext = fun (cmd, ctx) -> OrderContextCommand (cmd, ctx) |> dispatch
                        treatmentPlan = state.TreatmentPlan
                        treatmentPlanCommand = TreatmentPlanCommand >> dispatch
                        formulary = state.Formulary
                        updateFormulary = UpdateFormulary >> dispatch
                        parenteralia = state.Parenteralia
                        updateParenteralia = UpdateParenteralia >> dispatch
                        page = state.Page
                        localizationTerms = state.Localization
                        languages = Localization.languages
                        hospitals = state.Hospitals
                        switchLang = UpdateLanguage >> dispatch
                        switchHosp = UpdateHospital >> dispatch
                    |})
                    |> toReact |> Components.Context.Context state.Context
                }
            </Box>
            <div>
                <Snackbar
                    open={ state.SnackbarOpen }
                    autoHideDuration={3000}
                    message={ state.SnackbarMsg }
                    onClose={handleClose}
                />
            </div>
        </ThemeProvider>
    </React.StrictMode>
    """


let root = ReactDomClient.createRoot (document.getElementById "genpres-app")
root.render (View() |> toReact)

namespace Views

module Nutrition =


    open Fable.Core
    open Fable.React
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open Shared.Models.Order
    open Elmish
    open Utils
    open FSharp.Core


    module private Elmish =


        type State =
            {
                Order : Order option
                SelectedComponent : string option
            }

        type Msg =
            | ChangeComponent of string option
            | ChangeComponentOrderableQuantity of cmp: string * string option
            | ChangeComponentDoseQuantityAdjust of cmp: string * string option
            | ChangeOrderableDoseRate of string option
            | ChangeOrderableQuantity of string option
            | ChangeFrequency of string option
            | UpdateOrderScenario of Order
            | ResetOrderScenario
            // Rate navigation
            | DecreaseDoseRateProperty of ntimes: int * useCalc: bool
            | IncreaseDoseRateProperty of ntimes: int * useCalc: bool
            | SetMinDoseRateProperty
            | SetMaxDoseRateProperty
            | SetMedianDoseRateProperty
            // Dose Quantity navigation
            | ChangeOrderableDoseQuantity of string option
            | DecreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | IncreaseDoseQuantityProperty of ntimes: int * useCalc: bool
            | SetMinDoseQuantityProperty
            | SetMaxDoseQuantityProperty
            | SetMedianDoseQuantityProperty
            // Component Quantity navigation (carries component name)
            | DecreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | IncreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | SetMinComponentQuantityProperty of cmp: string
            | SetMaxComponentQuantityProperty of cmp: string
            | SetMedianComponentQuantityProperty of cmp: string


        let init (ctx : Deferred<OrderContext>) =
            let ord, cmp =
                match ctx with
                | Resolved ctx | Recalculating ctx ->
                    match ctx.Scenarios with
                    | [| sc |] ->
                        let ord = sc.Order
                        match ord.Orderable.Components with
                        | [||] -> Some ord, None
                        | cmps -> Some ord, Some cmps[0].Name
                    | _ ->
                        if ctx.Scenarios |> Array.length > 1 then
                            Logging.error "received multiple scenarios" ctx.Scenarios.Length
                        None, None
                | _ -> None, None

            {
                SelectedComponent = cmp
                Order = ord
            }
            , Cmd.none


        let update
            updateOrderScenario
            resetOrderScenario
            (navigate :
                {|
                    setRateMin : OrderLoader -> unit
                    setRateDec : int * bool -> OrderLoader -> unit
                    setRateMed : OrderLoader -> unit
                    setRateInc : int * bool -> OrderLoader -> unit
                    setRateMax : OrderLoader -> unit

                    setDoseQtyMin : OrderLoader -> unit
                    setDoseQtyDec : int * bool -> OrderLoader -> unit
                    setDoseQtyMed : OrderLoader -> unit
                    setDoseQtyInc : int * bool -> OrderLoader -> unit
                    setDoseQtyMax : OrderLoader -> unit

                    setComponentQtyMin : OrderLoader -> unit
                    setComponentQtyDec : int * bool -> OrderLoader -> unit
                    setComponentQtyMed : OrderLoader -> unit
                    setComponentQtyInc : int * bool -> OrderLoader  -> unit
                    setComponentQtyMax : OrderLoader -> unit
                |})
            (msg: Msg)
            (state : State) : State * Cmd<Msg>
            =
            let setOvar = OrderVariable.setOvar

            let handleNav nav =
                match state.Order with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create state.SelectedComponent None ord
                    |> nav
                    { state with
                        Order = None
                    }
                    , Cmd.none

            let handleNavWithCmp cmpName nav =
                match state.Order with
                | None -> state, Cmd.none
                | Some ord ->
                    OrderLoader.create (Some cmpName) None ord
                    |> nav
                    { state with
                        Order = None
                    }
                    , Cmd.none

            match msg with

            | UpdateOrderScenario ord ->
                OrderLoader.create state.SelectedComponent None ord
                |> updateOrderScenario

                { state with
                    Order = None
                }
                , Cmd.none

            | ResetOrderScenario ->
                match state.Order with
                | Some ord ->
                    OrderLoader.create state.SelectedComponent None ord
                    |> resetOrderScenario
                | None -> ()

                { state with
                    Order = None
                }
                , Cmd.none

            | ChangeComponent cmp ->
                match cmp with
                | None -> state, Cmd.none
                | Some _ ->
                    { state with
                        SelectedComponent = cmp
                    }, Cmd.none

            | ChangeComponentOrderableQuantity (cmpName, s) ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.map (fun cmp ->
                                            if cmp.Name = cmpName then
                                                { cmp with
                                                    OrderableQuantity =
                                                        cmp.OrderableQuantity |> setOvar s
                                                }
                                            else cmp
                                        )
                                }
                        }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeComponentDoseQuantityAdjust (cmpName, s) ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.map (fun cmp ->
                                            if cmp.Name = cmpName then
                                                { cmp with
                                                    Dose =
                                                        { cmp.Dose with
                                                            QuantityAdjust =
                                                                cmp.Dose.QuantityAdjust |> setOvar s
                                                        }
                                                }
                                            else cmp
                                        )
                                }
                        }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseRate s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Dose =
                                        { ord.Orderable.Dose with
                                            Rate =
                                                ord.Orderable.Dose.Rate
                                                |> setOvar s
                                        }
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    OrderableQuantity =
                                        ord.Orderable.OrderableQuantity
                                        |> setOvar s
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeFrequency s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Schedule =
                                { ord.Schedule with
                                    Frequency =
                                        ord.Schedule.Frequency
                                        |> setOvar s
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Dose =
                                        { ord.Orderable.Dose with
                                            Quantity =
                                                ord.Orderable.Dose.Quantity
                                                |> setOvar s
                                        }
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            // Rate navigation
            | SetMinDoseRateProperty -> handleNav navigate.setRateMin
            | DecreaseDoseRateProperty (n, uc) -> handleNav (navigate.setRateDec (n, uc))
            | SetMedianDoseRateProperty -> handleNav navigate.setRateMed
            | IncreaseDoseRateProperty (n, uc) -> handleNav (navigate.setRateInc (n, uc))
            | SetMaxDoseRateProperty -> handleNav navigate.setRateMax

            // Dose Quantity navigation
            | SetMinDoseQuantityProperty -> handleNav navigate.setDoseQtyMin
            | DecreaseDoseQuantityProperty (n, uc) -> handleNav (navigate.setDoseQtyDec (n, uc))
            | SetMedianDoseQuantityProperty -> handleNav navigate.setDoseQtyMed
            | IncreaseDoseQuantityProperty (n, uc) -> handleNav (navigate.setDoseQtyInc (n, uc))
            | SetMaxDoseQuantityProperty -> handleNav navigate.setDoseQtyMax

            // Component Quantity navigation
            | SetMinComponentQuantityProperty cmp -> handleNavWithCmp cmp navigate.setComponentQtyMin
            | DecreaseComponentQuantityProperty (cmp, n, uc) -> handleNavWithCmp cmp (navigate.setComponentQtyDec (n, uc))
            | SetMedianComponentQuantityProperty cmp -> handleNavWithCmp cmp navigate.setComponentQtyMed
            | IncreaseComponentQuantityProperty (cmp, n, uc) -> handleNavWithCmp cmp (navigate.setComponentQtyInc (n, uc))
            | SetMaxComponentQuantityProperty cmp -> handleNavWithCmp cmp navigate.setComponentQtyMax


    open Elmish


    let private halfSize = {| xs = 12; md = 6 |}

    let private cellSx = {| minWidth = 350; ``& .MuiFormControl-root`` = {| width = "100%" |} |}


    [<JSX.Component>]
    let private NutritionSlot (props: {|
        nutritionContext: NutritionContext
        plan: NutritionPlan
        nutritionPlanMsg: Api.NutritionPlanCommand -> unit
        localizationTerms: Deferred<string [][]>
        onRemove: (unit -> unit) option
        wrapInAccordion: bool
        isRecalculating: bool
    |}) =
        let ctx = props.nutritionContext.OrderContext
        let ncId = props.nutritionContext.Id
        let label = props.nutritionContext.Label

        // Use a ref for the plan so that closures captured by useElmish
        // always read the latest plan, even when useElmish doesn't re-initialize
        // (its deps only include ctx, not the plan).
        let planRef = React.useRef props.plan
        planRef.current <- props.plan

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let getWarning = ViewHelpers.getWarning

        let genericChange s =
            ctx
            |> OrderContext.medicationChange s
            |> fun updCtx ->
                // In Nutrition, Generic is upstream of Indication.
                // Clear Indication selection (but NOT the Indications list — server repopulates it).
                { updCtx with
                    Filter =
                        { updCtx.Filter with
                            Indication = None
                        }
                }
            |> fun updCtx -> Api.NavigateNutritionOrderContext(planRef.current, ncId, Api.UpdateOrderContext, updCtx)
            |> props.nutritionPlanMsg

        let indicationChange s =
            ctx |> OrderContext.indicationChange s
            |> fun updCtx -> Api.NavigateNutritionOrderContext(planRef.current, ncId, Api.UpdateOrderContext, updCtx)
            |> props.nutritionPlanMsg

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString
            ctx |> OrderContext.doseTypeChange dt
            |> fun updCtx -> Api.NavigateNutritionOrderContext(planRef.current, ncId, Api.UpdateOrderContext, updCtx)
            |> props.nutritionPlanMsg

        let updateOrderScenario (ol : OrderLoader) =
            { ctx with
                Scenarios =
                    ctx.Scenarios
                    |> Array.map (fun sc ->
                        if sc.Order.Id <> ol.Order.Id then sc
                        else
                            {
                                sc with
                                    Component = ol.Component
                                    Item = ol.Item
                                    Order = ol.Order
                            }
                    )
            }
            |> fun updCtx -> Api.NavigateNutritionOrderContext(planRef.current, ncId, Api.UpdateOrderScenario, updCtx) |> props.nutritionPlanMsg

        let resetOrderScenario (_ol : OrderLoader) =
            Api.NavigateNutritionOrderContext(planRef.current, ncId, Api.ResetOrderScenario, ctx)
            |> props.nutritionPlanMsg

        let navigate =
            let create nav =
                fun (ol : OrderLoader) ->
                    let updCtx =
                        { ctx with
                            Scenarios =
                                ctx.Scenarios
                                |> Array.map (fun sc ->
                                    if sc.Order.Id <> ol.Order.Id then sc
                                    else
                                        {
                                            sc with
                                                Component = ol.Component
                                                Item = ol.Item
                                                Order = ol.Order
                                        }
                                )
                        }
                    nav updCtx

            let createWithN nav =
                fun (n, uc) (ol : OrderLoader) ->
                    let updCtx =
                        { ctx with
                            Scenarios =
                                ctx.Scenarios
                                |> Array.map (fun sc ->
                                    if sc.Order.Id <> ol.Order.Id then sc
                                    else
                                        {
                                            sc with
                                                Component = ol.Component
                                                Item = ol.Item
                                                Order = ol.Order
                                        }
                                )
                        }
                    nav (updCtx, n, uc)

            let createWithCmp nav =
                fun (ol : OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp ->
                        let updCtx =
                            { ctx with
                                Scenarios =
                                    ctx.Scenarios
                                    |> Array.map (fun sc ->
                                        if sc.Order.Id <> ol.Order.Id then sc
                                        else
                                            {
                                                sc with
                                                    Component = ol.Component
                                                    Item = ol.Item
                                                    Order = ol.Order
                                            }
                                    )
                            }
                        nav (updCtx, cmp)

            let createWithCmpN nav =
                fun (n, uc) (ol : OrderLoader) ->
                    match ol.Component with
                    | None -> ()
                    | Some cmp ->
                        let updCtx =
                            { ctx with
                                Scenarios =
                                    ctx.Scenarios
                                    |> Array.map (fun sc ->
                                        if sc.Order.Id <> ol.Order.Id then sc
                                        else
                                            {
                                                sc with
                                                    Component = ol.Component
                                                    Item = ol.Item
                                                    Order = ol.Order
                                            }
                                    )
                            }
                        nav (updCtx, cmp, n, uc)

            let navRate cmd = fun updCtx -> Api.NavigateNutritionOrderContext(planRef.current, ncId, cmd, updCtx) |> props.nutritionPlanMsg
            let navRateN cmd = fun (updCtx, n, uc) -> Api.NavigateNutritionOrderContext(planRef.current, ncId, cmd (n, uc), updCtx) |> props.nutritionPlanMsg
            let navCmpQty cmd = fun (updCtx, cmp) -> Api.NavigateNutritionOrderContext(planRef.current, ncId, cmd cmp, updCtx) |> props.nutritionPlanMsg
            let navCmpQtyN cmd = fun (updCtx, cmp, n, uc) -> Api.NavigateNutritionOrderContext(planRef.current, ncId, cmd (cmp, n, uc), updCtx) |> props.nutritionPlanMsg

            {|
                // Dose Rate
                setRateMin = create (navRate Api.SetMinOrderableDoseRateProperty)
                setRateDec = createWithN (navRateN Api.DecreaseOrderableDoseRateProperty)
                setRateMed = create (navRate Api.SetMedianOrderableDoseRateProperty)
                setRateInc = createWithN (navRateN Api.IncreaseOrderableDoseRateProperty)
                setRateMax = create (navRate Api.SetMaxOrderableDoseRateProperty)
                // Dose Quantity
                setDoseQtyMin = create (navRate Api.SetMinOrderableDoseQuantityProperty)
                setDoseQtyDec = createWithN (navRateN Api.DecreaseOrderableDoseQuantityProperty)
                setDoseQtyMed = create (navRate Api.SetMedianOrderableDoseQuantityProperty)
                setDoseQtyInc = createWithN (navRateN Api.IncreaseOrderableDoseQuantityProperty)
                setDoseQtyMax = create (navRate Api.SetMaxOrderableDoseQuantityProperty)
                // Component Quantity
                setComponentQtyMin = createWithCmp (navCmpQty Api.SetMinComponentOrderableQuantityProperty)
                setComponentQtyDec = createWithCmpN (navCmpQtyN Api.DecreaseComponentOrderableQuantityProperty)
                setComponentQtyMed = createWithCmp (navCmpQty Api.SetMedianComponentOrderableQuantityProperty)
                setComponentQtyInc = createWithCmpN (navCmpQtyN Api.IncreaseComponentOrderableQuantityProperty)
                setComponentQtyMax = createWithCmp (navCmpQty Api.SetMaxComponentOrderableQuantityProperty)
            |}

        let state, dispatch =
            React.useElmish (
                init (Resolved ctx),
                update updateOrderScenario resetOrderScenario navigate,
                [| box ctx |]
            )

        let isOrderLoading = props.isRecalculating
        let isLoading = state.Order.IsNone && not props.isRecalculating
        let select = ViewHelpers.orderSelect true isOrderLoading
        let filterSelect = ViewHelpers.filterSelect isOrderLoading isOrderLoading
        let autoComplete = ViewHelpers.autoComplete isOrderLoading isOrderLoading
        let loadingIndicator = ViewHelpers.inlineProgress isOrderLoading

        // Use local state order when available, otherwise fall back to the
        // order carried by the parent context so that the UI stays populated
        // while the server is processing.
        let displayOrder =
            state.Order
            |> Option.orElseWith (fun () ->
                ctx.Scenarios
                |> Array.tryExactlyOne
                |> Option.map _.Order
            )

        let componentRows =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Components
                |> Array.map (fun cmp ->
                    // Quantity control (bereiding)
                    let qtyVals = cmp.OrderableQuantity |> ViewHelpers.ovarValsWithRange string 3

                    let navigable = cmp.OrderableQuantity |> OrderVariable.isNavigable
                    let solved = ord |> isSolved

                    let nav =
                        let c = qtyVals |> Array.length
                        let show =
                            cmp.OrderableQuantity.Variable.Min.IsSome &&
                            cmp.OrderableQuantity.Variable.Incr.IsSome &&
                            cmp.OrderableQuantity.Variable.Max.IsSome ||
                            c >= 1

                        if not show then None
                        else
                            let cmpName = cmp.Name
                            ViewHelpers.createNav dispatch navigable solved
                                (SetMinComponentQuantityProperty cmpName)
                                (fun (n, uc) -> DecreaseComponentQuantityProperty (cmpName, n, uc))
                                (SetMedianComponentQuantityProperty cmpName)
                                (fun (n, uc) -> IncreaseComponentQuantityProperty (cmpName, n, uc))
                                (SetMaxComponentQuantityProperty cmpName)

                    let qtyWarning = cmp.OrderableQuantity.Level |> getWarning

                    let qtyLabel = cmp.OrderableQuantity |> ViewHelpers.ovarLabel cmp.Name

                    let qtyControl =
                        select isLoading qtyLabel None (fun s -> ChangeComponentOrderableQuantity (cmp.Name, s) |> dispatch) nav false qtyWarning (Some 400) qtyVals

                    // Dose display (dosering) - always show with label
                    let doseLabel = cmp.Dose.QuantityAdjust |> ViewHelpers.ovarLabel cmp.Name
                    let doseWarning = cmp.Dose.QuantityAdjust.Level |> getWarning
                    let doseVals = cmp.Dose.QuantityAdjust |> ViewHelpers.ovarVals (fixPrecision 3)

                    let doseDisplay =
                        select isLoading doseLabel None (fun s -> ChangeComponentDoseQuantityAdjust (cmp.Name, s) |> dispatch) None false doseWarning (Some 400) doseVals

                    JSX.jsx
                        $"""
                    import Grid from '@mui/material/Grid';
                    import Box from '@mui/material/Box';
                    <Grid container spacing={{2}} alignItems="flex-end">
                        <Grid size={halfSize}>
                            <Box sx={cellSx}>
                                {qtyControl}
                            </Box>
                        </Grid>
                        <Grid size={halfSize}>
                            <Box sx={cellSx}>
                                {doseDisplay}
                            </Box>
                        </Grid>
                    </Grid>
                    """
                )
            | None ->
                [| null |]

        let isEnteral =
            props.nutritionContext.Category = NutritionCategory.EnteralFeeding
            || props.nutritionContext.Category = NutritionCategory.EnteralSupplement

        let selectMinWidth = if isEnteral then None else Some 400

        let doseQtyControl =
            match displayOrder with
            | Some ord ->
                let warning = ord.Orderable.Dose.Quantity.Level |> getWarning
                let label = ord.Orderable.Dose.Quantity |> ViewHelpers.ovarLabel "toedien hoeveelheid"
                let vals = ord.Orderable.Dose.Quantity |> ViewHelpers.ovarValsWithRange string 3

                let showNav =
                    ord.Orderable.Components
                    |> Array.forall (fun cmp ->
                        cmp.OrderableQuantity.Variable.Vals
                        |> Option.map (fun vu ->
                            vu.Value |> Array.length = 1
                        )
                        |> Option.defaultValue false
                    )

                let doseQtyNav =
                    if not showNav then None
                    else
                        let canIncr =
                            ord.Orderable.Components |> Array.length = 1 ||
                            ord.Orderable.DoseCount.Variable.Vals
                            |> Option.map (fun vu ->
                                vu.Value
                                |> Array.map snd
                                |> Array.forall (fun v -> v > 1m)
                            )
                            |> Option.defaultValue false

                        let solved = ord |> isSolved
                        let navigable =
                            ord.Orderable.Dose.Quantity
                            |> OrderVariable.isNavigable

                        {|
                            first =
                                if navigable then (fun (_: int) -> SetMinDoseQuantityProperty |> dispatch) |> Some
                                elif solved then (fun n -> (n, true) |> DecreaseDoseQuantityProperty |> dispatch) |> Some
                                else None
                            decrease =
                                if solved then (fun n -> (n, false) |> DecreaseDoseQuantityProperty |> dispatch) |> Some
                                else None
                            median =
                                if navigable then (fun () -> SetMedianDoseQuantityProperty |> dispatch) |> Some
                                else None
                            increase =
                                if solved && canIncr then (fun n -> (n, false) |> IncreaseDoseQuantityProperty |> dispatch) |> Some
                                else None
                            last =
                                if navigable then (fun (_: int) -> SetMaxDoseQuantityProperty |> dispatch) |> Some
                                elif solved && canIncr then (fun n -> (n, true) |> IncreaseDoseQuantityProperty |> dispatch) |> Some
                                else None
                            useDebounce = not navigable && solved
                        |}
                        |> Some

                select isLoading label None (ChangeOrderableDoseQuantity >> dispatch) doseQtyNav false warning selectMinWidth vals
            | None ->
                null

        let dosePerTimeAdjDisplay =
            match displayOrder with
            | Some ord ->
                ord.Orderable.Dose.PerTimeAdjust
                |> ViewHelpers.ovarDisplay select "dosering" (fixPrecision 3) selectMinWidth
            | None ->
                null

        let frequencyControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed ->
                let warning = ord.Schedule.Frequency.Level |> getWarning
                let label = ord.Schedule.Frequency |> ViewHelpers.ovarLabel "frequentie"
                let freqVals = ord.Schedule.Frequency |> ViewHelpers.ovarVals string

                select isLoading label None (ChangeFrequency >> dispatch) None false warning selectMinWidth freqVals
            | _ ->
                null

        let genericFilter =
            let sel = ctx.Filter.Generic
            let items = ctx.Filter.Generics
            let lbl = "Samenstelling"
            let onChange = genericChange

            if isMobile then
                items
                |> Array.map (fun s -> s, s)
                |> filterSelect lbl sel onChange
            else
                items
                |> autoComplete lbl sel onChange

        let frequencyDoseRow =
            if isEnteral then
                let flexSx =
                    {|
                        display = "flex"
                        flexWrap = "wrap"
                        gap = 4
                        alignItems = "flex-end"
                        width = "100%"
                    |}
                let itemSx =
                    {|
                        flex = "1 1 0%"
                        minWidth = 200
                        ``& .MuiFormControl-root`` = {| width = "100%" |}
                        ``& .MuiAutocomplete-root`` = {| minWidth = "unset" |}
                    |}
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={flexSx}>
                    <Box sx={itemSx}>
                        {genericFilter}
                    </Box>                        
                    <Box sx={itemSx}>
                        {frequencyControl}
                    </Box>
                    <Box sx={itemSx}>
                        {doseQtyControl}
                    </Box>
                    <Box sx={itemSx}>
                        {dosePerTimeAdjDisplay}
                    </Box>
                </Box>
                """
            else
                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                import Box from '@mui/material/Box';
                <Grid container spacing={{2}} alignItems="flex-end">
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {frequencyControl}
                        </Box>
                    </Grid>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {doseQtyControl}
                        </Box>
                    </Grid>
                </Grid>
                """

        let rateControl =
            match displayOrder with
            | Some ord when ord.Schedule.IsTimed || ord.Schedule.IsContinuous ->
                let solved = ord |> isSolved
                let navigable = ord.Orderable.Dose.Rate |> OrderVariable.isNavigable

                let nav =
                    ViewHelpers.createNav dispatch navigable solved
                        SetMinDoseRateProperty
                        DecreaseDoseRateProperty
                        SetMedianDoseRateProperty
                        IncreaseDoseRateProperty
                        SetMaxDoseRateProperty

                let warning = ord.Orderable.Dose.Rate.Level |> getWarning
                let label = ord.Orderable.Dose.Rate |> ViewHelpers.ovarLabel "infuussnelheid"

                let rateDisplay =
                    ord.Orderable.Dose.Rate
                    |> ViewHelpers.ovarValsWithRange string 3
                    |> select isLoading label None (ChangeOrderableDoseRate >> dispatch) nav false warning (Some 400)

                let timeDisplay =
                    ord.Schedule.Time
                    |> ViewHelpers.ovarDisplay select "looptijd" (fixPrecision 3) (Some 400)

                JSX.jsx
                    $"""
                import Grid from '@mui/material/Grid';
                import Box from '@mui/material/Box';
                <Grid container spacing={{2}} alignItems="flex-end">
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {rateDisplay}
                        </Box>
                    </Grid>
                    <Grid size={halfSize}>
                        <Box sx={cellSx}>
                            {timeDisplay}
                        </Box>
                    </Grid>
                </Grid>
                """
            | _ ->
                null

        let totalVolumeDisplay =
            match displayOrder with
            | Some ord ->
                ord.Orderable.OrderableQuantity
                |> ViewHelpers.ovarDisplay select "totaal volume" string (Some 400)
            | None ->
                null

        let onClickReset =
            fun () -> ResetOrderScenario |> dispatch

        let administrationDivider =
            JSX.jsx
                $"""<Divider><Typography variant="caption">toediening</Typography></Divider>"""

        let headerRow =
            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            import Typography from '@mui/material/Typography';
            <Grid container spacing={{2}}>
                <Grid size={halfSize}>
                    <Typography variant="caption" color="text.secondary">bereiding</Typography>
                </Grid>
                <Grid size={halfSize}>
                    <Typography variant="caption" color="text.secondary">dosering</Typography>
                </Grid>
            </Grid>
            """

        let preparationSection =
            if isEnteral then null
            else
                JSX.jsx
                    $"""
                import Divider from '@mui/material/Divider';
                <>
                    {headerRow}
                    {
                        componentRows
                        |> unbox
                        |> React.fragment
                    }
                    <Divider />
                    {totalVolumeDisplay}
                </>
                """

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            import Divider from '@mui/material/Divider';
            import Typography from '@mui/material/Typography';
            import Button from '@mui/material/Button';
            <Stack direction={"column"} spacing={1} >
                {preparationSection}
                {if isEnteral then null else administrationDivider}
                {frequencyDoseRow}
                {rateControl}
                {loadingIndicator}
                <Button
                    variant="outlined"
                    size="small"
                    disabled={isOrderLoading}
                    onClick={fun _ -> onClickReset ()}
                >
                    Reset
                </Button>
            </Stack>
            """

        let indicationFilter =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Indications |> Array.length <= 1 then null
            else
                let sel = ctx.Filter.Indication
                let items = ctx.Filter.Indications
                let lbl = "Indicatie"

                if isMobile then
                    items
                    |> Array.map (fun s -> s, s)
                    |> filterSelect lbl sel indicationChange
                else
                    items
                    |> autoComplete lbl sel indicationChange

        let doseTypeFilter =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Indication.IsNone || ctx.Filter.DoseTypes |> Array.length <= 1 then null
            else
                let sel = ctx.Filter.DoseType |> Option.map DoseType.doseTypeToString
                let items = ctx.Filter.DoseTypes
                let lbl = "Doseer type"

                items
                |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                |> filterSelect lbl sel doseTypeChange

        let filterSx = {| marginBottom = 2 |}
        let filterControls =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            <Stack direction="column" spacing={2} sx={filterSx}>
                {if isEnteral && displayOrder.IsSome then null else genericFilter}
                {indicationFilter}
                {doseTypeFilter}
            </Stack>
            """

        let orderDetails =
            if displayOrder.IsSome then details
            else loadingIndicator

        let expanded, setExpanded = React.useState true

        let handleAccordionChange =
            fun _ -> setExpanded (not expanded)

        let removeButton =
            match props.onRemove with
            | Some onRemove ->
                let handleClick (e: Browser.Types.Event) =
                    e.stopPropagation()
                    onRemove ()
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import DeleteIcon from '@mui/icons-material/Delete';
                <Box
                    component="span"
                    onClick={handleClick}
                    sx={
                        {|
                            marginLeft="auto"
                            display="inline-flex"
                            alignItems="center"
                            cursor="pointer"
                            padding="4px"
                            borderRadius="50%"
                            ``&:hover`` = {| backgroundColor="rgba(0, 0, 0, 0.04)" |}
                        |}
                    }
                >
                    <DeleteIcon fontSize="small" />
                </Box>
                """
            | None -> null

        if props.wrapInAccordion then
            let summary =
                JSX.jsx
                    $"""
                import React from 'react';
                import Typography from '@mui/material/Typography';

                <React.Fragment>
                    <Typography>{props.nutritionContext.Label}</Typography>
                    {removeButton}
                </React.Fragment>
                """

            let children =
                JSX.jsx
                    $"""
                import React from 'react';

                <React.Fragment>
                    {filterControls}
                    {orderDetails}
                </React.Fragment>
                """

            Components.Accordion.View
                {|
                    expanded = expanded
                    onChange = fun () -> setExpanded (not expanded)
                    summary = summary
                    children = children
                    isMobile = isMobile
                    detailsPaddingTop = if isMobile then None else Some 4
                    ariaControls = None
                    summaryId = None
                |}
        else
            JSX.jsx
                $"""
            import React from "react";
            import Stack from '@mui/material/Stack';
            import Divider from '@mui/material/Divider';
            import Typography from '@mui/material/Typography';
            import Box from '@mui/material/Box';

            <Box>
                <Divider>
                    <Stack direction="row" spacing={{1}} alignItems="center">
                        <Typography variant="caption">{props.nutritionContext.Label}</Typography>
                        {removeButton}
                    </Stack>
                </Divider>
                {filterControls}
                {orderDetails}
            </Box>
            """


    [<JSX.Component>]
    let private AddButton (props: {|
        label: string
        onClick: unit -> unit
    |}) =
        JSX.jsx
            $"""
        import Button from '@mui/material/Button';
        import AddIcon from '@mui/icons-material/Add';
        <Button
            variant="outlined"
            size="small"
            startIcon={{ <AddIcon /> }}
            onClick={fun _ -> props.onClick ()}
            sx={ {| marginTop=1; marginBottom=1 |} }
        >
            {props.label}
        </Button>
        """


    [<JSX.Component>]
    let View (props: {|
        patient: Patient option
        nutritionPlan: Deferred<NutritionPlan>
        nutritionPlanMsg: Api.NutritionPlanCommand -> unit
        orderContextMsg: (Api.OrderContextCommand * OrderContext) -> unit
        localizationTerms: Deferred<string[][]>
    |}) =

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        React.useEffect(
            (fun () ->
                match props.patient, props.nutritionPlan with
                | Some pat, HasNotStartedYet ->
                    props.nutritionPlanMsg (Api.InitNutritionPlan pat)
                | _ -> ()
            ),
            [| box props.patient; box props.nutritionPlan |]
        )

        let progress =
            match props.nutritionPlan with
            | HasNotStartedYet when props.patient.IsNone ->
                JSX.jsx $"<>Voer eerst patient gegevens in</>"
            | _ ->
                ViewHelpers.progressOrEmpty props.nutritionPlan

        let isRecalculating =
            match props.nutritionPlan with
            | Recalculating _ -> true
            | _ -> false

        let confirmDeleteTarget, setConfirmDeleteTarget = React.useState<string option> None
        let enteralExpanded, setEnteralExpanded = React.useState true

        let makeSlot wrapInAccordion plan nc =
            let onRemove =
                if nc.Removable then
                    let hasSupplements =
                        nc.Category = NutritionCategory.EnteralFeeding &&
                        plan.NutritionContexts |> Array.exists (fun c -> c.Category = NutritionCategory.EnteralSupplement)
                    Some (fun () ->
                        if hasSupplements then
                            setConfirmDeleteTarget (Some nc.Id)
                        else
                            Api.RemoveNutritionContext(plan, nc.Id)
                            |> props.nutritionPlanMsg
                    )
                else None
            NutritionSlot {|
                nutritionContext = nc
                plan = plan
                nutritionPlanMsg = props.nutritionPlanMsg
                localizationTerms = props.localizationTerms
                onRemove = onRemove
                wrapInAccordion = wrapInAccordion
                isRecalculating = isRecalculating
            |}

        let addContext plan category =
            Api.AddNutritionContext(plan, category) |> props.nutritionPlanMsg

        let hasCategory plan cat =
            plan.NutritionContexts |> Array.exists (fun nc -> nc.Category = cat)

        let content =
            match props.nutritionPlan with
            | Resolved plan
            | Recalculating plan ->
                let enteralContexts =
                    plan.NutritionContexts
                    |> Array.filter (fun nc ->
                        nc.Category = NutritionCategory.EnteralFeeding
                        || nc.Category = NutritionCategory.EnteralSupplement
                    )

                let parenteralContexts =
                    plan.NutritionContexts
                    |> Array.filter (fun nc ->
                        nc.Category = NutritionCategory.TPN ||
                        nc.Category = NutritionCategory.Lipid ||
                        nc.Category = NutritionCategory.ElectrolyteGlucose
                    )

                let enteralSlots =
                    enteralContexts
                    |> Array.map (makeSlot false plan)

                let parenteralSlots =
                    parenteralContexts
                    |> Array.map (makeSlot true plan)

                let hasEnteral = hasCategory plan NutritionCategory.EnteralFeeding
                let hasTPN = hasCategory plan NutritionCategory.TPN
                let hasLipid = hasCategory plan NutritionCategory.Lipid

                let enteralFeedingAddButton =
                    if not hasEnteral then
                        AddButton {| label = "Enterale Voeding"; onClick = fun () -> addContext plan NutritionCategory.EnteralFeeding |}
                    else null

                let supplementAddButton =
                    if hasEnteral then
                        AddButton {| label = "Supplement toevoegen"; onClick = fun () -> addContext plan NutritionCategory.EnteralSupplement |}
                    else null

                let parenteralAddButtons =
                    [|
                        if not hasTPN then
                            AddButton {| label = "TPN"; onClick = fun () -> addContext plan NutritionCategory.TPN |}
                        if not hasLipid then
                            AddButton {| label = "Vetten"; onClick = fun () -> addContext plan NutritionCategory.Lipid |}
                        AddButton {| label = "Elektrolyten/Glucose"; onClick = fun () -> addContext plan NutritionCategory.ElectrolyteGlucose |}
                    |]

                let enteralAccordion =
                    let summary =
                        JSX.jsx
                            $"""
                        import Typography from '@mui/material/Typography';
                        <Typography>Enterale Voeding</Typography>
                        """

                    let children =
                        JSX.jsx
                            $"""
                        import Stack from '@mui/material/Stack';
                        <Stack direction="column" spacing={{2}}>
                            {enteralSlots |> unbox |> React.fragment}
                            {enteralFeedingAddButton}
                            {supplementAddButton}
                        </Stack>
                        """

                    Components.Accordion.View
                        {|
                            expanded = enteralExpanded
                            onChange = fun () -> setEnteralExpanded (not enteralExpanded)
                            summary = summary
                            children = children
                            isMobile = isMobile
                            detailsPaddingTop = if isMobile then None else Some 4
                            ariaControls = None
                            summaryId = None
                        |}

                JSX.jsx
                    $"""
                import Stack from '@mui/material/Stack';
                import Typography from '@mui/material/Typography';
                import Divider from '@mui/material/Divider';

                <Stack direction="column" spacing={1}>
                    {enteralAccordion}
                    <Divider sx={ {| marginTop=2; marginBottom=2 |} } />
                    <Typography variant="h6">Parenteraal</Typography>
                    {parenteralSlots |> unbox |> React.fragment}
                    <Stack direction="row" spacing={1}>
                        {
                            parenteralAddButtons
                            |> unbox
                            |> React.fragment
                        }
                    </Stack>
                </Stack>
                """
            | _ -> null

        let confirmDeleteDialog =
            let isOpen = confirmDeleteTarget.IsSome
            let handleCancel = fun _ -> setConfirmDeleteTarget None
            let handleConfirm = fun _ ->
                match confirmDeleteTarget, props.nutritionPlan with
                | Some ncId, (Resolved plan | Recalculating plan) ->
                    Api.RemoveNutritionContext(plan, ncId)
                    |> props.nutritionPlanMsg
                | _ -> ()
                setConfirmDeleteTarget None
            JSX.jsx
                $"""
            import Dialog from '@mui/material/Dialog';
            import DialogTitle from '@mui/material/DialogTitle';
            import DialogContent from '@mui/material/DialogContent';
            import DialogContentText from '@mui/material/DialogContentText';
            import DialogActions from '@mui/material/DialogActions';
            import Button from '@mui/material/Button';

            <Dialog open={isOpen} onClose={handleCancel}>
                <DialogTitle>Enterale voeding verwijderen</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        Als u de enterale voeding verwijdert, worden ook alle bijbehorende supplementen verwijderd. Wilt u doorgaan?
                    </DialogContentText>
                </DialogContent>
                <DialogActions>
                    <Button onClick={handleCancel}>Annuleren</Button>
                    <Button onClick={handleConfirm} color="error">Verwijderen</Button>
                </DialogActions>
            </Dialog>
            """

        JSX.jsx
            $"""
        import React from "react";
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box sx={ {| paddingBottom=(if isMobile then "16px" else "220px") |} }>
            {content}
            {progress}
            {confirmDeleteDialog}
        </Box>
        """

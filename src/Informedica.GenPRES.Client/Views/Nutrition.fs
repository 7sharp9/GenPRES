namespace Views

module Nutrion =


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
            | ChangeOrderableDoseRate of string option
            | ChangeOrderableQuantity of string option
            | UpdateOrderScenario of Order
            | ResetOrderScenario
            // Rate navigation
            | DecreaseDoseRateProperty of ntimes: int * useCalc: bool
            | IncreaseDoseRateProperty of ntimes: int * useCalc: bool
            | SetMinDoseRateProperty
            | SetMaxDoseRateProperty
            | SetMedianDoseRateProperty
            // Component Quantity navigation (carries component name)
            | DecreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | IncreaseComponentQuantityProperty of cmp: string * ntimes: int * useCalc: bool
            | SetMinComponentQuantityProperty of cmp: string
            | SetMaxComponentQuantityProperty of cmp: string
            | SetMedianComponentQuantityProperty of cmp: string


        let init (ctx : Deferred<OrderContext>) =
            let ord, cmp =
                match ctx with
                | Resolved ctx ->
                    match ctx.Scenarios with
                    | [| sc |] ->
                        let ord = sc.Order
                        match ord.Orderable.Components with
                        | [||] -> Some ord, None
                        | cmps -> Some ord, Some cmps[0].Name
                    | _ -> None, None
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

            // Rate navigation
            | SetMinDoseRateProperty -> handleNav navigate.setRateMin
            | DecreaseDoseRateProperty (n, uc) -> handleNav (navigate.setRateDec (n, uc))
            | SetMedianDoseRateProperty -> handleNav navigate.setRateMed
            | IncreaseDoseRateProperty (n, uc) -> handleNav (navigate.setRateInc (n, uc))
            | SetMaxDoseRateProperty -> handleNav navigate.setRateMax

            // Component Quantity navigation
            | SetMinComponentQuantityProperty cmp -> handleNavWithCmp cmp navigate.setComponentQtyMin
            | DecreaseComponentQuantityProperty (cmp, n, uc) -> handleNavWithCmp cmp (navigate.setComponentQtyDec (n, uc))
            | SetMedianComponentQuantityProperty cmp -> handleNavWithCmp cmp navigate.setComponentQtyMed
            | IncreaseComponentQuantityProperty (cmp, n, uc) -> handleNavWithCmp cmp (navigate.setComponentQtyInc (n, uc))
            | SetMaxComponentQuantityProperty cmp -> handleNavWithCmp cmp navigate.setComponentQtyMax


    open Elmish


    [<JSX.Component>]
    let private NutritionSlot (props: {|
        nutritionContext: NutritionContext
        plan: NutritionPlan
        nutritionPlanMsg: Api.NutritionPlanCommand -> unit
        localizationTerms: Deferred<string [][]>
    |}) =
        let ctx = props.nutritionContext.OrderContext

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision

        let getWarning = ViewHelpers.getWarning
        let select = ViewHelpers.orderSelect

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
            |> fun updCtx -> Api.UpdateNutritionOrderContext(props.plan, updCtx) |> props.nutritionPlanMsg

        let resetOrderScenario (_ol : OrderLoader) =
            // Use ResetOrderScenario command which runs ReCalcValues pipeline
            // (re-applies constraints and recalculates from scratch)
            Api.NavigateNutritionOrderContext(props.plan, Api.ResetOrderScenario, ctx)
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

            let navRate cmd = fun updCtx -> Api.NavigateNutritionOrderContext(props.plan, cmd, updCtx) |> props.nutritionPlanMsg
            let navRateN cmd = fun (updCtx, n, uc) -> Api.NavigateNutritionOrderContext(props.plan, cmd (n, uc), updCtx) |> props.nutritionPlanMsg
            let navCmpQty cmd = fun (updCtx, cmp) -> Api.NavigateNutritionOrderContext(props.plan, cmd cmp, updCtx) |> props.nutritionPlanMsg
            let navCmpQtyN cmd = fun (updCtx, cmp, n, uc) -> Api.NavigateNutritionOrderContext(props.plan, cmd (cmp, n, uc), updCtx) |> props.nutritionPlanMsg

            {|
                // Dose Rate
                setRateMin = create (navRate Api.SetMinOrderableDoseRateProperty)
                setRateDec = createWithN (navRateN Api.DecreaseOrderableDoseRateProperty)
                setRateMed = create (navRate Api.SetMedianOrderableDoseRateProperty)
                setRateInc = createWithN (navRateN Api.IncreaseOrderableDoseRateProperty)
                setRateMax = create (navRate Api.SetMaxOrderableDoseRateProperty)
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

        let isLoading = state.Order.IsNone

        let componentRows =
            match state.Order with
            | Some ord ->
                ord.Orderable.Components
                |> Array.map (fun cmp ->
                    // Quantity control (bereiding)
                    let qtyVals =
                        cmp.OrderableQuantity.Variable.Vals
                        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                        |> Option.defaultValue (
                            cmp.OrderableQuantity.Variable |> Order.Variable.renderValue 3
                            |> function | "" -> [||] | s -> [| "range", s |]
                        )

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

                    let qtyLabel =
                        cmp.OrderableQuantity.Variable.Vals
                        |> Option.map (fun v -> $"{cmp.Name} ({v.Unit})")
                        |> Option.defaultValue cmp.Name

                    let qtyControl =
                        select isLoading qtyLabel None (fun s -> ChangeComponentOrderableQuantity (cmp.Name, s) |> dispatch) nav false qtyWarning (Some 400) qtyVals

                    // Dose display (dosering) - always show with label
                    let doseLabel =
                        cmp.Dose.QuantityAdjust.Variable.Vals
                        |> Option.map (fun v -> $"{cmp.Name} ({v.Unit})")
                        |> Option.defaultValue cmp.Name

                    let doseWarning = cmp.Dose.QuantityAdjust.Level |> getWarning

                    let doseVals =
                        cmp.Dose.QuantityAdjust.Variable.Vals
                        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                        |> Option.defaultValue [||]

                    let doseDisplay =
                        select false doseLabel None ignore None true doseWarning (Some 400) doseVals

                    let halfSize = {| xs = 12; md = 6 |}
                    let cellSx = {| minWidth = 350 |}
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
                [| ViewHelpers.empty |]

        let totalDoseRow =
            match state.Order with
            | Some ord ->
                let warning = ord.Orderable.Dose.Quantity.Level |> getWarning
                let label =
                    ord.Orderable.Dose.Quantity.Variable.Vals
                    |> Option.map (fun v -> $"totaal ({v.Unit})")
                    |> Option.defaultValue "totaal"

                let vals =
                    ord.Orderable.Dose.Quantity.Variable.Vals
                    |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                    |> Option.defaultValue [||]

                select false label None ignore None true warning (Some 400) vals
            | None ->
                ViewHelpers.empty

        let rateControl =
            match state.Order with
            | Some ord ->
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

                let label =
                    ord.Orderable.Dose.Rate.Variable.Vals
                    |> Option.map (fun v -> $"infuussnelheid ({v.Unit})")
                    |> Option.defaultValue "infuussnelheid"

                ord.Orderable.Dose.Rate.Variable.Vals
                |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                |> Option.defaultValue (
                    match Order.Variable.renderValue 3 ord.Orderable.Dose.Rate.Variable with
                    | "" -> [||]
                    | s -> [| "range", s |]
                )
                |> select isLoading label None (ChangeOrderableDoseRate >> dispatch) nav false warning (Some 400)
            | None ->
                ViewHelpers.empty

        let totalVolumeDisplay =
            match state.Order with
            | Some ord ->
                let warning = ord.Orderable.OrderableQuantity.Level |> getWarning
                let label =
                    ord.Orderable.OrderableQuantity.Variable.Vals
                    |> Option.map (fun v -> $"totaal volume ({v.Unit})")
                    |> Option.defaultValue "totaal volume"

                ord.Orderable.OrderableQuantity.Variable.Vals
                |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                |> Option.defaultValue [||]
                |> select false label None ignore None true warning (Some 400)
            | None ->
                ViewHelpers.empty

        let onClickReset =
            fun () -> ResetOrderScenario |> dispatch

        let administrationDivider =
            JSX.jsx
                $"""<Divider><Typography variant="caption">toediening</Typography></Divider>"""

        let headerRow =
            let halfSize = {| xs = 12; md = 6 |}
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

        let details =
            JSX.jsx
                $"""
            import Stack from '@mui/material/Stack';
            import Divider from '@mui/material/Divider';
            import Typography from '@mui/material/Typography';
            import Button from '@mui/material/Button';
            <Stack direction={"column"} spacing={1} >
                {headerRow}
                {
                    componentRows
                    |> unbox
                    |> React.fragment
                }
                <Divider />
                {totalDoseRow}
                {administrationDivider}
                {rateControl}
                {totalVolumeDisplay}
                <Button
                    variant="outlined"
                    size="small"
                    onClick={fun _ -> onClickReset ()}
                >
                    Reset
                </Button>
            </Stack>
            """

        let expanded, setExpanded = React.useState true

        let handleAccordionChange =
            fun _ _ -> setExpanded (not expanded)

        JSX.jsx
            $"""
        import React from "react";
        import Box from '@mui/material/Box';
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import Typography from '@mui/material/Typography';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

        <Accordion expanded={expanded} onChange={handleAccordionChange}>
            <AccordionSummary
            sx={ {| bgcolor=Mui.Colors.Grey.``100`` |} }
            expandIcon={{ <ExpandMoreIcon /> }}
            >
            <Typography>{props.nutritionContext.Label}</Typography>
            </AccordionSummary>
            <AccordionDetails>
                {details}
            </AccordionDetails>
        </Accordion>
        """


    [<JSX.Component>]
    let View (props: {|
        patient: Patient option
        nutritionPlan: Deferred<NutritionPlan>
        nutritionPlanMsg: Api.NutritionPlanCommand -> unit
        orderContextMsg: (Api.OrderContextCommand * OrderContext) -> unit
        localizationTerms: Deferred<string[][]>
    |}) =

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

        let slots plan =
            plan.NutritionContexts
            |> Array.map (fun nc ->
                NutritionSlot {|
                    nutritionContext = nc
                    plan = plan
                    nutritionPlanMsg = props.nutritionPlanMsg
                    localizationTerms = props.localizationTerms
                |}
            )

        let content =
            match props.nutritionPlan with
            | Resolved plan when plan.NutritionContexts |> Array.isEmpty ->
                JSX.jsx $"<Typography>Geen voedingsplannen beschikbaar voor deze patiënt</Typography>"
            | Resolved plan ->
                JSX.jsx
                    $"""
                import Stack from '@mui/material/Stack';

                <Stack direction="column" spacing={1}>
                    {
                        slots plan
                        |> unbox
                        |> React.fragment
                    }
                </Stack>
                """
            | _ -> ViewHelpers.empty

        JSX.jsx
            $"""
        import React from "react";
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <React.Fragment>
            {content}
            {progress}
        </React.Fragment>
        """

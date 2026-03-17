namespace Views


module Prescribe =

    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models


    type private LoadingSource =
        | IndicationLoading
        | MedicationLoading
        | RouteLoading
        | FormLoading
        | DiluentLoading
        | ComponentsLoading
        | DoseTypeLoading


    [<JSX.Component>]
    let View (props:
        {|
            orderContext: Deferred<OrderContext>
            orderContextMsg: Api.OrderContextCommand * OrderContext -> unit
            treatmentPlan : Deferred<OrderPlan>
            updateTreatmentPlan : OrderPlan -> unit
            localizationTerms : Deferred<string [] []>
        |}) =

        let context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:900px)"

        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let loadingSource, setLoadingSource = React.useState<LoadingSource option> None

        React.useEffect (
            (fun () ->
                match props.orderContext with
                | Resolved _ -> setLoadingSource None
                | _ -> ()
            ),
            [| box props.orderContext |]
        )

        let updateOrderContext ctx = props.orderContextMsg (Api.UpdateOrderContext, ctx)

        let indicationChange s =
            match props.orderContext with
            | Resolved pr ->
                setLoadingSource (Some IndicationLoading)
                pr |> OrderContext.indicationChange s |> updateOrderContext
            | _ -> ()

        let medicationChange s =
            match props.orderContext with
            | Resolved pr ->
                setLoadingSource (Some MedicationLoading)
                pr |> OrderContext.medicationChange s |> updateOrderContext
            | _ -> ()

        let routeChange s =
            match props.orderContext with
            | Resolved pr ->
                setLoadingSource (Some RouteLoading)
                pr |> OrderContext.routeChange s |> updateOrderContext
            | _ -> ()

        let formChange s =
            match props.orderContext with
            | Resolved ctx ->
                setLoadingSource (Some FormLoading)
                ctx |> OrderContext.formChange s |> updateOrderContext
            | _ -> ()

        let diluentChange s =
            match props.orderContext with
            | Resolved pr ->
                setLoadingSource (Some DiluentLoading)
                pr |> OrderContext.diluentChange s |> updateOrderContext
            | _ -> ()

        let componentsChange cs =
            Logging.log "componentsChange" cs
            match props.orderContext with
            | Resolved prctx ->
                setLoadingSource (Some ComponentsLoading)
                prctx |> OrderContext.componentsChange cs |> updateOrderContext
            | _ -> ()

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString
            match props.orderContext with
            | Resolved pr ->
                setLoadingSource (Some DoseTypeLoading)
                pr |> OrderContext.doseTypeChange dt |> updateOrderContext
            | _ -> ()

        let clear () =
            match props.orderContext with
            | Resolved _ ->
                setLoadingSource None
                OrderContext.empty |> updateOrderContext
            | _ -> ()

        let modalOpen, setModalOpen = React.useState false
        let handleModalClose = fun () -> setModalOpen false

        let isAnythingLoading =
            match props.orderContext with
            | InProgress | Recalculating _ -> true
            | _ -> false

        let isSourceLoading source =
            isAnythingLoading && loadingSource = Some source

        let select = ViewHelpers.simpleSelect isAnythingLoading

        let multiSelect isLoading lbl selected dispatch xs =
            Components.MultipleSelect.View({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
                disabled = isAnythingLoading
            |})

        let autoComplete = ViewHelpers.autoComplete isAnythingLoading

        let progress =
            match props.orderContext with
            | HasNotStartedYet -> JSX.jsx $"<>Voer eerst patient gegevens in</>"
            | _ -> null


        let displayScenario (pr : OrderContext) med (sc : OrderScenario) =
            if med |> Option.isNone then null
            else
                let caption =
                    let renal =
                        sc.RenalRule
                        |> Option.map (fun s ->
                            $" (doseer aanpassing volgens {s})"
                        )
                        |> Option.defaultValue ""
                    $"{sc.Form}{renal}"

                let onClick (sc : OrderScenario) =
                    let ctx = { pr with
                                    Filter = { pr.Filter with Form = Some sc.Form }
                                    Scenarios = [| sc |] }
                    props.orderContextMsg (Api.SelectOrderScenario, ctx)

                let updateTreatmentPlan () =
                    match props.treatmentPlan with
                    | Resolved tp ->
                        { tp with
                            Scenarios =
                                [| sc |]
                                |> Array.append tp.Scenarios
                        }
                        |> props.updateTreatmentPlan
                    | _ -> ()

                let item key icon prim (sec : TextBlock [][]) =
                    let rows =
                        let cells row =
                            row
                            |> Array.mapi (fun i cell ->
                                    JSX.jsx $"""
                                    <TableCell key={i} sx = { {| paddingTop=1; paddingRight=2 |} }>
                                        {cell |> Mui.TypoGraphy.fromTextBlock}
                                    </TableCell>
                                    """
                            )

                        let getItems tb =
                            match tb with
                            | Valid itms
                            | Caution itms
                            | Warning itms
                            | Alert itms ->
                                itms
                                |> Array.append [| " " |> Normal |]

                        let sec =
                             if not isMobile then sec
                             else
                                // flatten the TextBlock [] [] to a single TextBlock
                                let add xs =
                                    let plus = [| [| " + " |> Normal |] |]

                                    xs
                                    |> Array.fold (fun acc x ->
                                        if acc |> Array.isEmpty then x
                                        else
                                            x
                                            |> Array.append plus
                                            |> Array.append acc
                                    ) [||]
                                    |> Array.collect id

                                sec
                                |> Array.map (Array.map getItems)
                                |> add
                                |> (sec |> TextBlock.maxTb)
                                |> Array.singleton
                                |> Array.singleton

                        sec
                        |> Array.mapi (fun i row ->
                            JSX.jsx $"""
                                <TableRow key={i} sx={ {| border=0 |} } >
                                    {cells row}
                                </TableRow>
                            """
                        )

                    JSX.jsx
                        $"""
                    import Table from '@mui/material/Table';
                    import TableBody from '@mui/material/TableBody';
                    import TableCell from '@mui/material/TableCell';
                    import TableContainer from '@mui/material/TableContainer';
                    import TableRow from '@mui/material/TableRow';

                    <ListItem key={key} >
                        <ListItemIcon>
                            {icon}
                        </ListItemIcon>
                        <TableContainer sx={ {| overflowX="auto" |} } >
                            <Table padding="none" size="small" sx={ {| tableLayout="auto"; width="auto" |} } >
                                <TableBody>
                                    <TableRow sx={ {| border=0; ``& td``={| borderBottom=0 |} |} } >
                                        <TableCell >
                                            {prim}
                                        </TableCell>
                                    </TableRow >
                                    {rows}
                                </TableBody>
                            </Table>
                        </TableContainer>
                    </ListItem>
                    """

                let content =
                    JSX.jsx
                        $"""
                    <React.Fragment>
                        <Typography variant="h6" sx={ {| backgroundColor=Mui.Styles.headerBgColor; padding=1; borderRadius=1 |} } >
                            {caption}
                        </Typography>
                        <List sx={ {| width="100%"; maxWidth=1200; bgcolor=Mui.Colors.Grey.``50`` |} }>
                            {
                                [|
                                    item "prescription" Mui.Icons.Notes (Terms.``Prescribe Prescription`` |> getTerm "Voorschrift") sc.Prescription
                                    if sc.Preparation |> Array.length > 0 then
                                        item "preparation" Mui.Icons.Vaccines (Terms.``Prescribe Preparation`` |> getTerm "Bereiding") sc.Preparation
                                    item "administration" Mui.Icons.MedicationLiquid (Terms.``Prescribe Administration`` |> getTerm "Toediening") sc.Administration
                                |]
                                |> unbox
                                |> React.fragment
                            }
                        </List>
                    </React.Fragment>
                    """

                JSX.jsx
                    $"""
                import React from 'react';
                import Card from '@mui/material/Card';
                import CardActions from '@mui/material/CardActions';
                import CardContent from '@mui/material/CardContent';
                import Button from '@mui/material/Button';
                import Typography from '@mui/material/Typography';
                import Box from '@mui/material/Box';
                import List from '@mui/material/List';
                import ListItem from '@mui/material/ListItem';
                import Divider from '@mui/material/Divider';
                import ListItemText from '@mui/material/ListItemText';
                import ListItemIcon from '@mui/material/ListItemIcon';
                import Avatar from '@mui/material/Avatar';
                import Typography from '@mui/material/Typography';

                <Box sx={ {| height="100%" |} } >
                    <Card sx={ {| padding=0 |}  }>
                        <CardContent sx={ {| padding=0 |} }>
                            {content}
                            {progress}
                        </CardContent>
                        <CardActions>
                            <Button
                                size="small"
                                disabled={isAnythingLoading}
                                onClick={fun () -> setModalOpen true; onClick sc}
                                startIcon={Mui.Icons.CalculateIcon}
                            >{Edit |> getTerm "bewerken"}</Button>
                            <Button
                                size="small"
                                disabled={isAnythingLoading}
                                onClick={updateTreatmentPlan}
                                startIcon={Mui.Icons.Add}
                            >Voorschrijven</Button>
                        </CardActions>
                    </Card>
                </Box>
                """

        let stackDirection =
            if isMobile then "column" else "row"

        let loadingIndicator = ViewHelpers.inlineProgress isAnythingLoading

        let cards =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';

            <React.Fragment>
                <Stack direction="column" spacing={if isMobile then 1 else 3}>
                    <Typography sx={ {| fontSize=14 |} } color="text.secondary" >
                        {Terms.``Prescribe Scenarios`` |> getTerm "Medicatie scenario's"}
                    </Typography>
                    {
                        match props.orderContext with
                        | Resolved pr | Recalculating pr -> pr.Filter.Indication, pr.Filter.Indications
                        | _ -> None, [||]
                        |> fun (sel, items) ->
                            let isLoading = isSourceLoading IndicationLoading
                            let lbl = Terms.``Prescribe Indications`` |> getTerm "Indicaties"

                            if isMobile then
                                items
                                |> Array.map (fun s -> s, s)
                                |> select isLoading lbl sel indicationChange
                            else
                                items
                                |> autoComplete isLoading lbl sel indicationChange
                    }
                    <Stack direction={stackDirection} spacing={if isMobile then 1 else 3} >
                        {
                            match props.orderContext with
                            | Resolved pr | Recalculating pr -> pr.Filter.Generic, pr.Filter.Generics
                            | _ -> None, [||]
                            |> fun (sel, items) ->
                                let isLoading = isSourceLoading MedicationLoading
                                let lbl = Terms.``Prescribe Medications`` |> getTerm "Medicatie"

                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel medicationChange
                                else
                                    items
                                    |> autoComplete isLoading lbl sel medicationChange

                        }
                        {
                            match props.orderContext with
                            | Resolved pr | Recalculating pr -> pr.Filter.Route, pr.Filter.Routes
                            | _ -> None, [||]
                            |> fun (sel, items) ->
                                let isLoading = isSourceLoading RouteLoading
                                let lbl = Terms.``Prescribe Routes`` |> getTerm "Routes"

                                if isMobile then
                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel routeChange
                                else
                                    items
                                    |> autoComplete isLoading lbl sel routeChange

                        }
                        {
                            match props.orderContext with
                            | Resolved ctx | Recalculating ctx when ctx.Filter.Forms |> Array.length >= 1 &&
                                                (not isMobile || ctx.Scenarios |> Array.length <> 1) ->
                                ctx.Filter.Form, ctx.Filter.Forms
                            | _ -> None, [||]
                            |> fun (sel, items) ->
                                let isLoading = isSourceLoading FormLoading
                                let lbl = "Vorm"

                                if items |> Array.isEmpty then null
                                else
                                    if isMobile then
                                        items
                                        |> Array.map (fun s -> s, s)
                                        |> select isLoading lbl sel formChange
                                    else
                                        items
                                        |> Array.map (fun s -> s, s)
                                        |> select isLoading lbl sel formChange
                        }
                        {
                            match props.orderContext with
                            | Resolved pr | Recalculating pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Generic.IsSome &&
                                               pr.Filter.Route.IsSome &&
                                               pr.Filter.Diluents |> Array.length > 1 &&
                                               pr.Scenarios |> Array.length = 1 ->

                                    let isLoading = isSourceLoading DiluentLoading
                                    let sel = pr.Filter.Diluent
                                    let items = pr.Filter.Diluents
                                    let lbl = "Verdunningsvorm"

                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> select isLoading lbl sel diluentChange

                            | _ -> null
                        }
                        {
                            match props.orderContext with
                            | Resolved pr | Recalculating pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Generic.IsSome &&
                                               pr.Filter.Route.IsSome &&
                                               pr.Filter.Components |> Array.length > 1 &&
                                               pr.Scenarios |> Array.length = 1 ->

                                    let isLoading = isSourceLoading ComponentsLoading
                                    let items = pr.Filter.Components
                                    let lbl = "Componenten"
                                    let sel =
                                        if pr.Filter.SelectedComponents |> Array.isEmpty then items
                                        else pr.Filter.SelectedComponents

                                    items
                                    |> Array.map (fun s -> s, s)
                                    |> multiSelect isLoading lbl sel componentsChange

                            | _ -> null
                        }
                        {
                            match props.orderContext with
                            | Resolved pr | Recalculating pr when pr.Filter.Indication.IsSome &&
                                               pr.Filter.Generic.IsSome &&
                                               pr.Filter.Route.IsSome ->
                                let isLoading = isSourceLoading DoseTypeLoading
                                let sel = pr.Filter.DoseType |> Option.map DoseType.doseTypeToString
                                let items = pr.Filter.DoseTypes
                                let lbl = "Doseer types"

                                items
                                |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                                |> select isLoading lbl sel doseTypeChange

                            | _ -> null
                        }
                    </Stack>
                    {loadingIndicator}
                    <Box sx={ {| marginTop=2 |} }>
                        <Button variant="text" onClick={clear} disabled={isAnythingLoading} fullWidth startIcon={Mui.Icons.Delete} >
                            {Delete |> getTerm "Verwijder"}
                        </Button>
                    </Box>
                    <Stack direction="column" spacing={1} >
                        {
                            match props.orderContext with
                            | Resolved pr | Recalculating pr ->
                                pr.Scenarios
                                |> Array.map (displayScenario pr pr.Filter.Generic)
                                |> unbox
                                |> React.fragment
                            | _ -> Seq.empty |> React.fragment
                        }
                    </Stack>
                </Stack>
            </React.Fragment>
            """

        let modalStyle = ViewHelpers.modalStyle

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Modal from '@mui/material/Modal';

        <div>
            <Box sx={ {| height="100%"; paddingBottom=(if isMobile then "16px" else "220px") |} }>
                {cards}
                {progress}
            </Box>
            <Modal open={modalOpen} onClose={handleModalClose} >
                <Box sx={modalStyle}>
                    {
                        Order.View {|
                            orderContext = props.orderContext
                            updateOrderScenario = fun ctx -> props.orderContextMsg (Api.UpdateOrderScenario, ctx)
                            navigateOrderScenario = {|
                                // Frequency
                                setMinFrequency = fun ctx -> props.orderContextMsg (Api.SetMinScheduleFrequencyProperty, ctx)
                                decrFrequency = fun ctx -> props.orderContextMsg (Api.DecreaseScheduleFrequencyProperty, ctx)
                                setMedianFrequency = fun ctx -> props.orderContextMsg (Api.SetMedianScheduleFrequencyProperty, ctx)
                                incrFrequency = fun ctx -> props.orderContextMsg (Api.IncreaseScheduleFrequencyProperty, ctx)
                                setMaxFrequency = fun ctx -> props.orderContextMsg (Api.SetMaxScheduleFrequencyProperty, ctx)
                                // Rate
                                setMinRate = fun ctx -> props.orderContextMsg (Api.SetMinOrderableDoseRateProperty, ctx)
                                decrRate = fun (ctx, n, uc) -> props.orderContextMsg (Api.DecreaseOrderableDoseRateProperty (n, uc), ctx)
                                setMedianRate = fun ctx -> props.orderContextMsg (Api.SetMedianOrderableDoseRateProperty, ctx)
                                incrRate = fun (ctx, n, uc) -> props.orderContextMsg (Api.IncreaseOrderableDoseRateProperty (n, uc), ctx)
                                setMaxRate = fun ctx -> props.orderContextMsg (Api.SetMaxOrderableDoseRateProperty, ctx)
                                // Dose Quantity
                                setMinDoseQty = fun ctx -> props.orderContextMsg (Api.SetMinOrderableDoseQuantityProperty, ctx)
                                decrDoseQty = fun (ctx, n, uc) -> props.orderContextMsg (Api.DecreaseOrderableDoseQuantityProperty (n, uc), ctx)
                                setMedianDoseQty = fun ctx -> props.orderContextMsg (Api.SetMedianOrderableDoseQuantityProperty, ctx)
                                incrDoseQty = fun (ctx, n, uc) -> props.orderContextMsg (Api.IncreaseOrderableDoseQuantityProperty (n, uc), ctx)
                                setMaxDoseQty = fun ctx -> props.orderContextMsg (Api.SetMaxOrderableDoseQuantityProperty, ctx)
                                // Component Quantity
                                setMinComponentQty = fun (ctx, cmp) -> props.orderContextMsg (Api.SetMinComponentOrderableQuantityProperty cmp, ctx)
                                decrComponentQty = fun (ctx, cmp, n, uc) -> props.orderContextMsg (Api.DecreaseComponentOrderableQuantityProperty (cmp, n, uc), ctx)
                                setMedianComponentQty = fun (ctx, cmp) -> props.orderContextMsg (Api.SetMedianComponentOrderableQuantityProperty cmp, ctx)
                                incrComponentQty = fun (ctx, cmp, n, uc) -> props.orderContextMsg (Api.IncreaseComponentOrderableQuantityProperty (cmp, n, uc), ctx)
                                setMaxComponentQty = fun (ctx, cmp) -> props.orderContextMsg (Api.SetMaxComponentOrderableQuantityProperty cmp, ctx)
                            |}
                            refreshOrderScenario = fun ctx -> props.orderContextMsg (Api.ResetOrderScenario, ctx)
                            closeOrder = handleModalClose
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>
        </div>
        """
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
    let View (props: {| appEnv: obj |}) =
        let envOrderContext = AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv
        let orderContext = envOrderContext.OrderContext
        let orderContextMsg = envOrderContext.OrderContextMsg
        let envTreatmentPlan = AppEnv.asEnv<AppEnv.ITreatmentPlan> props.appEnv
        let treatmentPlan = envTreatmentPlan.TreatmentPlan
        let treatmentPlanCommand = envTreatmentPlan.TreatmentPlanCommand

        let updateTreatmentPlan tp =
            treatmentPlanCommand (Api.UpdateOrderPlan(tp, None))

        let localizationTerms =
            (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:900px)"

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let loadingSource, setLoadingSource = React.useState<LoadingSource option> None

        React.useEffect (
            (fun () ->
                match orderContext with
                | Resolved _ -> setLoadingSource None
                | _ -> ()
            ),
            [| box orderContext |]
        )

        let updateOrderContext ctx =
            orderContextMsg (Api.UpdateOrderContext, ctx)

        let indicationChange s =
            match orderContext with
            | Resolved pr ->
                setLoadingSource (Some IndicationLoading)
                pr |> OrderContext.indicationChange s |> updateOrderContext
            | _ -> ()

        let medicationChange s =
            match orderContext with
            | Resolved pr ->
                setLoadingSource (Some MedicationLoading)
                pr |> OrderContext.medicationChange s |> updateOrderContext
            | _ -> ()

        let routeChange s =
            match orderContext with
            | Resolved pr ->
                setLoadingSource (Some RouteLoading)
                pr |> OrderContext.routeChange s |> updateOrderContext
            | _ -> ()

        let formChange s =
            match orderContext with
            | Resolved ctx ->
                setLoadingSource (Some FormLoading)
                ctx |> OrderContext.formChange s |> updateOrderContext
            | _ -> ()

        let diluentChange s =
            match orderContext with
            | Resolved pr ->
                setLoadingSource (Some DiluentLoading)
                pr |> OrderContext.diluentChange s |> updateOrderContext
            | _ -> ()

        let componentsChange cs =
            Logging.log "componentsChange" cs

            match orderContext with
            | Resolved prctx ->
                setLoadingSource (Some ComponentsLoading)
                prctx |> OrderContext.componentsChange cs |> updateOrderContext
            | _ -> ()

        let doseTypeChange s =
            let dt = s |> Option.map DoseType.doseTypeFromString

            match orderContext with
            | Resolved pr ->
                setLoadingSource (Some DoseTypeLoading)
                pr |> OrderContext.doseTypeChange dt |> updateOrderContext
            | _ -> ()

        let clear () =
            match orderContext with
            | Resolved _ ->
                setLoadingSource None
                OrderContext.empty |> updateOrderContext
            | _ -> ()

        let modalOpen, setModalOpen = React.useState false
        let handleModalClose = fun () -> setModalOpen false

        let isAnythingLoading =
            match orderContext with
            | InProgress
            | Recalculating _ -> true
            | _ -> false

        let isSourceLoading source =
            isAnythingLoading && loadingSource = Some source

        let select = ViewHelpers.filterSelect isAnythingLoading

        let multiSelect isLoading lbl selected dispatch xs =
            Components.MultipleSelect.View(
                {|
                    updateSelected = dispatch
                    label = lbl
                    selected = selected
                    values = xs
                    isLoading = isLoading
                    disabled = isAnythingLoading
                |}
            )

        let autoComplete = ViewHelpers.autoComplete isAnythingLoading

        let progress =
            match orderContext with
            | HasNotStartedYet -> JSX.jsx $"<>Voer eerst patient gegevens in</>"
            | _ -> null


        let displayScenario (pr: OrderContext) med (sc: OrderScenario) =
            if med |> Option.isNone then
                null
            else
                let caption =
                    let renal =
                        sc.RenalRule
                        |> Option.map (fun s -> $" (doseer aanpassing volgens {s})")
                        |> Option.defaultValue ""

                    $"{sc.Form}{renal}"

                let onClick (sc: OrderScenario) =
                    let ctx =
                        { pr with
                            Filter = { pr.Filter with Form = Some sc.Form }
                            Scenarios = [| sc |]
                        }

                    orderContextMsg (Api.SelectOrderScenario, ctx)

                let updateTreatmentPlan () =
                    match treatmentPlan with
                    | Resolved tp ->
                        { tp with Scenarios = [| sc |] |> Array.append tp.Scenarios }
                        |> updateTreatmentPlan
                    | _ -> ()

                let item key icon prim (sec: TextBlock[][]) =
                    let rows =
                        let cells row =
                            row
                            |> Array.mapi (fun i cell ->
                                JSX.jsx
                                    $"""
                                    <TableCell key={i} sx = { {|
                                                                  paddingTop = 1
                                                                  paddingRight = 2
                                                              |} }>
                                        {cell |> Mui.TypoGraphy.fromTextBlock}
                                    </TableCell>
                                    """
                            )

                        let sec = if not isMobile then sec else sec |> TextBlock.flatten

                        sec
                        |> Array.mapi (fun i row ->
                            JSX.jsx
                                $"""
                                <TableRow key={i} sx={ {| border = 0 |} } >
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
                        <TableContainer sx={ {| overflowX = "auto" |} } >
                            <Table padding="none" size="small" sx={ {|
                                                                        tableLayout = "auto"
                                                                        width = "auto"
                                                                    |} } >
                                <TableBody>
                                    <TableRow sx={ {|
                                                       border = 0
                                                       ``& td`` = {| borderBottom = 0 |}
                                                   |} } >
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
                        <Typography variant="h6" sx={ {|
                                                          backgroundColor = Mui.Styles.headerBgColor
                                                          padding = 1
                                                          borderRadius = 1
                                                      |} } >
                            {caption}
                        </Typography>
                        <List sx={ {|
                                       width = "100%"
                                       maxWidth = 1200
                                       bgcolor = Mui.Colors.Grey.``50``
                                   |} }>
                            {[|
                                 item "prescription" Mui.Icons.Notes (Terms.``Prescribe Prescription`` |> getTerm "Voorschrift") sc.Prescription
                                 if sc.Preparation |> Array.length > 0 then
                                     item "preparation" Mui.Icons.Vaccines (Terms.``Prescribe Preparation`` |> getTerm "Bereiding") sc.Preparation
                                 item
                                     "administration"
                                     Mui.Icons.MedicationLiquid
                                     (Terms.``Prescribe Administration`` |> getTerm "Toediening")
                                     sc.Administration
                             |]
                             |> unbox<seq<ReactElement>>
                             |> React.Fragment}
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

                <Box sx={ {| height = "100%" |} } >
                    <Card sx={ {| padding = 0 |} }>
                        <CardContent sx={ {| padding = 0 |} }>
                            {content}
                            {progress}
                        </CardContent>
                        <CardActions>
                            <Button
                                size="small"
                                disabled={isAnythingLoading}
                                onClick={fun () ->
                                             setModalOpen true
                                             onClick sc}
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

        let stackDirection = if isMobile then "column" else "row"

        let loadingIndicator = ViewHelpers.inlineProgress isAnythingLoading

        let cards =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';

            <React.Fragment>
                <Stack direction="column" spacing={if isMobile then 1 else 3}>
                    <Typography sx={ {| fontSize = 14 |} } color="text.secondary" >
                        {Terms.``Prescribe Scenarios`` |> getTerm "Medicatie scenario's"}
                    </Typography>
                    {match orderContext with
                     | Resolved pr
                     | Recalculating pr -> pr.Filter.Indication, pr.Filter.Indications
                     | _ -> None, [||]
                     |> fun (sel, items) ->
                         let isLoading = isSourceLoading IndicationLoading
                         let lbl = Terms.``Prescribe Indications`` |> getTerm "Indicaties"

                         if isMobile then
                             items |> Array.map (fun s -> s, s) |> select isLoading lbl sel indicationChange
                         else
                             items |> autoComplete isLoading lbl sel indicationChange}
                    <Stack direction={stackDirection} spacing={if isMobile then 1 else 3} >
                        {match orderContext with
                         | Resolved pr
                         | Recalculating pr -> pr.Filter.Generic, pr.Filter.Generics
                         | _ -> None, [||]
                         |> fun (sel, items) ->
                             let isLoading = isSourceLoading MedicationLoading
                             let lbl = Terms.``Prescribe Medications`` |> getTerm "Medicatie"

                             if isMobile then
                                 items |> Array.map (fun s -> s, s) |> select isLoading lbl sel medicationChange
                             else
                                 items |> autoComplete isLoading lbl sel medicationChange

                }
                        {match orderContext with
                         | Resolved pr
                         | Recalculating pr -> pr.Filter.Route, pr.Filter.Routes
                         | _ -> None, [||]
                         |> fun (sel, items) ->
                             let isLoading = isSourceLoading RouteLoading
                             let lbl = Terms.``Prescribe Routes`` |> getTerm "Routes"

                             if isMobile then
                                 items |> Array.map (fun s -> s, s) |> select isLoading lbl sel routeChange
                             else
                                 items |> autoComplete isLoading lbl sel routeChange

                }
                        {match orderContext with
                         | Resolved ctx
                         | Recalculating ctx when
                             ctx.Filter.Forms |> Array.length >= 1
                             && (not isMobile || ctx.Scenarios |> Array.length <> 1)
                             ->
                             ctx.Filter.Form, ctx.Filter.Forms
                         | _ -> None, [||]
                         |> fun (sel, items) ->
                             let isLoading = isSourceLoading FormLoading
                             let lbl = "Vorm"

                             if items |> Array.isEmpty then
                                 null
                             else if isMobile then
                                 items |> Array.map (fun s -> s, s) |> select isLoading lbl sel formChange
                             else
                                 items |> Array.map (fun s -> s, s) |> select isLoading lbl sel formChange}
                        {match orderContext with
                         | Resolved pr
                         | Recalculating pr when
                             pr.Filter.Indication.IsSome
                             && pr.Filter.Generic.IsSome
                             && pr.Filter.Route.IsSome
                             && pr.Filter.Diluents |> Array.length > 1
                             && pr.Scenarios |> Array.length = 1
                             ->

                             let isLoading = isSourceLoading DiluentLoading
                             let sel = pr.Filter.Diluent
                             let items = pr.Filter.Diluents
                             let lbl = "Verdunningsvorm"

                             items |> Array.map (fun s -> s, s) |> select isLoading lbl sel diluentChange

                         | _ -> null}
                        {match orderContext with
                         | Resolved pr
                         | Recalculating pr when
                             pr.Filter.Indication.IsSome
                             && pr.Filter.Generic.IsSome
                             && pr.Filter.Route.IsSome
                             && pr.Filter.Components |> Array.length > 1
                             && pr.Scenarios |> Array.length = 1
                             ->

                             let isLoading = isSourceLoading ComponentsLoading
                             let items = pr.Filter.Components
                             let lbl = "Componenten"

                             let sel =
                                 if pr.Filter.SelectedComponents |> Array.isEmpty then
                                     items
                                 else
                                     pr.Filter.SelectedComponents

                             items
                             |> Array.map (fun s -> s, s)
                             |> multiSelect isLoading lbl sel componentsChange

                         | _ -> null}
                        {match orderContext with
                         | Resolved pr
                         | Recalculating pr when
                             pr.Filter.Indication.IsSome
                             && pr.Filter.Generic.IsSome
                             && pr.Filter.Route.IsSome
                             ->
                             let isLoading = isSourceLoading DoseTypeLoading
                             let sel = pr.Filter.DoseType |> Option.map DoseType.doseTypeToString
                             let items = pr.Filter.DoseTypes
                             let lbl = "Doseer types"

                             items
                             |> Array.map (fun s -> s |> DoseType.doseTypeToString, s |> DoseType.doseTypeToDescription)
                             |> select isLoading lbl sel doseTypeChange

                         | _ -> null}
                    </Stack>
                    {loadingIndicator}
                    <Box sx={ {| marginTop = 2 |} }>
                        <Button variant="text" onClick={clear} disabled={isAnythingLoading} fullWidth startIcon={Mui.Icons.Delete} >
                            {Delete |> getTerm "Verwijder"}
                        </Button>
                    </Box>
                    <Stack direction="column" spacing={1} >
                        {match orderContext with
                         | Resolved pr
                         | Recalculating pr ->
                             pr.Scenarios
                             |> Array.map (displayScenario pr pr.Filter.Generic)
                             |> unbox<seq<ReactElement>>
                             |> React.Fragment
                         | _ -> Seq.empty<ReactElement> |> React.Fragment}
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
            <Box sx={ {|
                          height = "100%"
                          paddingBottom = (if isMobile then "16px" else "220px")
                      |} }>
                {cards}
                {progress}
            </Box>
            <Modal open={modalOpen} onClose={handleModalClose} >
                <Box sx={modalStyle}>
                    {Order.View
                         {|
                             orderContext = orderContext
                             updateOrderScenario = fun ctx -> orderContextMsg (Api.UpdateOrderScenario, ctx)
                             navigateOrderScenario =
                                 {|
                                     // Frequency
                                     setMinFrequency = fun ctx -> orderContextMsg (Api.SetMinScheduleFrequencyProperty, ctx)
                                     decrFrequency = fun ctx -> orderContextMsg (Api.DecreaseScheduleFrequencyProperty, ctx)
                                     setMedianFrequency = fun ctx -> orderContextMsg (Api.SetMedianScheduleFrequencyProperty, ctx)
                                     incrFrequency = fun ctx -> orderContextMsg (Api.IncreaseScheduleFrequencyProperty, ctx)
                                     setMaxFrequency = fun ctx -> orderContextMsg (Api.SetMaxScheduleFrequencyProperty, ctx)
                                     // Rate
                                     setMinRate = fun ctx -> orderContextMsg (Api.SetMinOrderableDoseRateProperty, ctx)
                                     decrRate = fun (ctx, n, uc) -> orderContextMsg (Api.DecreaseOrderableDoseRateProperty(n, uc), ctx)
                                     setMedianRate = fun ctx -> orderContextMsg (Api.SetMedianOrderableDoseRateProperty, ctx)
                                     incrRate = fun (ctx, n, uc) -> orderContextMsg (Api.IncreaseOrderableDoseRateProperty(n, uc), ctx)
                                     setMaxRate = fun ctx -> orderContextMsg (Api.SetMaxOrderableDoseRateProperty, ctx)
                                     // Dose Quantity
                                     setMinDoseQty = fun ctx -> orderContextMsg (Api.SetMinOrderableDoseQuantityProperty, ctx)
                                     decrDoseQty =
                                         fun (ctx, n, uc) -> orderContextMsg (Api.DecreaseOrderableDoseQuantityProperty(n, uc), ctx)
                                     setMedianDoseQty = fun ctx -> orderContextMsg (Api.SetMedianOrderableDoseQuantityProperty, ctx)
                                     incrDoseQty =
                                         fun (ctx, n, uc) -> orderContextMsg (Api.IncreaseOrderableDoseQuantityProperty(n, uc), ctx)
                                     setMaxDoseQty = fun ctx -> orderContextMsg (Api.SetMaxOrderableDoseQuantityProperty, ctx)
                                     // Component Quantity
                                     setMinComponentQty =
                                         fun (ctx, cmp) -> orderContextMsg (Api.SetMinComponentOrderableQuantityProperty cmp, ctx)
                                     decrComponentQty =
                                         fun (ctx, cmp, n, uc) ->
                                             orderContextMsg (Api.DecreaseComponentOrderableQuantityProperty(cmp, n, uc), ctx)
                                     setMedianComponentQty =
                                         fun (ctx, cmp) -> orderContextMsg (Api.SetMedianComponentOrderableQuantityProperty cmp, ctx)
                                     incrComponentQty =
                                         fun (ctx, cmp, n, uc) ->
                                             orderContextMsg (Api.IncreaseComponentOrderableQuantityProperty(cmp, n, uc), ctx)
                                     setMaxComponentQty =
                                         fun (ctx, cmp) -> orderContextMsg (Api.SetMaxComponentOrderableQuantityProperty cmp, ctx)
                                 |}
                             refreshOrderScenario = fun ctx -> orderContextMsg (Api.ResetOrderScenario, ctx)
                             closeOrder = handleModalClose
                             localizationTerms = localizationTerms
                         |}}
                </Box>
            </Modal>
        </div>
        """

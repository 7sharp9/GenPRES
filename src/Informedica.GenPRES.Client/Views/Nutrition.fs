namespace Views

module Nutrion =


    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models


    let private columns = [|
        {| field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |} |> box
        {| field = "substance"; headerName = "Component"; width = 200; filterable = true; sortable = true |} |> box
        {| field = "quantity"; headerName = "Hoeveelheid"; width = 150; filterable = false; sortable = false |} |> box
        {| field = "dose"; headerName = "Dosering"; width = 200; filterable = false; sortable = false |} |> box
    |]


    let private rowCreate (cells : string []) =
        {|
            id = cells[0]
            substance = cells[1]
            quantity = cells[2]
            dose = cells[3]
        |}
        |> box


    let private scenarioToRows (scenarios: OrderScenario []) =
        scenarios
        |> Array.collect (fun sc ->
            let o = sc.Order
            let qty = o.Orderable.OrderableQuantity.Variable |> Order.Variable.renderValue -1
            let rate = o.Orderable.Dose.Rate.Variable |> Order.Variable.renderValue -1

            let rows =
                o.Orderable.Components
                |> Array.mapi (fun ci c ->
                    let qty = c.OrderableQuantity.Variable |> Order.Variable.renderValue -1
                    let dose = c.Dose.QuantityAdjust.Variable |> Order.Variable.renderValue 1

                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{o.Id}_{ci}" |}
                                {| field = "substance"; value = c.Name |}
                                {| field = "quantity"; value = qty |}
                                {| field = "dose"; value = dose |}
                            |]
                        actions = None
                    |}
                )
            
            [|
                    {|
                        cells =
                            [|
                                {| field = "id"; value = $"{o.Id}" |}
                                {| field = "substance"; value = "Totaal" |}
                                {| field = "quantity"; value = qty |}
                                {| field = "dose"; value = rate |}
                            |]
                        actions = None
                    |}

            |] |> Array.append rows
        )


    [<JSX.Component>]
    let private NutritionSlot (props: {|
        nutritionContext: NutritionContext
        plan: NutritionPlan
        nutritionPlanMsg: Api.NutritionPlanCommand -> unit
        orderContextMsg: (Api.OrderContextCommand * OrderContext) -> unit
        localizationTerms: Deferred<string [][]>
    |}) =
        let ctx = props.nutritionContext.OrderContext
        let rows = scenarioToRows ctx.Scenarios

        let details =
            if rows |> Array.isEmpty |> not then
                Components.ResponsiveTable.View({|
                    hideFilter = true
                    columns = columns
                    rows = rows
                    rowCreate = rowCreate
                    height = "auto"
                    onRowClick = ignore
                    checkboxSelection = false
                    selectedRows = [||]
                    onSelectChange = ignore
                    showToolbar = false
                    showFooter = false
                |})
            else
                JSX.jsx $"<Typography variant=\"body2\" color=\"text.secondary\">Geen componenten</Typography>"

        JSX.jsx
            $"""
        import React from "react";
        import Box from '@mui/material/Box';
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import Typography from '@mui/material/Typography';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

        <Accordion>
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
                    orderContextMsg = props.orderContextMsg
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
            | _ -> JSX.jsx $"<></>"

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

namespace Components


module ResponsiveTable =

    open System
    open Fable.Core
    open Feliz
    open Fable.Core.JsInterop


    module private Cards =


        [<JSX.Component>]
        let CardTable (props :
            {|
                columns : {|  field : string; headerName : string; width : int; filterable : bool; sortable : bool |}[]
                rows : {| cells : {| field: string; value: string |} []; actions : ReactElement option |} []
                filter : ReactElement option
            |}) =

            let cards =
                props.rows
                |> Array.map (fun row ->
                    let content =
                        row.cells
                        |> Array.choose (fun cell ->
                            if cell.field = "id" || String.IsNullOrWhiteSpace(cell.value) then None
                            else Some cell
                        )
                        |> Array.mapi (fun i cell ->
                            let b, s =
                                match cell.value with
                                | _ when cell.value.Contains("**") -> Mui.Colors.Blue.``900``, cell.value.Replace("**", "")
                                | _ when cell.value.Contains("*") -> Mui.Colors.Blue.``900``, cell.value.Replace("*", "")
                                | _ -> Mui.Colors.Grey.``700``, cell.value

                            if i = 0 then
                                JSX.jsx
                                    $"""
                                import Typography from '@mui/material/Typography';
                                import Box from '@mui/material/Box';

                                <Box sx={ {| paddingY = 0.5; backgroundColor = Mui.Colors.Grey.``100``; marginX = -1.5; paddingX = 1.5 |} } >
                                    <Typography variant="subtitle2" color={b} sx={ {| fontWeight = 600; lineHeight = 1.4 |} } >
                                        {s}
                                    </Typography>
                                </Box>
                                """
                                |> toReact
                            else
                                let h =
                                    props.columns
                                    |> Array.tryFind (fun c -> c.field = cell.field)
                                    |> function
                                    | Some h -> $"{h.headerName.ToLower()}: "
                                    | None   -> $"{cell.field}: "

                                JSX.jsx
                                    $"""
                                import Stack from '@mui/material/Stack';
                                import Typography from '@mui/material/Typography';

                                <Stack direction="row" spacing={1} sx={ {| paddingY = 0.5 |} } >
                                    <Typography minWidth={80} variant="body2" color={Mui.Colors.Grey.``900``} sx={  {| lineHeight = 1.4 |}  } >
                                        {h}
                                    </Typography>
                                    <Typography color={b} variant="body2" sx={  {| lineHeight = 1.4 |}  } >
                                        {s}
                                    </Typography>
                                </Stack>
                                """
                                |> toReact
                        )

                    let hasActions = row.actions |> Option.isSome

                    let actions =
                        match row.actions with
                        | Some act ->
                            JSX.jsx
                                $"""
                            import CardActions from '@mui/material/CardActions';
                            <CardActions sx={ {| paddingTop = 0; paddingBottom = 0.5; paddingX = 1 |} } >
                                {act}
                            </CardActions>
                            """
                            |> toReact
                        | None -> JSX.jsx "<></>" |> toReact

                    let divider =
                        JSX.jsx
                            $"""
                        import Divider from '@mui/material/Divider';
                        <Divider sx={ {| borderColor = Mui.Colors.Grey.``300`` |} } />
                        """
                        |> toReact

                    let bottomPad = if hasActions then 0.5 else 1

                    JSX.jsx
                        $"""
                    import Card from '@mui/material/Card';
                    import CardContent from '@mui/material/CardContent';
                    import Stack from '@mui/material/Stack';

                    <Grid item sx={ {| width="100%"; mb = 0.5 |} } >
                        <Card raised={true} >
                            <CardContent sx={ {| paddingTop = 1; paddingBottom = bottomPad; paddingX = 1.5; ``&:last-child`` = {| paddingBottom = bottomPad |} |} } >
                                <Stack spacing={0} divider={divider} >
                                    {content}
                                </Stack>
                            </CardContent>
                            {actions}
                        </Card>
                    </Grid>
                    """
                )

            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            import Box from '@mui/material/Box';
            import Stack from '@mui/material/Stack';

            <Stack id="responsive-card-table" >
                <Box sx={ {| marginBottom=1.5 |} }>
                    {props.filter |> Option.defaultValue (JSX.jsx "<></>" |> toReact)}
                </Box>
                <Grid container rowSpacing={1} columnSpacing={ {| xs=1; sm=2; md=3 |} } >
                    {React.fragment (cards |> unbox)}
                </Grid>
            </Stack>
            """


    open Cards


    [<JSX.Component>]
    let View (props :
        {|
            hideFilter : bool
            columns : obj[]
            rows : {| cells : {| field: string; value: string |} []; actions : ReactElement option |} []
            rowCreate : string[] -> obj
            height : string
            onRowClick : string -> unit
            checkboxSelection : bool
            selectedRows : string []
            onSelectChange: string [] -> unit
            showToolbar : bool
            showFooter : bool
        |}) =
        let state, setState = React.useState [||]

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let columnFilter =
            if props.hideFilter then None
            else
                if props.rows |> Array.isEmpty then None
                else
                    props.columns
                    |> Array.choose (fun c ->
                        let col = unbox<{| field: string; headerName: string; width: int; filterable: bool; sortable: bool |}> c
                        if col.filterable then Some col else None
                    )
                    |> Array.tryHead

        let filter =
            columnFilter
            |> function
            | None   -> JSX.jsx "<></>"
            | Some column ->
                let data =
                    props.rows
                    |> Array.map (fun r -> r.cells)
                    |> Array.map (Array.filter (fun cell ->
                        cell.field = column.field
                    ))
                    |> Array.collect (Array.map _.value)
                    |> Array.distinct
                    |> Array.sortBy _.ToLower()

                MultipleSelect.View({|
                    label = "Filter"
                    selected = state
                    updateSelected = setState
                    values = data |> Array.map (fun s -> s, s)
                    isLoading = false
                |})
            |> toReact

        let onRowClick =
            fun pars ->
                pars?id |> string |> props.onRowClick

        let onSelectionChange =
            fun selectionModel ->
                Logging.log "selectionModel" selectionModel
                // selectionModel is now { type: string, ids: Set }
                // Extract the ids and convert to array
                let selectedIds = 
                    selectionModel?ids 
                    |> unbox<Set<string>>
                    |> Seq.toArray
                props.onSelectChange selectedIds

        // Return an alternating class name based on the row index within the current page
        let getRowClassName =
            fun (pars: obj) ->
                let idx: int = pars?indexRelativeToCurrentPage
                if idx % 2 = 0 then "even" else "odd"

        // Style for striped rows: apply background to even rows
        // Use lef border color blue to indicate selection
        let stripedSx: obj =
            createObj [
                "& .MuiDataGrid-row.even"
                ==> createObj [
                        "backgroundColor" ==> Mui.Colors.Grey.``100``
                    ]

                "& .MuiDataGrid-row"
                ==> createObj [
                    "cursor" ==> "pointer"
                    "transition" ==> "border-left 0.1s ease"
                ]
                
                "& .MuiDataGrid-row.even:hover"
                ==> createObj [
                    "backgroundColor" ==> Mui.Colors.Grey.``100``
                    "borderLeft" ==> $"4px solid {Mui.Colors.Blue.``700``}"
                ]

                "& .MuiDataGrid-row.odd:hover"
                ==> createObj [
                    "backgroundColor" ==> "white"
                    "borderLeft" ==> $"4px solid {Mui.Colors.Blue.``700``}"
                ]
                
                "& .MuiDataGrid-cell"
                ==> createObj [
                    "whiteSpace" ==> "normal"
                    "wordWrap" ==> "break-word"
                    "lineHeight" ==> "1.5"
                    "paddingTop" ==> "8px"
                    "paddingBottom" ==> "8px"
                ]
            ]

        let rows =
            props.rows
            |> Array.filter (fun r ->
                match columnFilter with
                | None -> true
                | Some column ->
                    r.cells
                    |> Array.exists (fun cell ->
                        cell.field = column.field &&
                        (state |> Array.isEmpty || state |> Array.exists ((=) cell.value))
                    )
            )

        if isMobile then
            let typedColumns =
                props.columns
                |> Array.map unbox<{| field: string; headerName: string; width: int; filterable: bool; sortable: bool |}>
            {| columns = typedColumns; rows = rows; filter = Some filter |}
            |> CardTable
        else
            let rows =
                rows
                |> Array.map (fun r -> r.cells)
                |> Array.map (Array.map (fun r -> r.value))
                |> Array.map props.rowCreate

            let toolbar () =
                if props.showToolbar then
                    JSX.jsx
                        $"""
                    import {{ GridToolbar }} from '@mui/x-data-grid';

                    <GridToolbar printOptions = { {| hideFooter=true; hideToolbar=true |} } />
                    """
                    |> toReact
                else
                    JSX.jsx "<></>" |> toReact

            let selectedRows =
                props.selectedRows
                |> fun ids ->
                    {| ``type`` = "include"; ids = ids |> Set.ofArray |}

            let slots =
                if props.showToolbar then
                    createObj [ "toolbar" ==> toolbar ]
                else
                    createObj []

            JSX.jsx
                $"""
            import {{ DataGrid }} from '@mui/x-data-grid';

            <Box>
                <Box sx={ {| marginBottom=3 |} }>
                    {filter}
                </Box>
                <div style={ {| height =props.height; width = "100%" |} }>
                    <DataGrid
                        sx={stripedSx}
                        showToolbar={props.showToolbar}
                        checkboxSelection={props.checkboxSelection}
                        disableRowSelectionOnClick
                        rowSelectionModel = {selectedRows}
                        onRowSelectionModelChange = {onSelectionChange}
                        getRowClassName={getRowClassName}
                        
                        rows={rows}
                        slots={slots}
                        onRowClick={onRowClick}
                        hideFooter={not props.showFooter}
                        initialState =
                            {
                                {| 
                                    columns = {| columnVisibilityModel = {| id = false |} |} 
                                    density = "comfortable"
                                |}
                            }
                        columns=
                            {
                                props.columns
                                |> Array.map (fun c ->
                                    // Try to get the field property if it exists as a simple column
                                    try
                                        let simpleCol = unbox<{| field: string |} > c
                                        match simpleCol.field with
                                        | "id" ->
                                            createObj [
                                                "field" ==> "id"
                                                "hide" ==> true
                                            ]
                                        | _ -> c
                                    with _ -> c // Already an object with custom properties
                                )
                            }
                    />
                    </div>
            </Box>
            """


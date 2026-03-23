namespace Views


module Interactions =


    open Fable.Core
    open Fable.Core.JsInterop
    open Fable.React
    open Feliz
    open Elmish
    open Shared
    open Shared.Types


    module private Elmish =


        type State =
            {
                DrugInput: string
                Drugs: string list
            }


        type Msg =
            | UpdateDrugInput of string
            | AddDrug
            | RemoveDrug of string
            | ImportFromTreatmentPlan
            | ClearDrugs


        let init () =
            {
                DrugInput = ""
                Drugs = []
            },
            Cmd.none


        let update
            (treatmentPlan: Deferred<OrderPlan>)
            (checkInteractions: string list -> unit)
            (msg: Msg)
            (state: State)
            : State * Cmd<Msg>
            =
            match msg with
            | UpdateDrugInput s -> { state with DrugInput = s }, Cmd.none

            | AddDrug ->
                let drug = state.DrugInput.Trim()

                if drug = "" || state.Drugs |> List.contains drug then
                    { state with DrugInput = "" }, Cmd.none
                else
                    { state with
                        DrugInput = ""
                        Drugs = state.Drugs @ [ drug ]
                    },
                    Cmd.none

            | RemoveDrug drug -> { state with Drugs = state.Drugs |> List.filter (fun d -> d <> drug) }, Cmd.none

            | ImportFromTreatmentPlan ->
                let drugs =
                    match treatmentPlan with
                    | Resolved tp -> tp.Scenarios |> Array.map _.Name |> Array.distinct |> Array.toList
                    | _ -> []

                let newState = { state with Drugs = drugs }
                checkInteractions newState.Drugs
                newState, Cmd.none

            | ClearDrugs ->
                { state with
                    Drugs = []
                    DrugInput = ""
                },
                Cmd.none


    open Elmish


    [<JSX.Component>]
    let private InteractionRow
        (props:
            {|
                index: int
                interaction: DrugInteraction
            |})
        =
        let cls1, cls2 = props.interaction.Name

        JSX.jsx
            $"""
        <TableRow key={props.index}>
            <TableCell>{props.interaction.Drug1}</TableCell>
            <TableCell>{props.interaction.Drug2}</TableCell>
            <TableCell>{$"%s{cls1} / %s{cls2}"}</TableCell>
        </TableRow>
        """


    [<JSX.Component>]
    let private DrugChip
        (props:
            {|
                drug: string
                onDelete: unit -> unit
            |})
        =
        JSX.jsx
            $"""
        <Chip
            label={props.drug}
            onDelete={fun _ -> props.onDelete ()}
        />
        """


    [<JSX.Component>]
    let private ImportButton
        (props:
            {|
                label: string
                onClick: unit -> unit
            |})
        =
        JSX.jsx
            $"""
        <Button
            variant="outlined"
            onClick={fun _ -> props.onClick ()}
            startIcon={Mui.Icons.SummarizeIcon}
        >
            {props.label}
        </Button>
        """


    [<JSX.Component>]
    let View
        (props:
            {|
                interactions: Deferred<DrugInteraction[]>
                checkInteractions: string list -> unit
                treatmentPlan: Deferred<OrderPlan>
                localizationTerms: Deferred<string[][]>
            |})
        =

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let state, dispatch =
            React.useElmish (init (), update props.treatmentPlan props.checkInteractions, [| box props.treatmentPlan |])

        let hasTreatmentPlan =
            match props.treatmentPlan with
            | Resolved tp -> tp.Scenarios.Length > 0
            | _ -> false

        let interactionRows =
            match props.interactions with
            | Resolved interactions -> interactions
            | _ -> [||]

        let isLoading =
            match props.interactions with
            | InProgress -> true
            | _ -> false

        let hasChecked =
            match props.interactions with
            | Resolved _ -> true
            | _ -> false

        let importButton =
            if hasTreatmentPlan then
                ImportButton
                    {|
                        label = Terms.``Treatment Plan`` |> getTerm "Behandelplan"
                        onClick = fun () -> ImportFromTreatmentPlan |> dispatch
                    |}
            else
                null

        let chipList =
            state.Drugs
            |> List.toArray
            |> Array.map (fun drug ->
                DrugChip
                    {|
                        drug = drug
                        onDelete = fun () -> RemoveDrug drug |> dispatch
                    |}
            )

        let interactionTable =
            if interactionRows.Length > 0 then
                let rows =
                    interactionRows
                    |> Array.mapi (fun i interaction ->
                        InteractionRow
                            {|
                                index = i
                                interaction = interaction
                            |}
                    )

                JSX.jsx
                    $"""
                <TableContainer>
                    <Table size="small">
                        <TableHead>
                            <TableRow>
                                <TableCell>{"Medicatie 1"}</TableCell>
                                <TableCell>{"Medicatie 2"}</TableCell>
                                <TableCell>{"Interactie klasse"}</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {rows}
                        </TableBody>
                    </Table>
                </TableContainer>
                """
            elif hasChecked && not isLoading then
                JSX.jsx
                    $"""
                <Alert severity="success">{"Geen interacties gevonden"}</Alert>
                """
            else
                null

        let loadingIndicator =
            if isLoading then
                JSX.jsx $"""<CircularProgress />"""
            else
                null

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardContent from '@mui/material/CardContent';
        import Typography from '@mui/material/Typography';
        import Stack from '@mui/material/Stack';
        import TextField from '@mui/material/TextField';
        import Button from '@mui/material/Button';
        import Chip from '@mui/material/Chip';
        import Table from '@mui/material/Table';
        import TableBody from '@mui/material/TableBody';
        import TableCell from '@mui/material/TableCell';
        import TableContainer from '@mui/material/TableContainer';
        import TableHead from '@mui/material/TableHead';
        import TableRow from '@mui/material/TableRow';
        import Paper from '@mui/material/Paper';
        import CircularProgress from '@mui/material/CircularProgress';
        import Alert from '@mui/material/Alert';
        import Divider from '@mui/material/Divider';

        <Box sx={ {| overflowY = "auto" |} }>
            <Card>
                <CardContent>
                    <Stack direction="column" spacing={3}>

                        <Typography sx={ {| fontSize = 14 |} } color="text.secondary" gutterBottom>
                            {Terms.``Interactions`` |> getTerm "Interacties"}
                        </Typography>

                        <Stack direction="row" spacing={2}>
                            {importButton}
                        </Stack>

                        <Stack direction="row" spacing={1} alignItems="center">
                            <TextField
                                label="Medicatie"
                                variant="outlined"
                                size="small"
                                value={state.DrugInput}
                                onChange={fun (e: Browser.Types.Event) -> (e.target?value: string) |> UpdateDrugInput |> dispatch}
                                onKeyDown={fun (e: Browser.Types.KeyboardEvent) ->
                                               if e.key = "Enter" then
                                                   e.preventDefault ()
                                                   AddDrug |> dispatch}
                            />
                            <Button
                                variant="contained"
                                onClick={fun _ -> AddDrug |> dispatch}
                                disabled={state.DrugInput.Trim() = ""}
                            >
                                +
                            </Button>
                        </Stack>

                        <Stack direction="row" spacing={1} sx={ {|
                                                                    flexWrap = "wrap"
                                                                    gap = 1
                                                                |} }>
                            {chipList}
                        </Stack>

                        <Stack direction="row" spacing={2}>
                            <Button
                                variant="contained"
                                onClick={fun _ -> props.checkInteractions state.Drugs}
                                disabled={state.Drugs.Length < 2 || isLoading}
                            >
                                {"Check"}
                            </Button>
                            <Button
                                variant="text"
                                onClick={fun _ -> ClearDrugs |> dispatch}
                                startIcon={Mui.Icons.Delete}
                            >
                                {Terms.``Delete`` |> getTerm "Verwijder"}
                            </Button>
                        </Stack>

                        {loadingIndicator}

                        <Divider />

                        {interactionTable}
                    </Stack>
                </CardContent>
            </Card>
        </Box>
        """

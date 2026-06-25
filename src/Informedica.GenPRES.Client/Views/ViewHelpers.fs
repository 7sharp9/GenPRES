namespace Views

#nowarn "1104"

module ViewHelpers =

    open System
    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models.Order


    let filterSelect disabled isLoading lbl selected dispatch xs =
        let isEmpty = xs |> Array.isEmpty

        Components.SimpleSelect.View(
            {|
                updateSelected = if isEmpty then ignore else dispatch
                label = lbl
                selected = if isEmpty then None else selected
                values = xs
                isLoading = isLoading
                disabled = disabled || isEmpty
                hasClear = true
                navigate = None
                warning = None
                minWidth = None
            |}
        )


    let getWarning warning =
        match warning with
        | IsNormal -> None
        | IsCaution -> Some Mui.Colors.Blue.``600``
        | IsWarning -> Some Mui.Colors.Orange.``700``
        | IsAlert -> Some Mui.Colors.Red.``700``


    let orderSelect alwaysShow disabled isLoading lbl selected updateSelected navigate hasClear warning minWidth xs =

        if not alwaysShow && xs |> Array.isEmpty && navigate |> Option.isNone then
            null
        else
            let isEmpty = xs |> Array.isEmpty && navigate |> Option.isNone

            Components.SimpleSelect.View(
                {|
                    updateSelected = if isEmpty then ignore else updateSelected
                    label = lbl
                    selected =
                        if xs |> Array.length = 1 then
                            xs[0] |> fst |> Some
                        else
                            selected
                    values = xs
                    isLoading = isLoading
                    disabled = disabled || isEmpty
                    hasClear = hasClear
                    warning = warning
                    navigate = navigate
                    minWidth = minWidth
                |}
            )


    let createNav
        dispatch
        navigable
        solved
        setMin
        (decr: int * bool -> 'Msg)
        setMed
        (incr: int * bool -> 'Msg)
        setMax
        step
        =
        {|
            step = step
            first =
                if navigable then
                    (fun (_: int) -> setMin |> dispatch) |> Some
                elif solved then
                    (fun n -> (n, true) |> decr |> dispatch) |> Some
                else
                    None
            decrease =
                if solved then
                    (fun n -> (n, false) |> decr |> dispatch) |> Some
                else
                    None
            median =
                if navigable then
                    (fun () -> setMed |> dispatch) |> Some
                else
                    None
            increase =
                if solved then
                    (fun n -> (n, false) |> incr |> dispatch) |> Some
                else
                    None
            last =
                if navigable then
                    (fun (_: int) -> setMax |> dispatch) |> Some
                elif solved then
                    (fun n -> (n, true) |> incr |> dispatch) |> Some
                else
                    None
            useDebounce = not navigable && solved
        |}
        |> Some


    let ovarLabel (name: string) (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (fun v -> $"{name} ({v.Unit})")
        |> Option.defaultValue name


    let ovarVals (format: decimal -> string) (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> format} {v.Unit}"))
        |> Option.defaultValue [||]


    let ovarValsWithRange (format: decimal -> string) (prec: int) (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> format} {v.Unit}"))
        |> Option.defaultValue (
            match Variable.renderValue prec ovar.Variable with
            | "" -> [||]
            | s -> [| "range", s |]
        )


    /// Build a per-click step function for a solved order variable. Given the net inner
    /// click delta (single-step buttons, using the DEFINED increment) and the net outer
    /// click delta (first/last buttons, using the server's CALCULATED increment) it returns
    /// the (key, label) of the moved value, clamped to the defined range. The increments
    /// mirror the server (Informedica.GenORDER.Lib.OrderVariable.step): inner uses the
    /// defined increment, outer follows the server's calculated increment. Used to show
    /// optimistic feedback that follows the live click count (the badge) before the server
    /// confirms. Returns None when no increment is available (cannot step locally).
    let ovarStep (format: decimal -> string) (ovar: OrderVariable) : (int * int -> string * string) option =
        let firstSnd (vu: Types.ValueUnit) =
            vu.Value |> Array.tryHead |> Option.map snd

        let definedIncr =
            [ ovar.DefinedConstraints.Incr; ovar.Variable.Incr ]
            |> List.tryPick (Option.bind firstSnd)

        // The outer (first/last) buttons follow the server-provided effective increment
        // (OuterIncr); when the server emits none, the outer step falls back to the
        // defined increment (i.e. behaves like the inner buttons).
        let outerIncr = ovar.OuterIncr |> Option.bind firstSnd

        match ovar.Variable.Vals, definedIncr with
        | Some vals, Some di when di > 0M ->
            match firstSnd vals with
            | Some cur ->
                let unit = vals.Unit

                // Clamp against the DEFINED constraint range (the real allowed bounds),
                // NOT the solved Variable's own Min/Max — for a solved variable those
                // collapse to the single value, which would pin every step back to it.
                let clamp v =
                    let v =
                        ovar.DefinedConstraints.Max
                        |> Option.bind firstSnd
                        |> Option.map (min v)
                        |> Option.defaultValue v

                    ovar.DefinedConstraints.Min
                    |> Option.bind firstSnd
                    |> Option.map (max v)
                    |> Option.defaultValue v

                let ci = outerIncr |> Option.defaultValue di

                (fun (innerDelta, outerDelta) ->
                    let next = (cur + decimal innerDelta * di + decimal outerDelta * ci) |> clamp
                    string next, $"{format next} {unit}"
                )
                |> Some
            | None -> None
        | _ -> None


    let ovarDisplay select (name: string) (format: decimal -> string) minWidth (ovar: OrderVariable) =
        let warning = ovar.Level |> getWarning
        let label = ovar |> ovarLabel name
        let vals = ovar |> ovarVals format
        select false label None ignore None false warning minWidth vals


    let autoComplete disabled isLoading lbl selected dispatch xs =
        let isEmpty = xs |> Array.isEmpty

        Components.Autocomplete.View(
            {|
                updateSelected = if isEmpty then ignore else dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
                disabled = disabled || isEmpty
            |}
        )


    let inlineProgress isLoading =
        if isLoading then
            let progressSx =
                {|
                    display = "flex"
                    justifyContent = "center"
                    padding = 2
                |}

            JSX.jsx
                $"""
            import CircularProgress from '@mui/material/CircularProgress';
            import Box from '@mui/material/Box';
            <Box sx={progressSx}>
                <CircularProgress size={24} />
            </Box>
            """
        else
            null


    let circularProgress =
        let circularProgressSx =
            {|
                marginTop = 5
                display = "flex"
                padding = 20
            |}

        JSX.jsx
            $"""
        import CircularProgress from '@mui/material/CircularProgress';
        import Box from '@mui/material/Box';
        <Box sx={circularProgressSx}>
            <CircularProgress />
        </Box>
        """


    let progressOrEmpty (deferred: Deferred<'a>) =
        match deferred with
        | Resolved _
        | Recalculating _ -> null
        | _ -> circularProgress


    let backdropProgress isOpen (message: string) =
        let backdropBoxSx =
            {|
                display = "flex"
                flexDirection = "column"
                alignItems = "center"
                gap = 2
            |}

        let backdropSx =
            {|
                color = "#fff"
                zIndex = 9999
            |}

        JSX.jsx
            $"""
        import Backdrop from '@mui/material/Backdrop';
        import CircularProgress from '@mui/material/CircularProgress';
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Backdrop
            sx={backdropSx}
            open={isOpen}>
            <Box sx={backdropBoxSx}>
                <CircularProgress color="inherit" />
                <Typography variant="h6" color="inherit">
                    {message}
                </Typography>
            </Box>
        </Backdrop>
        """


    let modalStyle =
        {|
            position = "absolute"
            top = "50%"
            left = "50%"
            transform = "translate(-50%, -50%)"
            width = "90vw"
            maxWidth = 500
            maxHeight = "90vh"
            overflowY = "auto"
            overflowX = "hidden"
            bgcolor = "background.paper"
            boxShadow = 24
            borderRadius = "16px"
        |}


    module PrintView =


        let appBarSx = {| ``@media print`` = {| display = "none" |} |}


        let printSx =
            {|
                padding = 3
                ``@media print`` = {| padding = 1 |}
            |}


        let headerCellSx =
            {|
                fontWeight = "bold"
                borderBottom = "none"
                paddingY = "2px"
                width = "25%"
            |}


        let valueCellSx =
            {|
                borderBottom = "1px dotted #ccc"
                paddingY = "2px"
                width = "25%"
            |}


        let patientWeight (patient: Patient option) =
            patient
            |> Option.bind Models.Patient.getWeightInKg
            |> Option.map (fun w ->
                let s = decimal w |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 1
                s + " kg"
            )
            |> Option.defaultValue "onbekend"


        [<JSX.Component>]
        let PatientHeader (props: {| weightKg: string |}) =
            let currentDate =
                let dt = DateTime.Now
                let pad (n: int) = if n < 10 then $"0{n}" else $"{n}"
                $"{pad dt.Day} - {pad dt.Month} - {dt.Year}"

            let patientTableSx =
                {|
                    tableLayout = "fixed"
                    width = "100%"
                    marginBottom = 3
                |}

            JSX.jsx
                $"""
            import Table from '@mui/material/Table';
            import TableBody from '@mui/material/TableBody';
            import TableRow from '@mui/material/TableRow';
            import TableCell from '@mui/material/TableCell';

            <Table size="small" sx={patientTableSx}>
                <TableBody>
                    <TableRow>
                        <TableCell sx={headerCellSx}>D.D.</TableCell>
                        <TableCell sx={valueCellSx}>{currentDate}</TableCell>
                        <TableCell sx={headerCellSx}>Patientnummer</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Afdeling</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>Naam</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Bed</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>Geboorte datum</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Arts</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>Gewicht</TableCell>
                        <TableCell sx={valueCellSx}>{props.weightKg}</TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Zoemer</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}></TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                </TableBody>
            </Table>
            """


        [<JSX.Component>]
        let PatientSignature () =
            let signatureSx =
                {|
                    marginTop = 4
                    borderTop = "1px solid #ccc"
                    paddingTop = 2
                |}

            JSX.jsx
                $"""
            import Box from '@mui/material/Box';
            import Typography from '@mui/material/Typography';

            <Box sx={signatureSx}>
                <Typography variant="body2">Paraaf arts:</Typography>
            </Box>
            """


        [<JSX.Component>]
        let PrintDialog
            (props:
                {|
                    isOpen: bool
                    onClose: unit -> unit
                    title: string
                    children: ReactElement
                |})
            =
            let handlePrint = fun _ -> Browser.Dom.window.print ()

            let isOpen = props.isOpen

            let titleSx =
                {|
                    marginLeft = 2
                    flex = 1
                |}

            JSX.jsx
                $"""
            import Dialog from '@mui/material/Dialog';
            import AppBar from '@mui/material/AppBar';
            import Toolbar from '@mui/material/Toolbar';
            import IconButton from '@mui/material/IconButton';
            import Button from '@mui/material/Button';
            import Typography from '@mui/material/Typography';
            import Box from '@mui/material/Box';
            import CloseIcon from '@mui/icons-material/Close';
            import PrintIcon from '@mui/icons-material/Print';

            <Dialog fullScreen open={isOpen} onClose={fun _ -> props.onClose ()}>
                    <AppBar sx={appBarSx} position="static">
                        <Toolbar>
                            <IconButton edge="start" color="inherit" onClick={fun _ -> props.onClose ()} aria-label="close">
                                <CloseIcon />
                            </IconButton>
                            <Typography sx={titleSx} variant="h6" component="div">
                                {props.title}
                            </Typography>
                            <Button color="inherit" onClick={handlePrint} startIcon={{ <PrintIcon /> }}>
                                Print
                            </Button>
                        </Toolbar>
                    </AppBar>
                    <Box sx={printSx}>
                        {props.children}
                    </Box>
                </Dialog>
                """

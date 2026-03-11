namespace Views


module ViewHelpers =

    open Fable.Core
    open Shared
    open Shared.Types


    let simpleSelect isLoading lbl selected dispatch xs =
        Components.SimpleSelect.View({|
            updateSelected = dispatch
            label = lbl
            selected = selected
            values = xs
            isLoading = isLoading
            hasClear = true
            navigate = None
            warning = None
            minWidth = None
        |})


    let getWarning warning =
        match warning with
        | IsNormal -> None
        | IsCaution -> Some Mui.Colors.Blue.``600``
        | IsWarning -> Some Mui.Colors.Orange.``700``
        | IsAlert -> Some Mui.Colors.Red.``700``


    let empty = JSX.jsx $"<></>"


    let orderSelect
        isLoading
        lbl
        selected
        updateSelected
        navigate
        hasClear
        warning
        minWidth
        xs =

        if xs |> Array.isEmpty && navigate |> Option.isNone then
            empty
        else
            Components.SimpleSelect.View({|
                updateSelected = updateSelected
                label = lbl
                selected =
                    if xs |> Array.length = 1 then xs[0] |> fst |> Some
                    else selected
                values = xs
                isLoading = isLoading
                hasClear = hasClear
                warning = warning
                navigate = navigate
                minWidth = minWidth
            |})


    let createNav dispatch navigable solved
        setMin
        (decr : int * bool -> 'Msg)
        setMed
        (incr : int * bool -> 'Msg)
        setMax =
        {|
            first =
                if navigable then (fun (_: int) -> setMin |> dispatch) |> Some
                elif solved then (fun n -> (n, true) |> decr |> dispatch) |> Some
                else None
            decrease =
                if solved then (fun n -> (n, false) |> decr |> dispatch) |> Some
                else None
            median =
                if navigable then (fun () -> setMed |> dispatch) |> Some
                else None
            increase =
                if solved then (fun n -> (n, false) |> incr |> dispatch) |> Some
                else None
            last =
                if navigable then (fun (_: int) -> setMax |> dispatch) |> Some
                elif solved then (fun n -> (n, true) |> incr |> dispatch) |> Some
                else None
            useDebounce = not navigable && solved
        |}
        |> Some


    let autoComplete isLoading lbl selected dispatch xs =
        Components.Autocomplete.View({|
            updateSelected = dispatch
            label = lbl
            selected = selected
            values = xs
            isLoading = isLoading
        |})


    let circularProgress =
        JSX.jsx
            $"""
        import CircularProgress from '@mui/material/CircularProgress';
        import Box from '@mui/material/Box';
        <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
            <CircularProgress />
        </Box>
        """


    let progressOrEmpty (deferred: Deferred<'a>) =
        match deferred with
        | Resolved _ -> empty
        | _ -> circularProgress


    let modalStyle =
        {|
            position="absolute"
            top= "50%"
            left= "50%"
            transform= "translate(-50%, -50%)"
            width= "90vw"
            maxWidth= 500
            bgcolor= "background.paper"
            boxShadow= 24
            borderRadius = "16px"
        |}

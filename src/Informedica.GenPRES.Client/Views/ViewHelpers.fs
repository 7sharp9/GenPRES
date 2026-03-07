namespace Views


module ViewHelpers =

    open Fable.Core
    open Shared


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
        |})


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
        | Resolved _ -> JSX.jsx $"<></>"
        | _ -> circularProgress


    let modalStyle =
        {|
            position="absolute"
            top= "50%"
            left= "50%"
            transform= "translate(-50%, -50%)"
            width= 400
            bgcolor= "background.paper"
            boxShadow= 24
            borderRadius = "16px"
        |}

namespace Components


module SimpleSelect =


    open System
    open Fable.Core
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View (props :
            {|
                label : string
                selected : string option
                values : (string * string) []
                updateSelected : string option -> unit
                navigate : {| 
                    hasIncr: bool
                    hasDecr: bool
                    hasMedian: bool
                    first : unit -> unit
                    decrease : unit -> unit
                    median : unit -> unit 
                    increase : unit -> unit
                    last : unit -> unit
                |} option
                isLoading : bool
                hasClear : bool
            |}
        ) =

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> function
                | s when s |> String.IsNullOrWhiteSpace -> None
                | s -> s |> Some
                |> props.updateSelected

        let clear = fun _ -> None |> props.updateSelected

        let items =
            props.values
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx = { {| maxWidth = 400 |} }>
                    {v}
                </MenuItem>
                """
            )

        let isClear = props.selected |> Option.defaultValue "" |> String.IsNullOrWhiteSpace

        let clearButton =
            match props.isLoading, isClear with
            | true, _      -> Mui.Icons.Downloading
            | false, true  -> JSX.jsx "<></>"
            | false, false ->
                JSX.jsx
                    $"""
                import ClearIcon from '@mui/icons-material/Clear';
                import IconButton from "@mui/material/IconButton";

                <IconButton onClick={clear}>
                    {Mui.Icons.Clear}
                </IconButton>
                """

        let navigationSx = {|
            display = "flex"
            felxDirection = "column"
            alignItems = "center"
        |}

        let navigation = 
            props.navigate
            |> Option.map (fun nav ->
                JSX.jsx
                    $"""
                import IconButton from "@mui/material/IconButton";
                import ButtonGroup from '@mui/material/ButtonGroup';
                import Box from '@mui/material/Box';
                <Box
                sx={navigationSx}
                >
                <ButtonGroup variant="text" aria-label="navigation button group">
                    <IconButton onClick={fun _ -> nav.first ()} >{Mui.Icons.FirstPageIcon}</IconButton>
                    <IconButton disabled={not nav.hasDecr} onClick={fun _ -> nav.decrease () } >{Mui.Icons.SkipPreviousIcon}</IconButton>
                    <IconButton disabled={not nav.hasMedian} onClick={fun _ -> nav.median ()} >{Mui.Icons.PauseIcon}</IconButton>
                    <IconButton disabled={not nav.hasIncr} onClick={fun _ -> nav.increase ()} >{Mui.Icons.SkipNextIcon}</IconButton>
                    <IconButton onClick={fun _ -> nav.last ()} >{Mui.Icons.LastPageIcon}</IconButton>
                </ButtonGroup>
                </Box>            
                """
            )

        let endAdornment = 
            if navigation.IsNone && not isClear && props.hasClear then Some clearButton
            else
                navigation

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';

        <FormControl variant="standard" sx={ {| minWidth = 150; maxWidth = 400 |} }>
            <InputLabel id={props.label}>{props.label}</InputLabel>
            <Select
            labelId={props.label}
            id={props.label}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            endAdornment={endAdornment}
            sx=
                {
                    {| ``& .MuiSelect-icon`` =
                        {|
                            visibility = 
                                if endAdornment.IsNone then "visible" 
                                else "hidden"
                        |}
                    |}
                }
            >
                {items}
            </Select>
        </FormControl>
        """


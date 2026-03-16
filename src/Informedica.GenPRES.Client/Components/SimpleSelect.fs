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
                    first : (int -> unit) option
                    decrease : (int -> unit) option
                    median : (unit -> unit) option
                    increase : (int -> unit) option
                    last : (int -> unit) option
                    useDebounce : bool
                |} option
                isLoading : bool
                disabled : bool
                hasClear : bool
                warning : string option
                minWidth : int option
            |}
        ) =

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let menuProps =
            if isMobile then
                {| PaperProps = {| style = {| maxHeight = 400 |} |} |} |> box |> Some
            else
                None

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
                <MenuItem key={i} value={k} sx = { {| maxWidth = 400; paddingY = (if isMobile then 0.25 else 0.75) |} } dense={isMobile} >
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
                let getNav prop =
                    match prop with
                    | Some onClick -> false, onClick
                    | None -> true, fun () -> ()

                let getNavN prop =
                    match prop with
                    | Some onClick -> false, onClick
                    | None -> true, fun (_: int) -> ()

                let firstDisabled, firstClick = nav.first |> getNavN
                let decreaseDisabled, decreaseClick = nav.decrease |> getNavN
                let increaseDisabled, increaseClick = nav.increase |> getNavN
                let lastDisabled, lastClick = nav.last |> getNavN

                let firstButton =
                    if nav.useDebounce then
                        ClickCountingButton.View({|
                            disabled = firstDisabled
                            onClick = firstClick
                            icon = Mui.Icons.FirstPageIcon
                        |})
                    else
                        JSX.jsx
                            $"""
                        import IconButton from "@mui/material/IconButton";
                        <IconButton disabled={firstDisabled} onClick={fun _ -> firstClick 1} >{Mui.Icons.FirstPageIcon}</IconButton>
                        """

                let decreaseButton =
                    if nav.useDebounce then
                        ClickCountingButton.View({|
                            disabled = decreaseDisabled
                            onClick = decreaseClick
                            icon = Mui.Icons.SkipPreviousIcon
                        |})
                    else
                        JSX.jsx
                            $"""
                        import IconButton from "@mui/material/IconButton";
                        <IconButton disabled={decreaseDisabled} onClick={fun _ -> decreaseClick 1} >{Mui.Icons.SkipPreviousIcon}</IconButton>
                        """

                let increaseButton =
                    if nav.useDebounce then
                        ClickCountingButton.View({|
                            disabled = increaseDisabled
                            onClick = increaseClick
                            icon = Mui.Icons.SkipNextIcon
                        |})
                    else
                        JSX.jsx
                            $"""
                        import IconButton from "@mui/material/IconButton";
                        <IconButton disabled={increaseDisabled} onClick={fun _ -> increaseClick 1} >{Mui.Icons.SkipNextIcon}</IconButton>
                        """

                let lastButton =
                    if nav.useDebounce then
                        ClickCountingButton.View({|
                            disabled = lastDisabled
                            onClick = lastClick
                            icon = Mui.Icons.LastPageIcon
                        |})
                    else
                        JSX.jsx
                            $"""
                        import IconButton from "@mui/material/IconButton";
                        <IconButton disabled={lastDisabled} onClick={fun _ -> lastClick 1} >{Mui.Icons.LastPageIcon}</IconButton>
                        """

                JSX.jsx
                    $"""
                import IconButton from "@mui/material/IconButton";
                import ButtonGroup from '@mui/material/ButtonGroup';
                import Box from '@mui/material/Box';
                <Box
                sx={navigationSx}
                >
                <ButtonGroup variant="text" aria-label="navigation button group">
                    {firstButton}
                    {decreaseButton}
                    <IconButton disabled={nav.median |> getNav |> fst} onClick={fun _ -> (nav.median |> getNav |> snd) ()} >{Mui.Icons.PauseIcon}</IconButton>
                    {increaseButton}
                    {lastButton}
                </ButtonGroup>
                </Box>
                """
            )

        let endAdornment = 
            if navigation.IsNone && not isClear && props.hasClear then Some clearButton
            else
                navigation

        let hasNavigation =
            props.navigate
            |> Option.map (fun nav ->
                nav.first.IsSome ||
                nav.decrease.IsSome ||
                nav.median.IsSome ||
                nav.increase.IsSome ||
                nav.last.IsSome
            )
            |> Option.defaultValue false

        let hasInteraction = 
            hasNavigation || props.values.Length > 1

        let sx =
            match props.warning, hasInteraction with
            | Some color, _ ->
                {|
                    ``& .MuiSelect-icon`` =
                        {|
                            visibility = 
                                if endAdornment.IsNone then "visible" 
                                else "hidden"
                        |}
                    textDecoration = "underline double"
                    textDecorationColor = color
                    textUnderlineOffset = "3px"
                |}
                |> box
            | None, false ->
                {| 
                    ``& .MuiSelect-icon`` =
                        {|
                            visibility = 
                                if endAdornment.IsNone then "visible" 
                                else "hidden"
                        |}
                    backgroundColor = "action.hover"
                    borderRadius = "4px"
                    padding = "2px 8px"
                |}
                |> box
            | None, true ->
                {| 
                    ``& .MuiSelect-icon`` =
                        {|
                            visibility = 
                                if endAdornment.IsNone then "visible" 
                                else "hidden"
                        |}
                |}
                |> box

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';

        <FormControl variant="standard" sx={ {| minWidth = props.minWidth |> Option.defaultValue 150; maxWidth = "100%" |} }>
            <InputLabel id={props.label}>{props.label}</InputLabel>
            <Select
            labelId={props.label}
            id={props.label}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            disabled={props.disabled}
            endAdornment={endAdornment}
            sx={sx}
            MenuProps={menuProps}
            >
                {items}
            </Select>
        </FormControl>
        """


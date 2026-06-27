namespace Components


module SimpleSelect =


    open System
    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    [<JSX.Component>]
    let View
        (props:
            {|
                label: string
                selected: string option
                values: (string * string)[]
                updateSelected: string option -> unit
                navigate:
                    {|
                        step: (int * int -> string * string) option
                        first: (int -> unit) option
                        decrease: (int -> unit) option
                        median: (unit -> unit) option
                        increase: (int -> unit) option
                        last: (int -> unit) option
                        useDebounce: bool
                        revision: int
                    |} option
                isLoading: bool
                disabled: bool
                hasClear: bool
                warning: string option
                minWidth: int option
            |})
        =

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        // Net click deltas accumulated from the nav buttons. Drive an optimistic displayed
        // value that follows the live click count (the badge) before the server confirms.
        // Inner = single-step decrease/increase (defined increment); outer = first/last in
        // step state (calculated increment). Reset when the underlying value changes.
        let innerDelta, setInnerDelta = React.useState 0
        let outerDelta, setOuterDelta = React.useState 0
        let innerRef = React.useRef 0
        let outerRef = React.useRef 0

        // Use the raw value string as the dependency (a JS primitive compared by value)
        // so the reset only fires when the underlying value actually changes — boxing an
        // option would create a new reference every render and reset on every render.
        let valueKey =
            props.values |> Array.tryHead |> Option.map fst |> Option.defaultValue ""

        // A monotonic counter the parent bumps on every server response. It also resets
        // the optimistic deltas when the server returns the SAME value as before — e.g.
        // stepping past the maximum is clamped back to the current value, so valueKey
        // never changes and would otherwise leave the stale optimistic value displayed.
        let revision =
            props.navigate |> Option.map (fun n -> n.revision) |> Option.defaultValue 0

        // useLayoutEffect (not useEffect) so the deltas are reset BEFORE the browser
        // paints the frame on which the server's new value arrives — otherwise that frame
        // would briefly show newServerValue + staleDelta × increment.
        React.useLayoutEffect (
            (fun () ->
                innerRef.current <- 0
                outerRef.current <- 0
                setInnerDelta 0
                setOuterDelta 0
            ),
            [| box valueKey; box revision |]
        )

        let bumpInner sign =
            fun () ->
                innerRef.current <- innerRef.current + sign
                setInnerDelta innerRef.current

        let bumpOuter sign =
            fun () ->
                outerRef.current <- outerRef.current + sign
                setOuterDelta outerRef.current

        // Override only the displayed LABEL with the optimistically stepped value, keeping
        // the original (server-provided) key. The key is a BigRational string the server
        // recognises, so an in-flight dropdown change still dispatches a valid key; only
        // the shown text reflects the optimistic step.
        let displayValues, displaySelected =
            match props.navigate |> Option.bind (fun n -> n.step), props.values |> Array.tryHead with
            | Some step, Some(origKey, _) when innerDelta <> 0 || outerDelta <> 0 ->
                let _, label = step (innerDelta, outerDelta)
                [| (origKey, label) |], Some origKey
            | _ -> props.values, props.selected

        let selectSlotProps =
            if isMobile then
                {| menu = {| slotProps = {| paper = {| style = {| maxHeight = 400 |} |} |} |} |}
                |> box
            else
                {| |} |> box

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
            displayValues
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx = { {|
                                                       maxWidth = 400
                                                       paddingY = (if isMobile then 0.25 else 0.75)
                                                   |} } dense={isMobile} >
                    {v}
                </MenuItem>
                """
            )

        let isClear = displaySelected |> Option.defaultValue "" |> String.IsNullOrWhiteSpace

        let clearButton =
            match props.isLoading, isClear with
            | true, _ -> Mui.Icons.Downloading
            | false, true -> null
            | false, false ->
                JSX.jsx
                    $"""
                import ClearIcon from '@mui/icons-material/Clear';
                import IconButton from "@mui/material/IconButton";

                <IconButton onClick={clear}>
                    {Mui.Icons.Clear}
                </IconButton>
                """

        let navigationSx =
            {|
                display = "flex"
                flexDirection = "column"
                alignItems = "center"
            |}

        let navigation =
            props.navigate
            |> Option.map (fun nav ->
                let getNav prop =
                    match prop with
                    | Some onClick -> props.disabled, onClick
                    | None -> true, fun () -> ()

                let getNavN prop =
                    match prop with
                    | Some onClick -> props.disabled, onClick
                    | None -> true, fun (_: int) -> ()

                let firstDisabled, firstClick = nav.first |> getNavN
                let decreaseDisabled, decreaseClick = nav.decrease |> getNavN
                let increaseDisabled, increaseClick = nav.increase |> getNavN
                let lastDisabled, lastClick = nav.last |> getNavN

                let firstButton =
                    if nav.useDebounce then
                        ClickCountingButton.View(
                            {|
                                disabled = firstDisabled
                                onClick = firstClick
                                onStep = bumpOuter -1
                                icon = Mui.Icons.FirstPageIcon
                            |}
                        )
                    else
                        JSX.jsx
                            $"""
                        import IconButton from "@mui/material/IconButton";
                        <IconButton disabled={firstDisabled} onClick={fun _ -> firstClick 1} >{Mui.Icons.FirstPageIcon}</IconButton>
                        """

                let decreaseButton =
                    if nav.useDebounce then
                        ClickCountingButton.View(
                            {|
                                disabled = decreaseDisabled
                                onClick = decreaseClick
                                onStep = bumpInner -1
                                icon = Mui.Icons.SkipPreviousIcon
                            |}
                        )
                    else
                        JSX.jsx
                            $"""
                        import IconButton from "@mui/material/IconButton";
                        <IconButton disabled={decreaseDisabled} onClick={fun _ -> decreaseClick 1} >{Mui.Icons.SkipPreviousIcon}</IconButton>
                        """

                let increaseButton =
                    if nav.useDebounce then
                        ClickCountingButton.View(
                            {|
                                disabled = increaseDisabled
                                onClick = increaseClick
                                onStep = bumpInner 1
                                icon = Mui.Icons.SkipNextIcon
                            |}
                        )
                    else
                        JSX.jsx
                            $"""
                        import IconButton from "@mui/material/IconButton";
                        <IconButton disabled={increaseDisabled} onClick={fun _ -> increaseClick 1} >{Mui.Icons.SkipNextIcon}</IconButton>
                        """

                let lastButton =
                    if nav.useDebounce then
                        ClickCountingButton.View(
                            {|
                                disabled = lastDisabled
                                onClick = lastClick
                                onStep = bumpOuter 1
                                icon = Mui.Icons.LastPageIcon
                            |}
                        )
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
            if navigation.IsNone && not isClear && props.hasClear then
                Some clearButton
            else
                navigation

        let hasNavigation =
            props.navigate
            |> Option.map (fun nav ->
                nav.first.IsSome
                || nav.decrease.IsSome
                || nav.median.IsSome
                || nav.increase.IsSome
                || nav.last.IsSome
            )
            |> Option.defaultValue false

        let hasInteraction = hasNavigation || props.values.Length > 1

        let sx =
            match props.warning, hasInteraction with
            | Some color, _ ->
                {|
                    ``& .MuiSelect-icon`` = {| visibility = if endAdornment.IsNone then "visible" else "hidden" |}
                    textDecoration = "underline double"
                    textDecorationColor = color
                    textUnderlineOffset = "3px"
                |}
                |> box
            | None, false ->
                {|
                    ``& .MuiSelect-icon`` = {| visibility = if endAdornment.IsNone then "visible" else "hidden" |}
                    backgroundColor = "action.hover"
                    borderRadius = "4px"
                    padding = "2px 8px"
                |}
                |> box
            | None, true ->
                {| ``& .MuiSelect-icon`` = {| visibility = if endAdornment.IsNone then "visible" else "hidden" |} |}
                |> box

        let formControlSx =
            {|
                minWidth = props.minWidth |> Option.defaultValue 150
                maxWidth = "100%"
            |}

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';

        <FormControl variant="standard" sx={formControlSx}>
            <InputLabel id={props.label + "-label"}>{props.label}</InputLabel>
            <Select
            labelId={props.label + "-label"}
            id={props.label}
            name={props.label}
            value={displaySelected |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            disabled={props.disabled}
            endAdornment={endAdornment}
            sx={sx}
            slotProps={selectSlotProps}
            >
                {items}
            </Select>
        </FormControl>
        """

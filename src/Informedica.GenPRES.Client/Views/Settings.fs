namespace Views


module Settings =

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open Shared.Types


    [<JSX.Component>]
    let View
        (props: {|
            reloadResources: string -> unit
            orderContext: Deferred<OrderContext>
            localizationTerms: Deferred<string [] []>
        |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization

        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let refreshIcon = Mui.Icons.RefreshIcon

        let reloading, setReloading = React.useState false
        let dialogOpen, setDialogOpen = React.useState false
        let password, setPassword = React.useState ""
        let passwordError, setPasswordError = React.useState false
        let wasInProgress = React.useRef false

        let isLoading =
            reloading &&
            match props.orderContext with
            | Resolved _ -> false
            | _ -> true

        React.useEffect (
            fun () ->
                if reloading then
                    match props.orderContext with
                    | InProgress | Recalculating _ ->
                        wasInProgress.current <- true
                    | Resolved _ ->
                        wasInProgress.current <- false
                        setReloading false
                        setDialogOpen false
                        setPassword ""
                    | HasNotStartedYet when wasInProgress.current ->
                        // Error occurred (server rejected), show error in dialog
                        wasInProgress.current <- false
                        setReloading false
                        setDialogOpen true
                        setPasswordError true
                    | _ -> ()
        , [| box reloading; box props.orderContext |]
        )

        let backdrop =
            ViewHelpers.backdropProgress
                isLoading
                (Terms.``Reload resources`` |> getTerm "Reloading resources...")

        let handleOpen = fun _ ->
            setPassword ""
            setPasswordError false
            setDialogOpen true

        let handleClose = fun _ ->
            setDialogOpen false
            setPassword ""
            setPasswordError false

        let handleConfirm = fun _ ->
            setReloading true
            setPasswordError false
            props.reloadResources password

        let handleKeyDown (e: Browser.Types.KeyboardEvent) =
            if e.key = "Enter" then handleConfirm ()

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';
        import Dialog from '@mui/material/Dialog';
        import DialogTitle from '@mui/material/DialogTitle';
        import DialogContent from '@mui/material/DialogContent';
        import DialogActions from '@mui/material/DialogActions';
        import TextField from '@mui/material/TextField';

        <Box sx={ {| p = 2 |} }>
            <Typography variant="h6" sx={ {| mb = 2 |} }>
                {Terms.Settings |> getTerm "Settings"}
            </Typography>
            <Button
                variant="contained"
                disabled={isLoading}
                startIcon={refreshIcon}
                onClick={handleOpen}>
                {Terms.``Reload resources`` |> getTerm "Reload resources"}
            </Button>
            {backdrop}
            <Dialog open={dialogOpen} onClose={handleClose}>
                <DialogTitle>
                    {Terms.``Enter password`` |> getTerm "Enter password"}
                </DialogTitle>
                <DialogContent>
                    <TextField
                        autoFocus={true}
                        margin="dense"
                        label={Terms.Password |> getTerm "Password"}
                        type="password"
                        fullWidth={true}
                        variant="outlined"
                        value={password}
                        onChange={fun (e: Browser.Types.Event) -> setPassword (e.target?value: string); setPasswordError false}
                        error={passwordError}
                        helperText={if passwordError then Terms.``Invalid password`` |> getTerm "Invalid password" else ""}
                        onKeyDown={handleKeyDown}
                    />
                </DialogContent>
                <DialogActions>
                    <Button onClick={handleClose}>
                        {Terms.Cancel |> getTerm "Cancel"}
                    </Button>
                    <Button onClick={handleConfirm} variant="contained">
                        {Terms.Confirm |> getTerm "Confirm"}
                    </Button>
                </DialogActions>
            </Dialog>
        </Box>
        """

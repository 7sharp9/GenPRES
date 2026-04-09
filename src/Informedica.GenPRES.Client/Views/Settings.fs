namespace Views


module Settings =

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open Shared.Types


    let private formatSize (bytes: int64) =
        if bytes < 1024L then
            $"%d{bytes} B"
        elif bytes < 1024L * 1024L then
            $"%.1f{float bytes / 1024.0} KB"
        else
            $"%.1f{float bytes / (1024.0 * 1024.0)} MB"


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let reloadResources = (AppEnv.asEnv<AppEnv.IResources> props.appEnv).ReloadResources
        let orderContext = (AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv).OrderContext

        let logAnalyzer = (AppEnv.asEnv<AppEnv.ILogAnalyzer> props.appEnv)
        let auth = (AppEnv.asEnv<AppEnv.IAuthentication> props.appEnv)

        let localizationTerms =
            (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let refreshIcon = Mui.Icons.RefreshIcon

        let reloading, setReloading = React.useState false
        let dialogOpen, setDialogOpen = React.useState false
        let password, setPassword = React.useState ""
        let passwordError, setPasswordError = React.useState false
        let wasInProgress = React.useRef false

        // Log analyzer local state
        let reportDialogOpen, setReportDialogOpen = React.useState false

        let isLoading =
            reloading
            && match orderContext with
               | Resolved _ -> false
               | _ -> true

        // Load log files on mount only when authenticated
        React.useEffect (
            fun () ->
                if auth.IsAuthenticated then
                    logAnalyzer.ListLogFiles()
            , [||]
        )

        React.useEffect (
            fun () ->
                if reloading then
                    match orderContext with
                    | InProgress
                    | Recalculating _ -> wasInProgress.current <- true
                    | Resolved _ ->
                        wasInProgress.current <- false
                        setReloading false
                        setDialogOpen false
                        setPassword ""
                    | HasNotStartedYet when wasInProgress.current ->
                        wasInProgress.current <- false
                        setReloading false
                        setDialogOpen true
                        setPasswordError true
                    | _ -> ()
            , [| box reloading; box orderContext |]
        )

        // When analysis report resolves, open the report dialog
        React.useEffect (
            fun () ->
                match logAnalyzer.LogAnalysisReport with
                | Resolved _ -> setReportDialogOpen true
                | _ -> ()
            , [| box logAnalyzer.LogAnalysisReport |]
        )

        let backdrop =
            ViewHelpers.backdropProgress isLoading (Terms.``Reload resources`` |> getTerm "Reloading resources...")

        let handleOpen =
            fun _ ->
                setPassword ""
                setPasswordError false
                setDialogOpen true

        let handleClose =
            fun _ ->
                setDialogOpen false
                setPassword ""
                setPasswordError false

        let handleConfirm =
            fun _ ->
                setReloading true
                setPasswordError false
                reloadResources password

        let handleKeyDown (e: Browser.Types.KeyboardEvent) =
            if e.key = "Enter" then
                handleConfirm ()

        let handleRefreshLogs = fun _ -> logAnalyzer.ListLogFiles()

        let handleFileClick (fileName: string) =
            fun _ -> logAnalyzer.AnalyzeLogFile fileName

        let handleReportClose = fun _ -> setReportDialogOpen false

        let isAnalyzing =
            match logAnalyzer.LogAnalysisReport with
            | InProgress -> true
            | _ -> false

        let analysisBackdrop =
            ViewHelpers.backdropProgress isAnalyzing "Analyzing log file..."

        // Log file table content
        let logFileTable =
            match logAnalyzer.LogFiles with
            | InProgress ->
                JSX.jsx
                    $"""
                    <Box sx={ {|
                                  display = "flex"
                                  justifyContent = "center"
                                  p = 4
                              |} }>
                        <CircularProgress />
                    </Box>
                    """
            | Resolved files when files.Length = 0 ->
                JSX.jsx
                    $"""
                    <Typography variant="body2" sx={ {| p = 2 |} }>No log files found.</Typography>
                    """
            | Resolved files ->
                let rows =
                    files
                    |> Array.map (fun f ->
                        let onClick = handleFileClick f.FileName
                        let sizeText = formatSize f.SizeBytes

                        JSX.jsx
                            $"""
                            <TableRow
                                key={f.FileName}
                                hover={true}
                                onClick={onClick}
                                sx={ {| cursor = "pointer" |} }>
                                <TableCell>{f.FileName}</TableCell>
                                <TableCell align="right">{sizeText}</TableCell>
                                <TableCell>{f.LastModifiedAt}</TableCell>
                            </TableRow>
                            """
                    )

                JSX.jsx
                    $"""
                    <TableContainer component={{Paper}} sx={ {| maxHeight = 500 |} }>
                        <Table stickyHeader={true} size="small">
                            <TableHead>
                                <TableRow>
                                    <TableCell>{"File Name"}</TableCell>
                                    <TableCell align="right">{"Size"}</TableCell>
                                    <TableCell>{"Last Modified"}</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {rows}
                            </TableBody>
                        </Table>
                    </TableContainer>
                    """
            | _ -> JSX.jsx $"""<></>"""

        // Analysis report content
        let reportContent =
            match logAnalyzer.LogAnalysisReport with
            | InProgress ->
                JSX.jsx
                    $"""
                    <Box sx={ {|
                                  display = "flex"
                                  justifyContent = "center"
                                  p = 4
                              |} }>
                        <CircularProgress />
                    </Box>
                    """
            | Resolved report ->
                JSX.jsx
                    $"""
                    <Box sx={ {|
                                  overflow = "auto"
                                  maxHeight = "70vh"
                              |} }>
                        <pre style={ {|
                                         whiteSpace = "pre-wrap"
                                         wordBreak = "break-word"
                                         fontFamily = "monospace"
                                         fontSize = "0.8rem"
                                         margin = 0
                                     |} }>
                            {report}
                        </pre>
                    </Box>
                    """
            | _ -> JSX.jsx $"""<Typography>No report available.</Typography>"""

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
        import Table from '@mui/material/Table';
        import TableBody from '@mui/material/TableBody';
        import TableCell from '@mui/material/TableCell';
        import TableContainer from '@mui/material/TableContainer';
        import TableHead from '@mui/material/TableHead';
        import TableRow from '@mui/material/TableRow';
        import Paper from '@mui/material/Paper';
        import CircularProgress from '@mui/material/CircularProgress';
        import IconButton from '@mui/material/IconButton';

        <Box sx={ {| p = 2 |} }>
            <Typography variant="h6" sx={ {| mb = 2 |} }>
                {Terms.Settings |> getTerm "Settings"}
            </Typography>
            <Box sx={ {|
                          display = "flex"
                          gap = 2
                          mb = 3
                      |} }>
                <Button
                    variant="contained"
                    disabled={isLoading}
                    startIcon={refreshIcon}
                    onClick={handleOpen}>
                    {Terms.``Reload resources`` |> getTerm "Reload resources"}
                </Button>
            </Box>
            {backdrop}
            {analysisBackdrop}
            <Dialog open={dialogOpen} onClose={handleClose}>
                <DialogTitle>
                    {Terms.``Enter password`` |> getTerm "Enter password"}
                </DialogTitle>
                <DialogContent>
                    <TextField
                        id="settings-password"
                        name="password"
                        autoFocus={true}
                        margin="dense"
                        label={Terms.Password |> getTerm "Password"}
                        type="password"
                        fullWidth={true}
                        variant="outlined"
                        value={password}
                        onChange={fun (e: Browser.Types.Event) ->
                                      setPassword (e.target?value: string)
                                      setPasswordError false}
                        error={passwordError}
                        helperText={if passwordError then
                                        Terms.``Invalid password`` |> getTerm "Invalid password"
                                    else
                                        ""}
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
            <Box sx={ {|
                          display = "flex"
                          alignItems = "center"
                          gap = 1
                          mb = 1
                      |} }>
                <Typography variant="subtitle1">{"Log Files"}</Typography>
                <IconButton size="small" onClick={handleRefreshLogs} title="Refresh">
                    {refreshIcon}
                </IconButton>
            </Box>
            {logFileTable}
            <Dialog open={reportDialogOpen} onClose={handleReportClose} maxWidth="lg" fullWidth={true}>
                <DialogTitle>{"Log Analysis Report"}</DialogTitle>
                <DialogContent>
                    {reportContent}
                </DialogContent>
                <DialogActions>
                    <Button onClick={handleReportClose}>
                        {Terms.Cancel |> getTerm "Close"}
                    </Button>
                </DialogActions>
            </Dialog>
        </Box>
        """

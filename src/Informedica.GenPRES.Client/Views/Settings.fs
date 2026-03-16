namespace Views


module Settings =

    open Fable.Core
    open Feliz
    open Shared


    [<JSX.Component>]
    let View
        (props: {|
            reloadResources: unit -> unit
            localizationTerms: Deferred<string [] []>
        |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization

        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let refreshIcon = Mui.Icons.RefreshIcon

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';

        <Box sx={ {| p = 2 |} }>
            <Typography variant="h6" sx={ {| mb = 2 |} }>
                {Terms.Settings |> getTerm "Settings"}
            </Typography>
            <Button
                variant="contained"
                startIcon={refreshIcon}
                onClick={fun _ -> props.reloadResources ()}>
                {Terms.``Reload resources`` |> getTerm "Reload resources"}
            </Button>
        </Box>
        """

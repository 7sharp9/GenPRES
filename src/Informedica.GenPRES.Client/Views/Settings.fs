namespace Views


module Settings =

    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types


    [<JSX.Component>]
    let View
        (props: {|
            reloadResources: unit -> unit
            orderContext: Deferred<OrderContext>
            localizationTerms: Deferred<string [] []>
        |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization

        let getTerm = Global.getLocalizedTerm props.localizationTerms lang

        let refreshIcon = Mui.Icons.RefreshIcon

        let reloading, setReloading = React.useState false

        let isLoading =
            reloading &&
            match props.orderContext with
            | Resolved _ -> false
            | _ -> true

        React.useEffect (
            fun () ->
                if reloading then
                    match props.orderContext with
                    | Resolved _ -> setReloading false
                    | _ -> ()
        , [| box reloading; box props.orderContext |]
        )

        let backdrop =
            ViewHelpers.backdropProgress
                isLoading
                (Terms.``Reload resources`` |> getTerm "Reloading resources...")

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
                disabled={isLoading}
                startIcon={refreshIcon}
                onClick={fun _ ->
                    setReloading true
                    props.reloadResources ()
                }>
                {Terms.``Reload resources`` |> getTerm "Reload resources"}
            </Button>
            {backdrop}
        </Box>
        """

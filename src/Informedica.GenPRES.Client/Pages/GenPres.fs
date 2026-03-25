namespace Pages


module GenPres =


    open Fable.Core
    open Fable.React
    open Feliz
    open Elmish
    open Shared
    open Shared.Types


    module private Elmish =


        open Global


        type State =
            {
                SideMenuItems: (JSX.Element option * string * bool)[]
                SideMenuIsOpen: bool
                Configuration: Configuration Option
            }


        type Msg =
            | SideMenuClick of string
            | ToggleMenu


        let pages =
            [
                LifeSupport
                ContinuousMeds
                Prescribe
                Nutrition
                TreatmentPlan
                Interactions
                Formulary
                Parenteralia
                Settings
            ]


        let init lang terms page : State * Cmd<Msg> =
            let state =
                {
                    SideMenuItems =
                        pages
                        |> List.toArray
                        |> Array.map (fun p ->
                            let b = p = page

                            match p |> pageToString terms lang with
                            | s when p = LifeSupport -> Mui.Icons.FireExtinguisher |> Some, s, b
                            | s when p = ContinuousMeds -> Mui.Icons.Vaccines |> Some, s, b
                            | s when p = Prescribe -> Mui.Icons.Message |> Some, s, b
                            | s when p = Nutrition -> Mui.Icons.LocalDiningIcon |> Some, s, b
                            | s when p = TreatmentPlan -> Mui.Icons.SummarizeIcon |> Some, s, b
                            | s when p = Interactions -> Mui.Icons.WarningAmber |> Some, s, b
                            | s when p = Formulary -> Mui.Icons.LocalPharmacy |> Some, s, b
                            | s when p = Parenteralia -> Mui.Icons.Bloodtype |> Some, s, b
                            | s when p = Settings -> Mui.Icons.Settings |> Some, s, b
                            | s -> None, s, b
                        )

                    SideMenuIsOpen = false
                    Configuration = None
                }

            state, Cmd.none


        let update lang terms updatePage (msg: Msg) (state: State) =
            match msg with

            | ToggleMenu -> { state with SideMenuIsOpen = not state.SideMenuIsOpen }, Cmd.none

            | SideMenuClick s ->
                pages
                |> List.map (fun p -> p |> pageToString terms lang, p)
                |> List.tryFind (fst >> ((=) s))
                |> Option.map snd
                |> Option.defaultValue LifeSupport
                |> updatePage

                { state with
                    SideMenuItems =
                        state.SideMenuItems
                        |> Array.map (fun (icon, item, _) -> if item = s then icon, item, true else icon, item, false)
                },
                Cmd.none


    open Elmish


    [<JSX.Component>]
    let View
        (props:
            {|
                appEnv: obj
                showDisclaimer: bool
                isDemo: bool
                acceptDisclaimer: bool -> unit
                updatePage: Global.Pages -> unit
                page: Global.Pages
                languages: Localization.Locales[]
                hospitals: Deferred<string[]>
                switchLang: Localization.Locales -> unit
                switchHosp: string -> unit
            |})
        =

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let localizationTerms = (props.appEnv :?> AppEnv.ILocalization).LocalizationTerms
        let orderContext = (props.appEnv :?> AppEnv.IOrderContext).OrderContext
        let treatmentPlan = (props.appEnv :?> AppEnv.ITreatmentPlan).TreatmentPlan
        let nutritionPlan = (props.appEnv :?> AppEnv.INutritionPlan).NutritionPlan

        let deps =
            [|
                box props.page
                box props.updatePage
                box lang
                box orderContext
            |]

        let state, dispatch =
            React.useElmish (
                init lang localizationTerms props.page,
                update lang localizationTerms props.updatePage,
                deps
            )

        let modalStyle =
            {|
                position = "absolute"
                top = "50%"
                left = "50%"
                transform = "translate(-50%, -50%)"
                width = "90vw"
                maxWidth = 400
                bgcolor = "background.paper"
                boxShadow = 24
            |}

        let sxPageBox =
            {|
                marginTop = 3
                paddingRight = 1
                overflowY =
                    match props.page with
                    | Global.Pages.Prescribe
                    | Global.Pages.Nutrition
                    | Global.Pages.TreatmentPlan
                    | Global.Pages.Interactions
                    | Global.Pages.Parenteralia
                    | Global.Pages.Formulary -> "auto"
                    | _ when not isMobile -> "hidden"
                    | _ -> "auto"
            |}

        let appEnvProps = {| appEnv = props.appEnv |}

        let patientBox =
            match props.page with
            | Global.Pages.Settings -> null
            | _ ->
                let patView = Views.Patient.View appEnvProps

                let sxPatientBox = {| flexBasis = 1 |}

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={sxPatientBox} >
                    {patView}
                </Box>
                """

        let title =
            let s = $"GenPRES 2023 {props.page |> Global.pageToString localizationTerms lang}"

            if props.isDemo then $"{s} - DEMO VERSION!" else s


        let titleBar =
            Components.TitleBar.View
                {|
                    title = title
                    toggleSideMenu = fun _ -> ToggleMenu |> dispatch
                    languages = props.languages
                    hospitals = props.hospitals
                    switchLang = props.switchLang
                    switchHosp = props.switchHosp
                |}

        let sideMenu =
            Components.SideMenu.View
                {|
                    anchor = "left"
                    isOpen = state.SideMenuIsOpen
                    toggle = fun _ -> ToggleMenu |> dispatch
                    menuClick = SideMenuClick >> dispatch
                    items = state.SideMenuItems
                |}

        let pageView =
            match props.page with
            | Global.Pages.LifeSupport -> Views.EmergencyList.View appEnvProps
            | Global.Pages.ContinuousMeds -> Views.ContinuousMeds.View appEnvProps
            | Global.Pages.Prescribe -> Views.Prescribe.View appEnvProps
            | Global.Pages.Nutrition -> Views.Nutrition.View appEnvProps
            | Global.Pages.TreatmentPlan -> Views.TreatmentPlan.View appEnvProps
            | Global.Pages.Interactions -> Views.Interactions.View appEnvProps
            | Global.Pages.Formulary -> Views.Formulary.View appEnvProps
            | Global.Pages.Parenteralia -> Views.Parenteralia.View appEnvProps
            | Global.Pages.Settings -> Views.Settings.View appEnvProps

        let totalsView =
            match props.page with
            | Global.Pages.Prescribe ->
                match orderContext with
                | Resolved pr -> Views.Totals.View {| intake = pr.Intake |}
                | _ -> null
            | Global.Pages.Nutrition ->
                match nutritionPlan with
                | Resolved np -> Views.Totals.View {| intake = np.Totals |}
                | _ -> null
            | Global.Pages.TreatmentPlan ->
                match treatmentPlan with
                | Resolved tp -> Views.Totals.View {| intake = tp.Totals |}
                | _ -> null
            | _ -> null

        let disclaimerView =
            Views.Disclaimer.View
                {|
                    accept = props.acceptDisclaimer
                    languages = props.languages
                    switchLang = props.switchLang
                    localizationTerms = localizationTerms
                |}

        let sxContainer =
            {|
                height = "87%"
                marginTop = 3
            |}

        let sxStack = {| height = "100%" |}

        let onCloseModal = fun () -> ()

        JSX.jsx
            $"""
        import {{ ThemeProvider }} from '@mui/material/styles';
        import CssBaseline from '@mui/material/CssBaseline';
        import React from 'react';
        import Stack from '@mui/material/Stack';
        import Box from '@mui/material/Box';
        import Container from '@mui/material/Container';
        import Typography from '@mui/material/Typography';
        import Modal from '@mui/material/Modal';

        <React.Fragment>
            <Box>
                {titleBar}
            </Box>
            <React.Fragment>
                {sideMenu}
            </React.Fragment>
            <Container id="page-container" sx={sxContainer} >
                <Stack sx={sxStack}>
                    {patientBox}
                    <Box id="page-box" sx={sxPageBox}>
                        {pageView}
                    </Box>
                    <Box>
                        {totalsView}
                    </Box>
                </Stack>
            </Container>
            <Modal open={props.showDisclaimer} onClose={onCloseModal} >
                <Box sx={modalStyle}>
                    {disclaimerView}
                </Box>
            </Modal>

        </React.Fragment>
        """

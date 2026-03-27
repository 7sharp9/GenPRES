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
                SideMenuItems: (JSX.Element option * string * bool * string option)[]
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
                OrderPlan
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
                            | s when p = LifeSupport -> Mui.Icons.FireExtinguisher |> Some, s, b, None
                            | s when p = ContinuousMeds -> Mui.Icons.Vaccines |> Some, s, b, None
                            | s when p = Prescribe -> Mui.Icons.Message |> Some, s, b, None
                            | s when p = Nutrition -> Mui.Icons.LocalDiningIcon |> Some, s, b, None
                            | s when p = OrderPlan -> Mui.Icons.SummarizeIcon |> Some, s, b, None
                            | s when p = Interactions -> Mui.Icons.WarningAmber |> Some, s, b, None
                            | s when p = Formulary -> Mui.Icons.LocalPharmacy |> Some, s, b, None
                            | s when p = Parenteralia -> Mui.Icons.Bloodtype |> Some, s, b, None
                            | s when p = Settings -> Mui.Icons.Settings |> Some, s, b, None
                            | s -> None, s, b, None
                        )

                    SideMenuIsOpen = true
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
                        |> Array.map (fun (icon, item, _, bg) ->
                            if item = s then
                                icon, item, true, bg
                            else
                                icon, item, false, bg
                        )
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

        let localizationTerms =
            (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let orderContext = (AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv).OrderContext
        let orderPlan = (AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv).OrderPlan
        let nutritionPlan = (AppEnv.asEnv<AppEnv.INutritionPlan> props.appEnv).NutritionPlan

        let updatePageRef = React.useRef props.updatePage
        updatePageRef.current <- props.updatePage

        let deps =
            [|
                box props.page
                box lang
                box localizationTerms
                box orderContext
            |]

        let state, dispatch =
            React.useElmish (
                init lang localizationTerms props.page,
                (fun msg state -> update lang localizationTerms updatePageRef.current msg state),
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
                paddingBottom = 3
                flexGrow = 1
                minHeight = 0
                overflowY = "auto"
            |}

        let appEnvProps = {| appEnv = props.appEnv |}

        let patientBox =
            match props.page with
            | Global.Pages.Settings -> Unchecked.defaultof<JSX.Element>
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

        let interactions = (AppEnv.asEnv<AppEnv.IInteractions> props.appEnv).Interactions

        let hasInteractions =
            match interactions with
            | Resolved arr -> arr.Length > 0
            | _ -> false

        let interactionsIndex = pages |> List.tryFindIndex ((=) Global.Pages.Interactions)

        let menuItems =
            state.SideMenuItems
            |> Array.mapi (fun idx (icon, text, sel, _) ->
                if Some idx = interactionsIndex && hasInteractions then
                    icon, text, sel, Some "#fff4e5"
                else
                    icon, text, sel, None
            )

        let sideMenu =
            Components.SideMenu.View
                {|
                    anchor = "left"
                    isOpen = state.SideMenuIsOpen
                    isMobile = isMobile
                    toggle = fun _ -> ToggleMenu |> dispatch
                    menuClick = SideMenuClick >> dispatch
                    items = menuItems
                |}

        let pageView =
            match props.page with
            | Global.Pages.LifeSupport -> Views.EmergencyList.View appEnvProps
            | Global.Pages.ContinuousMeds -> Views.ContinuousMeds.View appEnvProps
            | Global.Pages.Prescribe -> Views.Prescribe.View appEnvProps
            | Global.Pages.Nutrition -> Views.Nutrition.View appEnvProps
            | Global.Pages.OrderPlan -> Views.OrderPlan.View appEnvProps
            | Global.Pages.Interactions -> Views.Interactions.View appEnvProps
            | Global.Pages.Formulary -> Views.Formulary.View appEnvProps
            | Global.Pages.Parenteralia -> Views.Parenteralia.View appEnvProps
            | Global.Pages.Settings -> Views.Settings.View appEnvProps

        let totalsView =
            match props.page with
            | Global.Pages.Prescribe ->
                match orderContext with
                | Resolved pr
                | Recalculating pr -> Views.Totals.View {| intake = pr.Intake |}
                | _ -> Unchecked.defaultof<JSX.Element>
            | Global.Pages.Nutrition ->
                match nutritionPlan with
                | Resolved np
                | Recalculating np -> Views.Totals.View {| intake = np.Totals |}
                | _ -> Unchecked.defaultof<JSX.Element>
            | Global.Pages.OrderPlan ->
                match orderPlan with
                | Resolved tp
                | Recalculating tp -> Views.Totals.View {| intake = tp.Totals |}
                | _ -> Unchecked.defaultof<JSX.Element>
            | _ -> Unchecked.defaultof<JSX.Element>

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
                flex = 1
                minHeight = 0
                display = "flex"
                flexDirection = "column"
                paddingTop = 3
            |}

        let sxStack =
            {|
                flex = 1
                minHeight = 0
                display = "flex"
                flexDirection = "column"
            |}

        let sxRoot =
            {|
                height = "100vh"
                display = "flex"
                flexDirection = "column"
                overflow = "hidden"
            |}

        let sxTitleBarBox =
            {|
                flexShrink = 0
                flexGrow = 0
            |}

        let sxSidebarMargin =
            {|
                marginLeft =
                    if isMobile || not state.SideMenuIsOpen then
                        "0px"
                    else
                        $"%i{Components.SideMenu.drawerWidth}px"
            |}

        let sxMarginBox =
            {|
                marginLeft = sxSidebarMargin.marginLeft
                flex = 1
                overflow = "hidden"
                display = "flex"
                flexDirection = "column"
            |}

        let sxTotalsBar =
            {|
                marginLeft = sxSidebarMargin.marginLeft
                flexShrink = 0
                bgcolor = Mui.Colors.Grey.``100``
                marginTop = 2
            |}

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

        <Box sx={sxRoot}>
            <Box sx={sxTitleBarBox}>
                {titleBar}
            </Box>
            {sideMenu}
            <Box sx={sxMarginBox}>
                <Container id="page-container" sx={sxContainer} >
                    <Stack sx={sxStack}>
                        {patientBox}
                        <Box id="page-box" sx={sxPageBox}>
                            {pageView}
                        </Box>
                    </Stack>
                </Container>
            </Box>
            <Box sx={sxTotalsBar}>
                <Container>
                    {totalsView}
                </Container>
            </Box>
            <Modal open={props.showDisclaimer} onClose={onCloseModal} >
                <Box sx={modalStyle}>
                    {disclaimerView}
                </Box>
            </Modal>
        </Box>
        """

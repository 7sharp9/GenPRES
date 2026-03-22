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
                SideMenuItems: (JSX.Element option * string * bool) []
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

            | ToggleMenu ->
                { state with
                    SideMenuIsOpen = not state.SideMenuIsOpen
                },
                Cmd.none

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
                        |> Array.map (fun (icon, item, _) ->
                            if item = s then
                                icon, item, true
                            else
                                icon, item, false
                        )
                },
                Cmd.none


    open Elmish



    [<JSX.Component>]
    let View
        (props: {|
            showDisclaimer: bool
            isDemo: bool
            acceptDisclaimer: bool -> unit
            patient: Patient option
            updatePatient: Patient option -> unit
            updatePage: Global.Pages -> unit
            bolusMedication: Deferred<Intervention list>
            continuousMedication: Deferred<Intervention list>
            onSelectContinuousMedicationItem: string -> unit
            onSelectEmergencyListItem: string -> unit
            products: Deferred<Product list>
            orderContext: Deferred<OrderContext>
            orderContextMsg : (Api.OrderContextCommand * OrderContext) -> unit
            treatmentPlan: Deferred<OrderPlan>
            treatmentPlanCommand: Api.OrderPlanCommand -> unit
            nutritionPlan: Deferred<NutritionPlan>
            nutritionPlanMsg: Api.NutritionPlanCommand -> unit
            formulary: Deferred<Formulary>
            updateFormulary : Formulary -> unit
            parenteralia : Deferred<Parenteralia>
            updateParenteralia : Parenteralia -> unit
            reloadResources : string -> unit
            page : Global.Pages
            localizationTerms : Deferred<string[][]>
            languages : Localization.Locales []
            hospitals : Deferred<string []>
            switchLang : Localization.Locales -> unit
            switchHosp : string -> unit |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let deps =
            [|
                box props.page
                box props.updatePage
                box lang
                box props.orderContext
            |]
        let state, dispatch = React.useElmish (init lang props.localizationTerms props.page, update lang props.localizationTerms props.updatePage, deps)

        let modalStyle =
            {|
                position="absolute"
                top= "50%"
                left= "50%"
                transform= "translate(-50%, -50%)"
                width= "90vw"
                maxWidth= 400
                bgcolor= "background.paper"
                boxShadow= 24
            |}

        let sxPageBox =
            {|
                marginTop= 3
                paddingRight= 1
                overflowY =
                    match props.page with
                    | Global.Pages.Prescribe
                    | Global.Pages.Nutrition
                    | Global.Pages.TreatmentPlan
                    | Global.Pages.Parenteralia
                    | Global.Pages.Formulary -> "auto"
                    | _ when not isMobile -> "hidden"
                    | _ -> "auto"
            |}

        let patientBox =
            match props.page with
            | Global.Pages.Settings -> null
            | _ ->
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={ {| flexBasis=1 |} } >
                    {
                        Views.Patient.View({|
                            patient = props.patient
                            updatePatient = props.updatePatient
                            localizationTerms = props.localizationTerms
                        |})
                    }
                </Box>
                """

        JSX.jsx
            $"""
        import {{ ThemeProvider }} from '@mui/material/styles';
        import CssBaseline from '@mui/material/CssBaseline';
        import React from "react";
        import Stack from '@mui/material/Stack';
        import Box from '@mui/material/Box';
        import Container from '@mui/material/Container';
        import Typography from '@mui/material/Typography';
        import Modal from '@mui/material/Modal';

        <React.Fragment>
            <Box>
                {Components.TitleBar.View({|
                    title =
                        let s = $"GenPRES 2023 {props.page |> Global.pageToString props.localizationTerms lang}"
                        if props.isDemo then $"{s} - DEMO VERSION!" else s

                    toggleSideMenu = fun _ -> ToggleMenu |> dispatch
                    languages = props.languages
                    hospitals = props.hospitals
                    switchLang = props.switchLang
                    switchHosp = props.switchHosp
                |})}
            </Box>
            <React.Fragment>
                {
                    Components.SideMenu.View({|
                        anchor = "left"
                        isOpen = state.SideMenuIsOpen
                        toggle = (fun _ -> ToggleMenu |> dispatch)
                        menuClick = SideMenuClick >> dispatch
                        items =  state.SideMenuItems
                    |})
                }
            </React.Fragment>
            <Container id="page-container" sx={ {| height="87%"; marginTop= 3 |} } >
                <Stack sx={ {| height="100%" |} }>
                    {patientBox}
                    <Box id="page-box" sx={sxPageBox}>
                        {
                            match props.page with
                            | Global.Pages.LifeSupport ->
                                Views.EmergencyList.View {|
                                    interventions = props.bolusMedication
                                    localizationTerms = props.localizationTerms
                                    patient = props.patient
                                    onSelectItem = props.onSelectEmergencyListItem
                                |}
                            | Global.Pages.ContinuousMeds ->
                                Views.ContinuousMeds.View {|
                                    interventions = props.continuousMedication
                                    localizationTerms = props.localizationTerms
                                    patient = props.patient
                                    onSelectItem = props.onSelectContinuousMedicationItem
                                |}
                            | Global.Pages.Prescribe ->
                                Views.Prescribe.View {|
                                    orderContext = props.orderContext
                                    orderContextMsg = props.orderContextMsg
                                    treatmentPlan = props.treatmentPlan
                                    updateTreatmentPlan = fun tp -> Api.UpdateOrderPlan (tp, None) |> props.treatmentPlanCommand
                                    localizationTerms = props.localizationTerms
                                |}
                            | Global.Pages.Nutrition ->
                                Views.Nutrition.View {|
                                    patient = props.patient
                                    nutritionPlan = props.nutritionPlan
                                    nutritionPlanMsg = props.nutritionPlanMsg
                                    orderContextMsg = props.orderContextMsg
                                    localizationTerms = props.localizationTerms
                                |}

                            | Global.Pages.TreatmentPlan ->
                                Views.TreatmentPlan.View {|
                                    treatmentPlan = props.treatmentPlan
                                    updateTreatmentPlan = fun tp -> Api.UpdateOrderPlan (tp, None) |> props.treatmentPlanCommand
                                    filterTreatmentPlan = Api.FilterOrderPlan >> props.treatmentPlanCommand
                                    orderContextMsg =
                                        fun (cmd, ctx) ->
                                            match props.treatmentPlan with
                                            | Resolved tp -> Api.UpdateOrderPlan (tp, Some (cmd, ctx)) |> props.treatmentPlanCommand
                                            | _ -> ()
                                    localizationTerms = props.localizationTerms
                                |}
                            | Global.Pages.Formulary ->
                                Views.Formulary.View {|
                                    formulary = props.formulary
                                    updateFormulary = props.updateFormulary
                                    localizationTerms = props.localizationTerms
                                |}
                            | Global.Pages.Parenteralia ->
                                Views.Parenteralia.View {|
                                    parenteralia = props.parenteralia
                                    updateParenteralia = props.updateParenteralia
                                |}
                            | Global.Pages.Settings ->
                                Views.Settings.View {|
                                    reloadResources = props.reloadResources
                                    orderContext = props.orderContext
                                    localizationTerms = props.localizationTerms
                                |}

                        }
                    </Box>
                    <Box>
                        {
                            match props.page with
                            | Global.Pages.Prescribe ->
                                match props.orderContext with
                                | Resolved pr ->
                                    Views.Totals.View {| intake = pr.Intake |}
                                | _ -> null
                            | Global.Pages.Nutrition ->
                                match props.nutritionPlan with
                                | Resolved np ->
                                    Views.Totals.View {| intake = np.Totals |}
                                | _ -> null
                            | Global.Pages.TreatmentPlan ->
                                match props.treatmentPlan with
                                | Resolved tp ->
                                    Views.Totals.View {| intake = tp.Totals |}
                                | _ -> null
                            | _ -> null
                        }
                    </Box>
                </Stack>
            </Container>
            <Modal open={props.showDisclaimer} onClose={fun () -> ()} >
                <Box sx={modalStyle}>
                    {
                        Views.Disclaimer.View {|
                            accept = props.acceptDisclaimer
                            languages = props.languages
                            switchLang = props.switchLang
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>

        </React.Fragment>
        """
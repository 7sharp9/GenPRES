namespace Components


module Accordion =

    open Fable.Core


    [<JSX.Component>]
    let View (props :
            {|
                expanded : bool
                onChange : unit -> unit
                summary : obj
                children : obj
                isMobile : bool
            |}
        ) =
        let sx =
            {|
                bgcolor=Mui.Styles.headerBgColor
                paddingTop=(if props.isMobile then 0 else 1)
                paddingBottom=(if props.isMobile then 0 else 1)
                minHeight = 0
                ``&.Mui-expanded`` = {| minHeight=0 |}
                ``& .MuiAccordionSummary-content`` = {| margin=0; display="flex"; alignItems="center" |}
                ``& .MuiAccordionSummary-content.Mui-expanded`` = {| margin=0 |}
            |}

        JSX.jsx
            $"""
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';


        <Accordion expanded={props.expanded} onChange={fun _ -> props.onChange ()}>
            <AccordionSummary
            sx={sx}
            expandIcon={{ <ExpandMoreIcon /> }}
            >
            {props.summary}
            </AccordionSummary>
            <AccordionDetails sx={ {| paddingTop=(if props.isMobile then 1 else 2) |} }>
                {props.children}
            </AccordionDetails>
        </Accordion>
        """

namespace Components


module SideMenu =


    open Fable.Core


    let drawerWidth = 240


    [<JSX.Component>]
    let View
        (props:
            {|
                anchor: string
                isOpen: bool
                isMobile: bool
                toggle: unit -> unit
                menuClick: string -> unit
                items: (JSX.Element option * string * bool * string option)[]
            |})
        =

        let menu =
            {|
                updateSelected = props.menuClick
                items = props.items
            |}
            |> SelectableList.View

        if props.isMobile then
            let sxDrawerPaper = {| width = drawerWidth |}

            JSX.jsx
                $"""
            import Drawer from '@mui/material/Drawer';

            <div>
                <Drawer
                    anchor={props.anchor}
                    open={props.isOpen}
                    onClose={props.toggle}
                    PaperProps={ {| sx = sxDrawerPaper |} }
                >
                {menu}
                </Drawer>
            </div>
            """
        else
            let sxDrawer =
                {|
                    width = drawerWidth
                    flexShrink = 0
                |}

            let sxDrawerPaper =
                {|
                    width = drawerWidth
                    boxSizing = "border-box"
                    top = "64px"
                |}

            JSX.jsx
                $"""
            import Drawer from '@mui/material/Drawer';

            <Drawer
                variant="permanent"
                anchor={props.anchor}
                sx={sxDrawer}
                PaperProps={ {| sx = sxDrawerPaper |} }
                open={true}
            >
            {menu}
            </Drawer>
            """

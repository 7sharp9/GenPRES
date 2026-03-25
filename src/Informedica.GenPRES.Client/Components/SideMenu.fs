namespace Components


module SideMenu =


    open Fable.Core


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
        let drawerWidth = 240

        let menu =
            {|
                updateSelected = props.menuClick
                items = props.items
            |}
            |> SelectableList.View

        if props.isMobile then
            JSX.jsx
                $"""
            import Drawer from '@mui/material/Drawer';

            <div>
                <Drawer
                    anchor={props.anchor}
                    width={drawerWidth}
                    open={props.isOpen}
                    onClose={props.toggle}
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

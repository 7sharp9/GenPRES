namespace Components


module SelectableList =


    open Fable.Core


    [<JSX.Component>]
    let View
        (props:
            {|
                updateSelected: string -> unit
                items: (JSX.Element option * string * bool * string option)[]
            |})
        =
        let items =
            props.items
            |> Array.mapi (fun i (el, text, selected, bgColor) ->
                let icon =
                    match el with
                    | None -> null
                    | Some el ->
                        JSX.jsx
                            $"""
                        import ListItemIcon from '@mui/material/ListItemIcon';
                        <ListItemIcon>{el}</ListItemIcon>
                        """

                let sxListItem =
                    match bgColor with
                    | Some color -> {| backgroundColor = color |}
                    | None -> {| backgroundColor = "inherit" |}

                JSX.jsx
                    $"""
                import React from 'react';
                import Divider from '@mui/material/Divider';
                import ListItem from '@mui/material/ListItem';
                import ListItemButton from '@mui/material/ListItemButton';
                import ListItemText from '@mui/material/ListItemText';

                <React.Fragment key={i} >
                    <ListItem value={text} sx={sxListItem} >
                        {icon}
                        <ListItemButton selected={selected} onClick={fun _ -> text |> props.updateSelected}>
                        <ListItemText primary={text} />
                        </ListItemButton>
                    </ListItem>
                    <Divider></Divider>
                </React.Fragment>
                """
            )

        JSX.jsx
            $"""
        import List from '@mui/material/List';

        <List>
            {items}
        </List>
        """

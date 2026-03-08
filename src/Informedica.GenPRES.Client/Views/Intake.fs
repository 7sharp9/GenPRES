namespace Views

module Intake =


    open Fable.Core
    open Feliz
    open Shared
    open Types


    let private rows1 = Shared.Models.Totals.intakeRows1
    let private rows2 = Shared.Models.Totals.intakeRows2
    let private rows3 = Shared.Models.Totals.intakeRows3
    let private rows4 = Shared.Models.Totals.intakeRows4


    let private typoGraphy (items : TextItem[]) =
        let variant = "body2"
        let print item =
            match item with
            | Normal s ->
                JSX.jsx
                    $"""
                <Typography variant={variant} color={Mui.Colors.Grey.``700``} display="inline">{s}</Typography>
                """
            | Bold s ->
                JSX.jsx
                    $"""
                <Typography
                color={Mui.Colors.BlueGrey.``700``}
                variant={variant}
                display="inline"
                >
                <strong> {s} </strong>
                </Typography>
                """
            | Italic s ->
                JSX.jsx
                    $"""
                <Typography
                color={Mui.Colors.Grey.``700``}
                variant={variant}
                display="inline"
                >
                {s}
                </Typography>
                """

        JSX.jsx
            $"""
        import Typography from '@mui/material/Typography';
        import Box from '@mui/material/Box';

        <Box display="inline" >
            {items |> Array.map print |> unbox |> React.fragment}
        </Box>
        """


    [<JSX.Component>]
    let View(props: {| intake : Totals |}) =
        let mapRow (intake: Totals) row =
            let print n itms =
                if itms |> Array.length < 2 then [||]
                else
                    [|
                        [| Normal n |] |> typoGraphy
                        itms[0..(itms.Length - 2)] |> typoGraphy
                        [| itms |> Array.last |] |> typoGraphy
                    |]
                |> Array.map box

            row
            |> Array.map (fun cells ->
                let name = cells |> Array.head
                let items = Shared.Models.Totals.substanceToField intake name
                print name items
            )

        let rows1, rows2, rows3, rows4 =
            let map = mapRow props.intake
            map rows1
            ,
            map rows2
            ,
            map rows3
            ,
            map rows4

        let createTable n rows = $"table{n}", Components.BasicTable.View({| header = [||]; rows = rows |}) |> toReact

        let content =
            [|
                if rows1 |> Array.isEmpty |> not then createTable 1 rows1
                if rows2 |> Array.isEmpty |> not then createTable 2 rows2
                if rows3 |> Array.isEmpty |> not then createTable 3 rows3
                if rows4 |> Array.isEmpty |> not then createTable 4 rows4
            |]

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        if isMobile then
            JSX.jsx $"""
            import React from 'react';
            <React.Fragment />
            """
        else
            Components.BottomDrawer.View {|
                isOpen = true;
                content = content
                |}

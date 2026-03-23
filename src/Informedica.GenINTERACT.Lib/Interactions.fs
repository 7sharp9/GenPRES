namespace Informedica.GenInteract.Lib


module Interactions =

    open Informedica.Utils.Lib.BCL


    let dataToInteractions di =
        di
        |> List.map (fun (cls1, dl1, cls2, dl2) ->
            {
                Interaction.DrugClass1 =
                    {
                        DrugClass.Name = cls1
                        Drugs = dl1
                    }
                DrugClass2 =
                    {
                        DrugClass.Name = cls2
                        Drugs = dl2
                    }
            }
        )


    let tupelizeInteractions_ (il: InteractionList) =
        [|
            for i in il do
                for n1 in i.DrugClass1.Drugs do
                    for n2 in i.DrugClass2.Drugs do
                        if not (n1 = n2) then
                            (i.DrugClass1.Name, i.DrugClass2.Name, n1, n2)
        |]
        |> Array.fold
            (fun acc (c1, c2, n1, n2) ->
                if acc |> Array.contains (c2, c1, n2, n1) then
                    acc
                else
                    [| (c1, c2, n1, n2) |] |> Array.append acc
            )
            [||]


    let tupelizeInteractions =
        Informedica.Utils.Lib.Memoization.memoize tupelizeInteractions_


    let check: Check =
        let eqs n d =
            d
            |> String.toLower
            |> String.splitAt ' '
            |> Array.collect (String.splitAt '/')
            |> Array.map String.trim
            |> Array.distinct
            |> Array.exists ((=) (n |> String.trim |> String.toLower))

        fun dl il ->
            let il = il |> tupelizeInteractions

            [
                for (c1, c2, n1, n2) in il do
                    if dl |> List.exists (eqs n1) && dl |> List.exists (eqs n2) then
                        {
                            DrugInteraction.Name = c1, c2
                            Drug1 = n1
                            Drug2 = n2
                        }
            ]

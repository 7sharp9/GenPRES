namespace Informedica.GenForm.Lib


module Source =

    open Informedica.Utils.Lib.BCL


    let toString =
        function
        | Identified s -> s
        | Other s -> s


    let identified = Identified

    let other = Other


    let getLink
        (meds:
            {|
                generic: string
                id: string
            |} list)
        source
        gen
        =
        let gen = gen |> GenericLabel.toString
        let src = source |> toString

        meds
        |> List.tryFind (fun m ->
            m.generic
            |> String.split "+"
            |> List.map String.trim
            |> String.concat "/"
            |> String.equalsCapInsens gen
        )
        |> Option.bind (fun m ->
            let gen = gen |> String.replace "/" "-"

            match src with
            | _ when src = "NKF" ->
                $"[Kinderformularium](https://www.kinderformularium.nl/geneesmiddel/{m.id}/{gen})"
                |> Some
            | _ when src = "FK" ->
                $"[Farmacotherapeutisch Kompas](https://www.farmacotherapeutischkompas.nl/bladeren/preparaatteksten/n/{gen}#doseringen)"
                |> Some
            | _ -> None
        )

namespace Informedica.GenForm.Lib


module PharmaceuticalForm =

    open Informedica.Utils.Lib.BCL


    let fromString s =
        match s with
        | _ when s |> String.containsCapsInsens "vloeistof" -> s |> Solution
        | _ -> s |> Solid


    let toString =
        function
        | Solution s
        | Solid s -> s

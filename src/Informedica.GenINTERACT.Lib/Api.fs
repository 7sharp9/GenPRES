namespace Informedica.GenInteract.Lib


module Api =


    let getDrugNames (json: string option) : string list =
        let data = Data.fromCache json

        data.DrugClasses |> List.collect _.Drugs |> List.distinct |> List.sort


    let checkInteractions (json: string option) (drugNames: DrugName list) : DrugInteraction list =
        let interactions = Data.cacheToInteractions json
        Interactions.check drugNames interactions

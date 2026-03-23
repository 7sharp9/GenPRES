namespace Informedica.GenInteract.Lib


module Api =

    let checkInteractions (json: string option) (drugNames: DrugName list) : DrugInteraction list =
        let interactions = Data.cacheToInteractions json
        Interactions.check drugNames interactions

namespace Informedica.GenInteract.Lib


type ClassName = string


type DrugName = string


type DrugClass =
    {
        Name: ClassName
        Drugs: DrugName list
    }


type Interaction =
    {
        DrugClass1: DrugClass
        DrugClass2: DrugClass
    }


type InteractionList = Interaction list


type DrugInteraction =
    {
        Name: ClassName * ClassName
        Drug1: DrugName
        Drug2: DrugName
    }


type DrugInteractions = DrugInteraction list


type Check = DrugName list -> InteractionList -> DrugInteractions


// Cache/serialization types (matches Data.JSON format)
type CacheClass =
    {
        Name: string
        Drugs: string list
    }


type CacheInteraction =
    {
        DrugClass1: string
        DrugClass2: string
    }


type InteractionData =
    {
        DrugClasses: CacheClass list
        Interactions: CacheInteraction list
    }

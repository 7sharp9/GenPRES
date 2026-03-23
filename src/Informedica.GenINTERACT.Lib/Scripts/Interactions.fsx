#I __SOURCE_DIRECTORY__

#r "nuget: Expecto, 10.2.1"
#r "nuget: Expecto.Flip, 10.2.1"
#r "nuget: Newtonsoft.Json"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"


open System
open System.IO
open Newtonsoft.Json
open Expecto
open Expecto.Flip


Environment.CurrentDirectory <- __SOURCE_DIRECTORY__ + "/../../../"


// ---------------------------------------------------------------
//  Types
// ---------------------------------------------------------------


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


type Interactions = Interaction list


type DrugInteraction =
    {
        Name: ClassName * ClassName
        Drug1: DrugName
        Drug2: DrugName
    }


type DrugInteractions = DrugInteraction list


type Check = DrugName list -> Interactions -> DrugInteractions


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


// ---------------------------------------------------------------
//  Interactions module
// ---------------------------------------------------------------


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


    let tupelizeInteractions_ (il: Interactions) =
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


// ---------------------------------------------------------------
//  Data module
// ---------------------------------------------------------------


module Data =

    let fromCache (json: string option) =
        match json with
        | Some s -> JsonConvert.DeserializeObject<InteractionData>(s)
        | None ->
            let path = "data/cache/interactions/Data.JSON"
            File.ReadAllText(path) |> JsonConvert.DeserializeObject<InteractionData>


    let cacheToInteractions (json: string option) : Interaction list =
        fromCache json
        |> fun d ->
            let getDrugs n =
                d.DrugClasses
                |> List.filter (fun dc -> dc.Name = n)
                |> List.collect (fun dc -> dc.Drugs)

            d.Interactions
            |> List.map (fun
                             {
                                 CacheInteraction.DrugClass1 = c1
                                 DrugClass2 = c2
                             } ->
                {
                    Interaction.DrugClass1 =
                        {
                            DrugClass.Name = c1
                            Drugs = c1 |> getDrugs
                        }
                    DrugClass2 =
                        {
                            DrugClass.Name = c2
                            Drugs = c2 |> getDrugs
                        }
                }
            )


// ---------------------------------------------------------------
//  Api module
// ---------------------------------------------------------------


module Api =

    let checkInteractions (json: string option) (drugNames: DrugName list) : DrugInteraction list =
        let interactions = Data.cacheToInteractions json
        Interactions.check drugNames interactions


// ---------------------------------------------------------------
//  Tests
// ---------------------------------------------------------------


let interactionTests =
    testList
        "Interaction checking"
        [
            test "tupelizeInteractions deduplicates symmetric pairs" {
                let il: Interactions =
                    [
                        {
                            Interaction.DrugClass1 =
                                {
                                    DrugClass.Name = "ClassA"
                                    Drugs = [ "drugA" ]
                                }
                            DrugClass2 =
                                {
                                    DrugClass.Name = "ClassB"
                                    Drugs = [ "drugB" ]
                                }
                        }
                        {
                            Interaction.DrugClass1 =
                                {
                                    DrugClass.Name = "ClassB"
                                    Drugs = [ "drugB" ]
                                }
                            DrugClass2 =
                                {
                                    DrugClass.Name = "ClassA"
                                    Drugs = [ "drugA" ]
                                }
                        }
                    ]

                let tuples = Interactions.tupelizeInteractions_ il

                tuples |> Array.length |> Expect.equal "should have 1 tuple (not 2)" 1
            }

            test "check detects known interaction" {
                let il: Interactions =
                    [
                        {
                            Interaction.DrugClass1 =
                                {
                                    DrugClass.Name = "ACE Inhibitors"
                                    Drugs = [ "lisinopril"; "enalapril" ]
                                }
                            DrugClass2 =
                                {
                                    DrugClass.Name = "NSAIDs"
                                    Drugs = [ "ibuprofen"; "naproxen" ]
                                }
                        }
                    ]

                let drugs = [ "lisinopril"; "ibuprofen" ]
                let result = Interactions.check drugs il

                result |> List.length |> Expect.equal "should detect 1 interaction" 1
                result.[0].Drug1 |> Expect.equal "drug1 should be lisinopril" "lisinopril"
                result.[0].Drug2 |> Expect.equal "drug2 should be ibuprofen" "ibuprofen"
            }

            test "check returns empty for non-interacting drugs" {
                let il: Interactions =
                    [
                        {
                            Interaction.DrugClass1 =
                                {
                                    DrugClass.Name = "ACE Inhibitors"
                                    Drugs = [ "lisinopril" ]
                                }
                            DrugClass2 =
                                {
                                    DrugClass.Name = "NSAIDs"
                                    Drugs = [ "ibuprofen" ]
                                }
                        }
                    ]

                let drugs = [ "paracetamol"; "amoxicillin" ]
                let result = Interactions.check drugs il
                result |> List.length |> Expect.equal "should detect 0 interactions" 0
            }

            test "check handles empty drug list" {
                let il: Interactions =
                    [
                        {
                            Interaction.DrugClass1 =
                                {
                                    DrugClass.Name = "ClassA"
                                    Drugs = [ "drugA" ]
                                }
                            DrugClass2 =
                                {
                                    DrugClass.Name = "ClassB"
                                    Drugs = [ "drugB" ]
                                }
                        }
                    ]

                let result = Interactions.check [] il
                result |> List.length |> Expect.equal "should detect 0 interactions" 0
            }

            test "check handles case-insensitive matching" {
                let il: Interactions =
                    [
                        {
                            Interaction.DrugClass1 =
                                {
                                    DrugClass.Name = "ClassA"
                                    Drugs = [ "DrugA" ]
                                }
                            DrugClass2 =
                                {
                                    DrugClass.Name = "ClassB"
                                    Drugs = [ "DrugB" ]
                                }
                        }
                    ]

                let drugs = [ "druga"; "drugb" ]
                let result = Interactions.check drugs il

                result
                |> List.length
                |> Expect.equal "should detect 1 interaction (case insensitive)" 1
            }

            test "dataToInteractions converts tuples correctly" {
                let data =
                    [
                        ("ClassA", [ "drugA1"; "drugA2" ], "ClassB", [ "drugB1" ])
                    ]

                let result = Interactions.dataToInteractions data

                result |> List.length |> Expect.equal "should have 1 interaction" 1
                result.[0].DrugClass1.Name |> Expect.equal "class1 name" "ClassA"

                result.[0].DrugClass1.Drugs
                |> List.length
                |> Expect.equal "class1 should have 2 drugs" 2
            }

            test "real Data.JSON loads and check works" {
                let drugs = [ "ciclosporine"; "colestyramine" ]
                let result = Api.checkInteractions None drugs

                result
                |> List.length
                |> Expect.equal "should detect ciclosporine-colestyramine interaction" 1
            }
        ]


runTestsWithCLIArgs [] [||] interactionTests



#load "load.fsx"

#time

open System

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib


Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "1")


let dataUrl = "1rfOo5UjGoVHT5h-bJxR7FS-Qgz4faRrNGLeu2Yj8SS8" //Environment.GetEnvironmentVariable("GENPRES_URL_ID")


printfn $"dataurl: {dataUrl}"


let data = Web.GoogleSheets.getCsvDataFromSheetSync dataUrl "Formulary"


/// The Assortment Product that is
/// available as a GenericProduct.
type Assortment =
    {
        /// The GPK code
        GPK: string
        /// The generic name
        Generic: string
        /// The TallMan alternative name
        TallMan : string
        /// The Divisibility of the product
        Divisible : int
    }



(*
let prods =
    Web.GoogleSheets.getCsvDataFromSheetSync dataUrl "Formulary"
    |> Array.skip 1
    |> Array.map (fun row -> row[0] |> int)
    |> Array.filter (fun gpk -> gpk < 90_000_000)
    |> Array.map (fun gpk ->
        GenPresProduct.findByGPK gpk
        |> Array.tryHead
        |> Option.map (fun prod ->
            prod.Name,
            prod.Form,
            prod.GenericProducts
            |> Array.filter (fun gp -> gp.Id = gpk)
            |> Array.collect (fun gp ->
                gp.PrescriptionProducts
                |> Array.collect (fun pp ->
                    pp.TradeProducts
                    |> Array.map (fun tp -> tp.Brand)
                )
            )
            |> Array.filter (String.isNullOrWhiteSpace >> not)
            |> Array.distinct
            |> String.concat "; ",
            prod.GenericProducts
            |> Array.filter (fun gp -> gp.Id = gpk)
            |> Array.map (fun gp ->
                gp.Substances
                |> Array.map _.GenericName
                |> String.concat "/"
            )
            |> Array.distinct
            |> Array.tryExactlyOne
            |> Option.defaultValue ""
        )
        |> Option.defaultValue ("", "", "", "")
    )


prods |> Array.iter (fun (name, form, brands, genName) -> printfn $"{name}\t{form}\t{brands}\t{genName}")


*)

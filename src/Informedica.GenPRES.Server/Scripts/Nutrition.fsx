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


let data =
    Web.GoogleSheets.getCsvDataFromSheetResultSync dataUrl "Assortment"
    |> Result.defaultValue [||]

(*
Fields to add
UseGenName
UseForm
UseBrand
Mmol
Form
Brand
GenName
Unit
Energy kCal
Carb g
Prot g
Lip g
Sod mmol
Pot mmol
Calc mmol
Posph mmol
Magn
Chlor mmol
Iron mmol
VitD IE
IsReconste
IsDilute
IsAdditive
*)

/// The Assortment Product that is
/// available as a GenericProduct.
type AssortmentProduct =
    {
        /// The GPK code
        GPK: string
        /// The generic name
        Generic: string
        /// The TallMan alternative name
        TallMan: string
        /// The Divisibility of the product
        Divisible: int
        /// Use generic name
        UseGenName: bool
        /// Use form
        UseForm: bool
        /// Use brand
        UseBrand: bool
        /// Mmol
        Mmol: float option
        /// Form
        Form: string option
        /// Brand
        Brand: string option
        /// Generic name
        GenName: string option
        /// Unit
        Unit: string option
        /// Energy in kCal
        EnergyKCal: float option
        /// Carbohydrates in g
        CarbG: float option
        /// Protein in g
        ProtG: float option
        /// Lipids in g
        LipG: float option
        /// Sodium in mmol
        SodMmol: float option
        /// Potassium in mmol
        PotMmol: float option
        /// Calcium in mmol
        CalcMmol: float option
        /// Phosphorus in mmol
        PosphMmol: float option
        /// Magnesium
        Magn: float option
        /// Chloride in mmol
        ChlorMmol: float option
        /// Iron in mmol
        IronMmol: float option
        /// Vitamin D in IE
        VitDIE: float option
        /// Is reconstituted
        IsReconste: bool
        /// Is diluted
        IsDilute: bool
        /// Is additive
        IsAdditive: bool
    }

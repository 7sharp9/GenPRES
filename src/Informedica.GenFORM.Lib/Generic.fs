namespace Informedica.GenForm.Lib


module Generic =


    let create lbl frm ids : Generic =
        {
            Label = lbl
            Form = frm
            Products = ids
        }


    let toString gen = gen.Label |> GenericLabel.toString


    /// The base generic substance name (no form/brand qualifier), for external
    /// lookups such as the G-Standaard. See [[GenericLabel.genericName]].
    let genericName gen = gen.Label |> GenericLabel.genericName

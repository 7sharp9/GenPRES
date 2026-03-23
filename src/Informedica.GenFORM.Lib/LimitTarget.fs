namespace Informedica.GenForm.Lib


module LimitTarget =


    /// Get the LimitTarget as a string.
    let toString =
        function
        | NoLimitTarget
        | OrderableLimitTarget -> ""
        | ComponentLimitTarget s
        | SubstanceLimitTarget s -> s


    /// Get the substance from the SubstanceLimitTarget.
    let componentTargetToString =
        function
        | ComponentLimitTarget s -> s
        | _ -> ""

    /// Get the substance from the SubstanceLimitTarget.
    let substanceTargetToString =
        function
        | SubstanceLimitTarget s -> s
        | _ -> ""


    /// Check whether the LimitTarget is a OrderableLimitTarget.
    let isOrderableTarget target =
        target
        |> function
            | OrderableLimitTarget -> true
            | _ -> false


    /// Check whether the LimitTarget is a SubstanceLimitTarget.
    let isComponentTarget target =
        target
        |> function
            | ComponentLimitTarget _ -> true
            | _ -> false


    /// Check whether the LimitTarget is a SubstanceLimitTarget.
    let isSubstanceTarget target =
        target
        |> function
            | SubstanceLimitTarget _ -> true
            | _ -> false

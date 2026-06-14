#load "../../../scripts/load-dependencies.fsx"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"

// Full source chain in fsproj compile order (AssemblyInfo.fs is omitted —
// it only carries assembly attributes and is not needed in FSI). ValueUnit.fs
// depends on Types.fs/Core.fs/Units.fs/etc., so those must load first.
#load "../Utils.fs"
#load "../Types.fs"
#load "../Core.fs"
#load "../Group.fs"
#load "../Units.fs"
#load "../Combine.fs"
#load "../Parser.fs"
#load "../UnitsParse.fs"
#load "../ValueUnit.fs"
#load "../Api.fs"

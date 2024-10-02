namespace aardpack

open System
open System.IO

[<AutoOpen>]
module PathExtensions =

    type Path with
        static member Relative(path : string, ?baseDir : string) =
            let baseDir = defaultArg baseDir Environment.CurrentDirectory |> Path.GetFullPath
            let path = Path.GetFullPath path
            if path.StartsWith baseDir then
                let rest = path.Substring(baseDir.Length).TrimStart([|Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar|])
                rest
            else
                path


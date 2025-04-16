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

    module Path =
        let rec private getRandomName (path: string) =
            let name = Path.Combine(path, Path.GetRandomFileName())
            if File.Exists name || Directory.Exists name then getRandomName path
            else name

        let rec tempDir (path: string) (action: string -> 'T) =
            let name = getRandomName path
            Directory.CreateDirectory name |> ignore

            try
                action name
            finally
                if Directory.Exists name then Directory.Delete(name, true)
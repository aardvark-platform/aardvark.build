namespace Aardvark.Build.Tool

open ICSharpCode.SharpZipLib.Zip
open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open System.Threading

module LocalSourcesCommand =

    module private LocalSources =

        let private rx = Regex(@"^(?<indent>[ \t]*)(?<content>.*)$", RegexOptions.Compiled)

        let parse (file: string) : Map<string, string list> =
            let info = FileInfo file

            if info.Exists then
                Log.debug $"Found local sources: {file}"

                try
                    let lines = File.ReadAllLines file
                    let mutable result = Map.empty
                    let mutable currentPath = None

                    for l in lines do
                        if not <| String.IsNullOrWhiteSpace l then
                            let m = rx.Match l
                            let isTop = m.Groups.["indent"].Length = 0
                            let content = m.Groups.["content"].Value

                            if isTop then
                                let path = Path.GetFullPath(content, info.DirectoryName)

                                if not <| Directory.Exists path then
                                    Log.warn $"Directory '{path}' does not exist ({file})."
                                else
                                    result <- Map.remove path result
                                    currentPath <- Some path
                            else
                                match currentPath with
                                | Some p when content.Length > 0 ->
                                    let cmds = result |> Map.tryFind p |> Option.defaultValue []
                                    result <- result |> Map.add p (cmds @ [content])

                                | _ -> ()

                    result

                with e ->
                    Log.warn $"Error while parsing '{file}': {e.Message}"
                    Map.empty
            else
                Map.empty

        let rec private parseAllInternal (accum: Map<string, string list>) (directory: string) =
            try
                let directory = Path.GetFullPath(directory)

                let t = parse <| Path.Combine(directory, "local.sources")
                if Map.isEmpty t then
                    accum
                else
                    let mutable res = accum
                    for KeyValue(tt, v) in t do
                        if res |> Map.containsKey tt |> not then
                            res <- Map.add tt v res
                            res <- parseAllInternal res tt
                    res

            with e ->
                Log.warn $"Error while traversing '{directory}' for local sources: {e.Message}"
                accum

        let parseAll (directory: string) =
            directory
            |> parseAllInternal Map.empty
            |> Map.toList

    module private Cache =

        let locked (directory: string) (action: FileStream -> 'T) =
            if not (Directory.Exists directory) then Directory.CreateDirectory directory |> ignore
            let file = Path.Combine(directory, "sources.cache")

            let mutable stream : FileStream = null
            while isNull stream do
                try
                    let s = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
                    stream <- s
                with _ ->
                    Thread.Sleep 400

            try action stream
            finally stream.Dispose()

        let getDirectory =
            let path =
                Lazy<string> (fun () ->
                    let path =
                        Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Aardvark.Build"
                        )

                    if not <| Directory.Exists path then
                        Directory.CreateDirectory(path) |> ignore

                    path

                , false)

            fun () -> path.Value

        let tryRead (hash: string) (file: FileStream) =
            try
                let lines = Stream.readAllLines file |> Seq.toList
                file.Seek(0L, SeekOrigin.Begin) |> ignore

                match lines with
                | cacheHash :: lines when cacheHash = hash ->
                    let mutable result = Map.empty<string * string, string list>

                    for l in lines do
                        match l.Split(';') with
                        | [| packageId; framework; dllPath |] ->
                            let key = packageId, framework

                            let old =
                                result
                                |> Map.tryFind key
                                |> Option.defaultValue []

                            result <- result |> Map.add key (dllPath :: old)

                        | _ ->
                            failwithf $"Invalid line '{l}'."

                    Some result

                | _ ->
                    None
            with e ->
                Log.warn $"Failed to read cache '{file.Name}': {e.Message}"
                None

        let write (hash: string) (libs: Map<string * string, string list>) (file: FileStream) =
            try
                use w = new StreamWriter(file, leaveOpen = true)

                w.WriteLine hash
                for KeyValue((packageId, framework), dlls) in libs do
                    for d in dlls do
                        w.WriteLine(sprintf "%s;%s;%s" packageId framework d)

                file.Seek(0L, SeekOrigin.Begin) |> ignore

            with e ->
                Log.warn $"Failed to write cache '{file.Name}': {e.Message}"

    let private executeCommands (directory: string) (splices: list<string * string>) (commands: string list) =
        for cmd in commands do
            let cmd = (cmd, splices) ||> List.fold (fun cmd (name, value) -> cmd.Replace(sprintf "{%s}" name, value))
            Log.debug $"{cmd}"

            let cmd, args, tempPath =
                if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                    let path = Path.ChangeExtension(Path.GetTempFileName(), ".cmd")
                    File.WriteAllLines(path, ["@echo off"; cmd])
                    "cmd.exe", $"/c \"{path}\"", path
                else
                    let path = Path.ChangeExtension(Path.GetTempFileName(), ".sh")
                    File.WriteAllLines(path, [cmd])
                    "/bin/sh", $"\"{path}\"", path

            try
                Process.run true (Some directory) cmd args |> ignore

            finally
                File.Delete tempPath

    let private extractPackage (packagePath: string) : Map<string * string, string list> =
        let outputFolder = Path.ChangeExtension(packagePath, null)

        if Directory.Exists outputFolder then
            Directory.Delete(outputFolder, true)
        Directory.CreateDirectory outputFolder |> ignore

        let packageName =
            let file = Path.GetFileNameWithoutExtension(packagePath)
            if file.EndsWith ".0.0.0" then file.Substring(0, file.Length - 6)
            else file

        let mutable libs = Map.empty

        use zip = new ZipFile(packagePath)
        for i = 0 to int zip.Count - 1 do
            let e = zip.EntryByIndex i
            if e.Name.StartsWith "lib/" && (e.Name.EndsWith ".dll" || e.Name.EndsWith ".exe") then
                let path = e.Name.Substring(4).Replace('/', Path.DirectorySeparatorChar)

                let outPath = Path.Combine(outputFolder, path)
                if not (Directory.Exists (Path.GetDirectoryName outPath)) then
                    Directory.CreateDirectory (Path.GetDirectoryName outPath) |> ignore

                let framework =
                    let i = path.IndexOf Path.DirectorySeparatorChar
                    if i >= 0 then path.Substring(0, i) |> Some
                    else None

                use s = zip.GetInputStream(e)
                use dst = File.OpenWrite outPath
                s.CopyTo dst

                match framework with
                | Some framework ->
                    Log.debug $"{packageName} ({framework}) -> {outPath}"
                    let key = packageName.ToLowerInvariant(), framework
                    let old = libs |> Map.tryFind key |> Option.defaultValue []
                    libs <- libs |> Map.add key (outPath :: old)

                | None ->
                    Log.warn "No framework found for %s (%s)." path packagePath

        libs

    let run (args: Args) =
        let path = Path.GetDirectoryName args.["project-path"]

        let references =
            match args |> Args.tryGet "references" with
            | Some paths -> paths.Split(';')
            | _ -> [||]

        let copyLocal =
            match args |> Args.tryGet "copy-local" with
            | Some paths -> paths.Split(';')
            | _ -> [||]

        let root =
            match args |> Args.tryGet "repository-root" with
            | Some r -> Some r
            | _ ->
                Log.debug "Locating repository root for path: %s" path
                Utilities.locateRepositoryRoot path

        match root with
        | Some root ->
            let sources = LocalSources.parseAll root

            let assemblies =
                sources |> List.map (fun (path, cmds) ->
                    try
                        let srcHash = Directory.computeHash path
                        let cacheDir = Path.Combine(Cache.getDirectory(), Path.getHashName path)

                        Cache.locked cacheDir (fun cacheFile ->
                            match Cache.tryRead srcHash cacheFile with
                            | Some res ->
                                Log.debug $"Found cache for local source: {path}"
                                res

                            | _ ->
                                Log.info $"Building local source: {path}"

                                cmds |> executeCommands path ["OUTPUT", $"\"{cacheDir}\""; "VERSION", "0.0.0"]

                                let libs =
                                    Directory.GetFiles(cacheDir, "*.nupkg")
                                    |> Array.map extractPackage
                                    |> Map.unionMany

                                Cache.write srcHash libs cacheFile
                                libs
                        )
                        |> Map.map (fun _ dlls -> path, dlls)

                    with e ->
                        Log.warn $"Unexpected error: {e.Message}"
                        Map.empty
                )
                |> Map.unionMany

            // Only references with a path that ends in <packageId>\<version>\lib\<framework>\<assembly> are regarded.
            // I.e. how the packages are organized in the nuget package folder.
            let packageRx =
                let s = Regex.Escape($"{Path.DirectorySeparatorChar}{Path.AltDirectorySeparatorChar}")
                let name = $"[^{s}]+"
                let sep = $"[{s}]"
                Regex(
                    $"(?<packageId>{name})" +
                    $"{sep}(?<version>{name})" +
                    $"{sep}lib" +
                    $"{sep}(?<framework>{name})" +
                    $"{sep}{name}$"
                    , RegexOptions.Compiled
                )

            let getOverridePackage (path: string) =
                let m = packageRx.Match path
                if m.Success then
                    let id = m.Groups.["packageId"].Value
                    let version = m.Groups.["version"].Value
                    let framework = m.Groups.["framework"].Value

                    match Map.tryFind (id.ToLowerInvariant(), framework) assemblies with
                    | Some (sourcePath, dlls) ->
                        Some <| {| Id = id; SourcePath = sourcePath; Version = version; Assemblies = dlls |}
                    | None ->
                        None
                else
                    None

            let referencedPackages = Dictionary<_, _>()
            let copyLocalPackages = Dictionary<_, _>()

            let addReferences = ResizeArray<_>()
            let remReferences = ResizeArray<_>()

            let addCopyLocal = ResizeArray<_>()
            let remCopyLocal = ResizeArray<_>()

            for r in references do
                match getOverridePackage r with
                | Some pkg ->
                    referencedPackages.[pkg.Id.ToLowerInvariant()] <- pkg
                    remReferences.Add r
                | _ -> ()

            for c in copyLocal do
                match getOverridePackage c with
                | Some pkg ->
                    copyLocalPackages.[pkg.Id.ToLowerInvariant()] <- pkg
                    remCopyLocal.Add c
                | _ -> ()

            for pkg in referencedPackages.Values do
                Log.warn $"Overriding {pkg.Id} ({pkg.Version}) with build from '{pkg.SourcePath}'"
                addReferences.AddRange pkg.Assemblies

            for pkg in copyLocalPackages.Values do
                addCopyLocal.AddRange pkg.Assemblies

            File.WriteAllText(args.["output-add-references"], addReferences |> String.concat ";")
            File.WriteAllText(args.["output-rem-references"], remReferences |> String.concat ";")
            File.WriteAllText(args.["output-add-copy-local"], addCopyLocal |> String.concat ";")
            File.WriteAllText(args.["output-rem-copy-local"], remCopyLocal |> String.concat ";")

        | None ->
            Log.warn "Could not find repository root (please specify AardvarkBuildRepositoryRoot Property)"

        0
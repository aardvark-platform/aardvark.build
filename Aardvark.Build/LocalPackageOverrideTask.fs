namespace Aardvark.Build

open System
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open System.Threading
open Aardvark.Build
open System.Xml
open System.Xml.Linq
open System.Text.RegularExpressions
open System.Runtime.InteropServices
open System.Diagnostics

type LocalPackageOverrideTask() as this =
    inherit Task()

    // microsoft.build.tasks.core/17.0.0/lib/netstandard2.0/Microsoft.Build.Tasks.Core.dll
    static let packageRx =
        Regex @"^.*([^/]*)/([^/]*)/lib/([^/]*)/([^/]*)\.dll$"

    let mutable repoRoot = ""
    let mutable references : string[] = [||]
    let mutable outputReferences : string[] = [||]
    let mutable copyLocal : string[] = [||]
    let mutable copyLocalOut : string[] = [||]
    let mutable projectPath = ""


    do Tools.boot this.Log

    member x.RepositoryRoot 
        with get() = repoRoot
        and set p = repoRoot <- p


    [<Required>]
    member x.InputCopyLocal
        with get() = copyLocal
        and set d = copyLocal <- d


    [<Output>]
    member x.OutputCopyLocal
        with get() = copyLocalOut
        and set d = copyLocalOut <- d


    [<Required>]
    member x.InputReferences
        with get() = references
        and set d = references <- d

    [<Output>]
    member x.OutputReferences
        with get() = outputReferences
        and set d = outputReferences <- d

        
    [<Required>]
    member x.ProjectPath
        with get() = projectPath
        and set d = projectPath <- d
         
    override x.Execute() =
        let projDir = Path.GetDirectoryName projectPath
        let root =
            if System.String.IsNullOrWhiteSpace repoRoot then Tools.findProjectRoot projDir
            elif Directory.Exists repoRoot then Some repoRoot
            else None


        // let sourcePaths =
        //     [
        //         "/Users/schorsch/Development/FSys/", [
        //             "dotnet tool restore"
        //             "dotnet build -c Debug"
        //             "dotnet paket pack --version {VERSION} --build-config Debug {OUTPUT}"
        //         ]
        //     ]

        let parseLocalSources (file : string) =
            if File.Exists file then
                try
                    let rx = System.Text.RegularExpressions.Regex @"^([ \t]*)(.*)$"
                    use r = new StreamReader(File.OpenRead file)

                    let mutable currentPath = None
                    let mutable result = Map.empty
                    while not r.EndOfStream do  
                        let l = r.ReadLine()
                        let m = rx.Match l
                        let isTop = m.Groups.[1].Length = 0
                        if isTop then   
                            result <- Map.remove m.Groups.[2].Value result
                            currentPath <- Some m.Groups.[2].Value
                        else
                            match currentPath with
                            | Some p -> 
                                result <-
                                    match Map.tryFind p result with
                                    | Some cmds -> Map.add p (cmds @ [m.Groups.[2].Value]) result
                                    | None -> Map.add p ([m.Groups.[2].Value]) result
                            | None ->
                                ()
                    result
                with _ ->
                    Map.empty
            else
                Map.empty
        
        let sourcePaths =
            match root with
            | Some root ->
                let sources = Path.Combine(root, "local.sources")
                if File.Exists sources then
                    let m = parseLocalSources sources

                    let rec run (m : Map<string, list<string>>) (p : string) =
                        let t = parseLocalSources (Path.Combine(p, "local.sources"))
                        if Map.isEmpty t then
                            m
                        else
                            let mutable res = m 
                            for KeyValue(tt, v) in t do
                                res <- Map.add tt v res
                                res <- run res tt
                            res


                    let mutable m = m
                    for KeyValue(p, _) in m do
                        m <- run m p

                    if Map.isEmpty m then None
                    else Some (Map.toList m)
                else
                    None
            | None ->   
                None

        match sourcePaths with
        | Some sourcePaths ->
            // microsoft.build.tasks.core/17.0.0/lib/netstandard2.0/Microsoft.Build.Tasks.Core.dll
            let packagePath = 
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget",
                    "packages"
                )
            let run (workdir : string) (splices : list<string * string>)  (buildCommands : list<string>) =
                
                for cmd in buildCommands do
                    let cmd =
                        (cmd, splices) ||> List.fold (fun cmd (name, value) -> cmd.Replace(sprintf "{%s}" name, value))

                    x.Log.LogMessage (sprintf "running %A" cmd)
                    let info, tempPath = 
                        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                            let path = Path.ChangeExtension(Path.GetTempFileName(), ".cmd")
                            File.WriteAllLines(path, ["@echo off"; cmd])
                            let info = ProcessStartInfo("cmd.exe")
                            info.Arguments <- sprintf "/c \"%s\"" path
                            info, path
                        else
                            let path = Path.ChangeExtension(Path.GetTempFileName(), ".sh")
                            File.WriteAllLines(path, [cmd])
                            let info = ProcessStartInfo("/bin/sh")
                            info.Arguments <- sprintf "\"%s\"" path
                            info, path


                    info.WorkingDirectory <- workdir
                    info.UseShellExecute <- false
                    info.RedirectStandardOutput <- true
                    info.RedirectStandardError <- true
                    info.CreateNoWindow <- true
                    let p = Process.Start(info)
                    p.OutputDataReceived.Add (fun e ->
                        if not (isNull e.Data) then x.Log.LogMessage(sprintf "    %s" e.Data)
                    )
                    p.ErrorDataReceived.Add (fun e ->
                        if not (isNull e.Data) then x.Log.LogMessage(sprintf "    %s" e.Data)
                    )
                    p.BeginOutputReadLine()
                    p.BeginErrorReadLine()

                    p.WaitForExit()
                    if p.ExitCode <> 0 then failwithf "%A failed with %d" cmd p.ExitCode
                    File.Delete tempPath

            let locked (directory : string) (action : unit -> 'a) =
                if not (Directory.Exists directory) then Directory.CreateDirectory directory |> ignore
                let lockFile = Path.Combine(directory, "lockfile")

                let mutable stream : FileStream = null
                while isNull stream do
                    try
                        let s = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None) 
                        stream <- s
                    with _ ->   
                        Thread.Sleep 400

                try action()
                finally stream.Dispose()



            let packageMapping =
                sourcePaths |> List.collect (fun (sourcePath, packCommand) ->
                    
                    let sourcePath = Path.GetFullPath sourcePath

                    if Directory.Exists sourcePath then
                        try
                            let hash = Tools.computeDirectoryHash sourcePath
                            let tempFolder =
                                let sha = System.Security.Cryptography.SHA1.Create()
                                let sourceName = 
                                    System.Convert.ToBase64String(sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes sourcePath))
                                        .Replace("/", "_")
                                        .Replace("=", "-")
                                let dir = 
                                    Path.Combine (
                                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                        "Aardvark.Build",
                                        sourceName
                                    )
                                if not (Directory.Exists dir) then 
                                    Directory.CreateDirectory dir |> ignore
                                dir

                            let cachePath = Path.Combine(tempFolder, "cache.txt")

                            let readCache() =
                                try
                                    if File.Exists cachePath then   
                                        let lines = File.ReadAllLines cachePath |> Array.toList
                                        match lines with
                                        | cacheHash :: lines when cacheHash = hash ->  
                                            let mutable result = Map.empty
                                            let mutable bad = false
                                            for l in lines do
                                                match l.Split(';') with
                                                | [|packageId; framework; dllPath|] ->
                                                    let old =
                                                        match Map.tryFind packageId result with
                                                        | Some p -> p
                                                        | None -> Map.empty
                                                    let n =
                                                        match Map.tryFind framework old with
                                                        | Some o -> Map.add framework (dllPath :: o) old
                                                        | None -> Map.add framework [dllPath] old
                                                    result <- Map.add packageId n result
                                                | _ ->  
                                                    bad <- true

                                            if bad then
                                                None
                                            else
                                                result
                                                |> Map.toList
                                                |> List.map (fun (packageId, map) -> packageId, (sourcePath,map))
                                                |> Some

                                        | _ -> 
                                            None
                                    else
                                        None
                                with _ ->   
                                    None


                            match readCache() with
                            | Some res ->
                                res
                            | None ->

                                locked tempFolder (fun () ->
                                    let result = readCache()
                                        
                                    match result with
                                    | Some res ->
                                        res
                                    | None ->
                                        run sourcePath ["OUTPUT", tempFolder; "VERSION", "0.0.0"] packCommand
                                        //deps.Pack(tempFolder, version = "0.0.0", interprojectReferencesConstraint = Some Paket.InterprojectReferencesConstraint.Fix)

                                        let pkgs = Directory.GetFiles(tempFolder, "*.nupkg")
                                        let result = 
                                            pkgs
                                            |> Array.map (fun packagePath ->
                                                let outputFolder = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension packagePath)
                                                if not (Directory.Exists outputFolder) then
                                                    Directory.CreateDirectory outputFolder |> ignore

                                                use zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(packagePath)
                                                let mutable libs = Map.empty
                                                for i in 0 .. int zip.Count - 1 do
                                                    let e = zip.[i]
                                                    if e.Name.StartsWith "lib/" then
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
                                                            match Map.tryFind framework libs with
                                                            | Some o -> libs <- Map.add framework (outPath :: o) libs
                                                            | None -> libs <- Map.add framework [outPath] libs
                                                        | None -> 
                                                            x.Log.LogWarning (sprintf "No framework found for %s (in %s)" path packagePath)

                                                let packageName =
                                                    let file = Path.GetFileNameWithoutExtension(packagePath)
                                                    if file.EndsWith ".0.0.0" then file.Substring(0, file.Length - 6)
                                                    else file

                                                packageName.ToLower(), (sourcePath, libs)
                                            )
                                            |> Array.toList
                                        for p in pkgs do File.Delete p

                                        do
                                            use w = File.OpenWrite cachePath
                                            use w = new StreamWriter(w)

                                            w.WriteLine hash
                                            for (packageId, (_, map)) in result do
                                                for KeyValue(framework, dlls) in map do
                                                    for d in dlls do
                                                        w.WriteLine(sprintf "%s;%s;%s" packageId framework d)

                                        result
                                )
                        with e ->   
                            x.Log.LogWarning (sprintf "Unexpected error: %s" e.Message)
                            []
                    else
                        x.Log.LogWarning (sprintf "%A does not exist" sourcePath)
                        []
                )
                |> Map.ofList

            let mutable referencedPackages = Map.empty
            let mutable otherReferences = System.Collections.Generic.List()
            for path in references do
                let path = Path.GetFullPath path
                if path.StartsWith packagePath then
                    let relative = path.Substring(packagePath.Length).TrimStart [|Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar|]
                    let parts = relative.Split [|Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar|]
                    if parts.Length = 5 then
                        let id = parts.[parts.Length - 5].ToLower()
                        let version = parts.[parts.Length - 4]
                        let framework = parts.[parts.Length - 2]
                        match Map.tryFind id packageMapping with
                        | Some (sourcePath, map) ->
                            match Map.tryFind framework map with
                            | Some dlls ->
                                referencedPackages <- Map.add id (sourcePath, version, dlls) referencedPackages
                            | None ->   
                                x.Log.LogWarning (sprintf "could not find %s in package %s" framework id)
                                otherReferences.Add path
                        | None ->   
                            otherReferences.Add path
                    else
                        otherReferences.Add path
                else
                    otherReferences.Add path

            for KeyValue(id, (sourcePath, version, dlls))in referencedPackages do
                
                x.Log.LogWarning (sprintf "override %s[%s] with local build from %s" id version sourcePath)
                otherReferences.AddRange dlls


            outputReferences <- otherReferences.ToArray()
        | None ->   
            outputReferences <- references

        true
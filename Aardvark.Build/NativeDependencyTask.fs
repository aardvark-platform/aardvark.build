namespace Aardvark.Build

open System
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open System.Threading
open Aardvark.Build
open System.Xml
open System.Xml.Linq
open Paket
open Paket.Core
open Paket.Domain



type NativeDependencyTask() as this =
    inherit Task()

    let mutable repoRoot = ""
    let mutable designTime = false
    let mutable assembly = ""
    let mutable assemblyName = ""
    let mutable outputPath = ""
    let mutable projectPath = ""

    let mutable cancel : CancellationTokenSource = null
    
    do Tools.boot this.Log

    member x.Remapping (srcDirectory : string, outputPath : string) =
        let ct = cancel.Token
        let file = Path.Combine(srcDirectory, "remap.xml")
        if File.Exists file then
            let d = XDocument.Load(file)
            let config = d.Element(XName.Get "configuration")
            for e in config.Elements(XName.Get("dllmap")) do
                let os = e.Attribute(XName.Get "os").Value
                if os = Native.platform then
                    let dll = e.Attribute(XName.Get "dll").Value
                    let target = e.Attribute(XName.Get "target").Value

                    let dst = Path.Combine(outputPath, target)
                    if File.Exists dst then
                        match Symlink.symlink dst (Path.Combine(outputPath, dll)) with
                        | Ok () -> x.Log.LogMessage $"created symlink {dll} -> {target}"
                        | Error e -> x.Log.LogWarning e

                ct.ThrowIfCancellationRequested()

    member x.CopyNative (libs : string, outputPath : string) =
        let ct = cancel.Token
        let copyDependencies =
            let dir = Path.Combine(libs, Native.platform, Native.arch)
            try
                if Directory.Exists dir then
                    Directory.GetFiles(libs, "*", SearchOption.AllDirectories)
                    |> Array.choose (fun f -> 
                        match Path.TryGetRelativePath(f, dir) with
                        | Some rel -> 
                            let rel = rel.Replace('/', Path.DirectorySeparatorChar)
                            Some (f, rel)
                        | None -> None
                    )
                else
                    [||]
            with _ ->
                [||]

        for (src, dstFile) in copyDependencies do
            let dst = Path.Combine(outputPath, dstFile)

            let dstDate = 
                if File.Exists dst then File.GetLastWriteTime dst else DateTime.MinValue
            let srcDate =
                File.GetLastWriteTime src

            if dstDate < srcDate then
                let dir = Path.GetDirectoryName dst
                if not (Directory.Exists dir) then Directory.CreateDirectory dir |> ignore
                x.Log.LogMessage($"copying {src} to {dst}")
                File.Copy(src, dst, true)

            ct.ThrowIfCancellationRequested()


    member x.Cancel() =
        if not (isNull cancel) then cancel.Cancel()

    interface ICancelableTask with
        member x.Cancel() = x.Cancel()

    override x.Execute() =  
        if designTime then
            true
        else
            let projDir = Path.GetDirectoryName projectPath
            let root =
                if System.String.IsNullOrWhiteSpace repoRoot then Tools.findProjectRoot projDir
                elif Directory.Exists repoRoot then Some repoRoot
                else None

            let libs = 
                match root with
                | Some root ->  
                    let inline isLibDir (path : string) =
                        let name = Path.GetFileName(path).ToLower()
                        if name = "lib" || name = "libs" then
                            Directory.Exists (Path.Combine(path, "Native"))
                        else
                            false
                    match Directory.GetDirectories(root, "*") |> Array.tryFind isLibDir with
                    | Some libs -> 
                        let dir = Path.Combine(libs, "Native", assemblyName)
                        if Directory.Exists dir then Some dir
                        else None
                    | None ->
                        None
                | None ->
                    x.Log.LogWarning "Could not find repository root (please specify RepositoryRoot Property)"
                    None

            match libs with
            | Some libs ->
                cancel <- new CancellationTokenSource()
                try
                    let assemblyPath = 
                        Path.Combine(projDir, assembly)
                        |> Path.GetFullPath

                    let outputPath =
                        Path.Combine(projDir, outputPath)
                        |> Path.GetFullPath
                        
                    Assembly.addNativeZip cancel.Token libs assemblyPath
                    //x.CopyNative(libs, outputPath)
                    x.Remapping(libs, outputPath)


                    try
                        x.Log.LogMessage($"added {libs} to {Path.GetFileName(assemblyPath)}")
                        true
                    finally
                        cancel.Dispose()
                        cancel <- null
                with _ -> 
                    false
            | None ->
                x.Log.LogMessage($"no libs found for {assemblyName}")
                true

    member x.DesignTime
        with get() = designTime
        and set d = designTime <- d

    member x.RepositoryRoot
        with get() = repoRoot
        and set r = repoRoot <- r

    [<Required>]
    member x.ProjectPath
        with get() = projectPath
        and set p = projectPath <- p

    [<Required>]
    member x.Assembly
        with get() = assembly
        and set d = assembly <- d

    [<Required>]
    member x.AssemblyName
        with get() = assemblyName
        and set d = assemblyName <- d

    [<Required>]
    member x.OutputPath
        with get() = outputPath
        and set d = outputPath <- d
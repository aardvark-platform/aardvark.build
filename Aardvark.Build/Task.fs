namespace Aardvark.Build

open System
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open System.Threading
open Aardvark.Build
open System.Xml
open System.Xml.Linq

module Tools =
    let libDirNames = ["lib"; "libs"]

    let isGitRepo (path : string) =
        try Directory.Exists (Path.Combine(path, ".git"))
        with _ -> false

    let rec findLibs (path : string) =
        let found =
            libDirNames |> List.tryPick (fun l ->   
                let p = Path.Combine(path, l, "Native")
                try if Directory.Exists p then Some p else None
                with _ -> None
            )
        match found with
        | Some path -> Some path
        | None ->  
            try
                let parent = Path.GetDirectoryName path
                if isNull parent then None
                else findLibs parent
            with _ ->   
                None


module Symlink =
    open System.Runtime.InteropServices

    module Mac =
        [<DllImport("libc")>]
        extern int symlink(string src, string linkName);

    module Linux =
        [<DllImport("libc")>]
        extern int symlink(string src, string linkName);

    let symlink (src : string) (name : string) =
        if File.Exists name then File.Delete name
        if RuntimeInformation.IsOSPlatform OSPlatform.Linux then 
            let ret = Linux.symlink(src, name)
            if ret <> 0 then Result.Error $"could not create symlink {name} {ret}"
            else Ok()
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then 
            let ret = Mac.symlink(src, name)
            if ret <> 0 then Result.Error $"could not create symlink {name} {ret}"
            else Ok()
        else 
            Error "symlinks not supported on Windows"




type NativeTask() =
    inherit Task()

    let mutable designTime = false
    let mutable assembly = ""
    let mutable assemblyName = ""
    let mutable outputPath = ""

    let mutable cancel : CancellationTokenSource = null


    member x.Remapping (srcDirectory : string, outputPath : string) =
        let ct = cancel.Token
        let file = Path.Combine(srcDirectory, "remap.xml")
        if File.Exists file then
            let d = XDocument.Load(file)
            let config = d.Element("configuration")
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
            let libs = 
                match Tools.findLibs Environment.CurrentDirectory with
                | Some dir -> 
                    let dir = Path.Combine(dir, assemblyName)
                    if Directory.Exists dir then Some dir
                    else None
                | None ->
                    None

            match libs with
            | Some libs ->

                cancel <- new CancellationTokenSource()
                try
                    let assemblyPath = 
                        Path.Combine(Environment.CurrentDirectory, assembly)
                        |> Path.GetFullPath

                    let outputPath =
                        Path.Combine(Environment.CurrentDirectory, outputPath)
                        |> Path.GetFullPath
                        
                    Assembly.addNativeZip cancel.Token libs assemblyPath
                    x.CopyNative(libs, outputPath)
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
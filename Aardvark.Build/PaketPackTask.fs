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
open System.Diagnostics

type PaketPackTask() as this =
    inherit Task()

    let mutable designTime = false
    let mutable repoRoot = ""
    let mutable assemblyPath = ""
    let mutable projectPath = ""
    let mutable config = ""
    let mutable assemblyName = ""
    let mutable outputPath = ""
    let mutable projectReferences = Array.empty<string>
    let mutable packageOutputPath = ""

    let mutable packageVersion = ""
    let mutable packageReleaseNotes = ""
    
    do Tools.boot this.Log

    member x.PackageReleaseNotes
        with get() = packageReleaseNotes
        and set d = packageReleaseNotes <- d

    [<Required>]
    member x.OutputPath
        with get() = outputPath
        and set p = outputPath <- p

    [<Required>]
    member x.PackageVersion
        with get() = packageVersion
        and set p = packageVersion <- p

    [<Required>]
    member x.AssemblyName
        with get() = assemblyName
        and set n = assemblyName <- n

    [<Required>]
    member x.Configuration
        with get() = config
        and set c = config <- c

    member x.RepositoryRoot
        with get() = repoRoot
        and set r = repoRoot <- r

    member x.DesignTime
        with get() = designTime
        and set d = designTime <- d

    [<Required>]        
    member x.Assembly
        with get() = assemblyPath
        and set d = assemblyPath <- d
         
    [<Required>]
    member x.ProjectPath
        with get() = projectPath
        and set d = projectPath <- d
         
    [<Required>]
    member x.PackageOutputPath
        with get() = packageOutputPath
        and set d = packageOutputPath <- d

    member x.ProjectReferences
        with get() = projectReferences
        and set d = projectReferences <- d

    override x.Execute() =
        if designTime then
            true
        else
            let projDir = Path.GetDirectoryName projectPath
        
            let runPaket fmt  =
                fmt |> Printf.kprintf (fun command ->
                    let proc = ProcessStartInfo("dotnet")
                    proc.Arguments <- sprintf "paket %s" command
                    proc.UseShellExecute <- false
                    proc.CreateNoWindow <- true
                    proc.RedirectStandardOutput <- true
                    proc.RedirectStandardError <- true

                    let p = Process.Start proc
                    p.OutputDataReceived.Add (fun e ->
                        if not (isNull e.Data) then
                            x.Log.LogMessage(MessageImportance.Normal, e.Data)
                    )

                    p.ErrorDataReceived.Add (fun e ->
                        if not (isNull e.Data) then
                            x.Log.LogError(e.Data)
                    )
                    p.BeginOutputReadLine()
                    p.BeginErrorReadLine()

                    p.WaitForExit()
                    if p.ExitCode <> 0 then
                        x.Log.LogError(sprintf "Paket exited with code %d" p.ExitCode)
                        false
                    else
                        true
                )



            let packageOutputPath = Path.GetFullPath packageOutputPath
            let template = Path.Combine(projDir, "paket.template")
            if File.Exists template then

                if runPaket "--version" then

                    let result =
                        runPaket "pack --silent --interproject-references fix --template \"%s\" --release-notes \"%s\" --version %s --build-config %s \"%s\"" template packageReleaseNotes packageVersion config packageOutputPath

                    if result then
                        let packageId =  
                            let customId = 
                                let nameRx = System.Text.RegularExpressions.Regex @"^id[ \t]+(.*)$"
                                File.ReadAllLines template
                                |> Array.tryPick (fun line ->
                                    let m = nameRx.Match line
                                    if m.Success then Some m.Groups.[1].Value
                                    else None
                                )
                            match customId with
                            | Some id -> id
                            | None -> Path.GetFileNameWithoutExtension projectPath

                        let packageName = sprintf "%s.%s.nupkg" packageId packageVersion 
                        let message =
                            let p = Path.Combine(packageOutputPath, packageName)
                            sprintf "Packed %s" p

                        x.Log.LogMessage(MessageImportance.High, message)
                        true
                    else
                        false
                else
                    false
            else
                true
         

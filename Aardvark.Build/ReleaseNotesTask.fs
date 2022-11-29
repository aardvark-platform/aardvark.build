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


// special tpye to wrap the execute method to make the task robust against missing method exceptions etc.
type ReleaseNotesTaskImpl() =

    static member Execute(task : ReleaseNotesTask) =
        if task.DesignTime then
            true
        else
            let projDir = Path.GetDirectoryName task.ProjectPath
            let root =
                if System.String.IsNullOrWhiteSpace task.RepositoryRoot then Tools.findProjectRoot projDir
                elif Directory.Exists task.RepositoryRoot then Some task.RepositoryRoot
                else None

            match root with
            | Some rootDir ->
                let releaseNotes =
                    let path = 
                        Directory.GetFiles(rootDir, "*") |> Array.tryFind Tools.isReleaseNotesFile

                    match path with
                    | Some path ->  
                        try Some (Fake.Core.ReleaseNotes.load path)
                        with _ -> None
                    | None ->
                        None

                match releaseNotes with
                | Some n -> 
                    task.NugetVersion <- n.NugetVersion
                    task.AssemblyVersion <- sprintf "%d.%d.0.0" n.SemVer.Major n.SemVer.Minor
                    task.ReleaseNotes <- n.Notes |> String.concat "\n" 
                    true
                | None ->
                    task.Log.LogWarning "No release notes found: version will be 1.0.0.0. consider adding a RELEASE_NOTES.md to your repository root."
                    task.NugetVersion <- "1.0.0.0"
                    task.AssemblyVersion <- "1.0.0.0"
                    task.ReleaseNotes <- ""
                    true
            | None ->
                task.Log.LogWarning "Could not find repository root (please specify RepositoryRoot Property)"   
                true

and ReleaseNotesTask() as this =
    inherit Task()


    let mutable designTime = false
    let mutable projectPath = ""
    let mutable nugetVersion = ""
    let mutable assemblyVersion = ""
    let mutable repoRoot = ""
    let mutable notes = ""
    let mutable attachDebuggerOnError = false

    do Tools.boot this.Log

    member x.RepositoryRoot
        with get() = repoRoot
        and set r = repoRoot <- r

    member x.DesignTime
        with get() = designTime
        and set d = designTime <- d

    member x.AttachDebuggerOnError
        with get() = attachDebuggerOnError
        and set d = attachDebuggerOnError <- d
         
    [<Required>]
    member x.ProjectPath
        with get() = projectPath
        and set p = projectPath <- p

    [<Output>]
    member x.NugetVersion
        with get() = nugetVersion
        and set d = nugetVersion <- d

    [<Output>]
    member x.AssemblyVersion
        with get() = assemblyVersion
        and set d = assemblyVersion <- d

    [<Output>]
    member x.ReleaseNotes
        with get() = notes
        and set d = notes <- d

    override x.Execute() =  
        if designTime then 
            true
        else 
            try
                ReleaseNotesTaskImpl.Execute(x)
            with e -> 
                try
                    x.Log.LogWarning "ReleaseNotesTaskImpl failed." 
                    x.Log.LogWarning (sprintf "ReleaseNotesTaskImpl failed: %A" e) 
                with e -> 
                    // logging failed. no idea what to do... fails anyways.
                    ()
                if x.AttachDebuggerOnError then 
                    System.Diagnostics.Debugger.Launch() |> ignore
                    System.Diagnostics.Debugger.Break()
                    false
                else    
                    false
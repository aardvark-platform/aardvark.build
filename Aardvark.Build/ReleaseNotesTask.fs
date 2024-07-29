namespace Aardvark.Build

open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open Aardvark.Build
open System.Diagnostics


// special type to wrap the execute method to make the task robust against missing method exceptions etc. (See https://github.com/aardvark-platform/aardvark.build/issues/3)
type ReleaseNotesTaskImpl() =

    static member Execute(task : ReleaseNotesTask) =
        if task.DesignTime then
            true
        else
            let path : string = task.ProjectPath // workaround for type inference problem
            let projDir = Path.GetDirectoryName(path)

            let root =
                if not <| System.String.IsNullOrWhiteSpace task.RepositoryRoot && Directory.Exists task.RepositoryRoot then
                    Some task.RepositoryRoot
                else
                    None

            let releaseNotes =
                let path = 
                    match root with
                    | Some rootDir -> Directory.GetFiles(rootDir, "*") |> Array.tryFind Tools.isReleaseNotesFile
                    | _ -> Tools.findReleaseNotesFile projDir

                match path with
                | Some path ->  
                    try 
                        //ReleaseNotes.StandaloneImpl.parseReleaseNotes path 
                        let releaseNotes = Fake.Core.ReleaseNotes.load path
                        let nugetVersion = releaseNotes.NugetVersion
                        let assemblyVersion = sprintf "%d.%d.0.0" releaseNotes.SemVer.Major releaseNotes.SemVer.Minor
                        let notes = releaseNotes.Notes |> String.concat "\n" 
                        (nugetVersion, assemblyVersion, notes) |> Some
                    with e -> 
                        task.Log.LogWarning (sprintf "could not parse release notes, using version 1.0.0.0 as a fallback. The exception was: %A" e)
                        if task.AttachDebuggerOnError then 
                            Debugger.Launch() |> ignore
                            Debugger.Break()
                            None
                        else 
                            None
                | None ->
                    None

            match releaseNotes with
            | Some (nugetVersion, assemblyVersion, notes) -> 
                task.NugetVersion <- nugetVersion
                task.AssemblyVersion <- assemblyVersion
                task.ReleaseNotes <- notes
                true
            | None ->
                task.Log.LogWarning "No release notes found: version will be 1.0.0.0. consider adding a RELEASE_NOTES.md to your repository root."
                task.NugetVersion <- "1.0.0.0"
                task.AssemblyVersion <- "1.0.0.0"
                task.ReleaseNotes <- ""
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
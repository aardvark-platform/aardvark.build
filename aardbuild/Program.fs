﻿open Paket.Core
open Fake.Core
open Fake.DotNet
open System.IO
open System
open System.Text.RegularExpressions
open Fake.Tools

module Log =
    let mutable private indent = ""

    let private consoleColorSupported =
        let o = Console.ForegroundColor
        try
            Console.ForegroundColor <- ConsoleColor.Yellow
            Console.ForegroundColor = ConsoleColor.Yellow
        finally
            Console.ForegroundColor <- o

    let start fmt =
        fmt |> Printf.kprintf (fun str -> 
            Console.WriteLine("{0}{1}", indent, str)
            indent <- indent + "  "
        )

    let stop() =
        if indent.Length >= 2 then indent <- indent.Substring(0, indent.Length - 2)
        else indent <- ""

    let line fmt =
        fmt |> Printf.kprintf (fun str -> 
            Console.WriteLine("{0}{1}", indent, str)
        )
        
    let warn fmt =
        fmt |> Printf.kprintf (fun str -> 
            let c = Console.ForegroundColor
            try
                Console.ForegroundColor <- ConsoleColor.Yellow
                Console.WriteLine("\u001b[1;33m{0}WRN {1}", indent, str)
            finally
                Console.ForegroundColor <- c
        )


    let error fmt =
        fmt |> Printf.kprintf (fun str -> 
            let c = Console.ForegroundColor
            try
                Console.ForegroundColor <- ConsoleColor.Red
                Console.WriteLine("\u001b[1;31m{0}ERR {1}", indent, str)
            finally
                Console.ForegroundColor <- c
        )

    
type Path with
    static member Relative(path : string, ?baseDir : string) =
        let baseDir = defaultArg baseDir Environment.CurrentDirectory |> Path.GetFullPath
        let path = Path.GetFullPath path
        if path.StartsWith baseDir then 
            let rest = path.Substring(baseDir.Length).TrimStart([|Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar|])
            rest
        else
            path

[<EntryPoint>]
let main args =
    let sw = System.Diagnostics.Stopwatch.StartNew()
    do
        let ctx = Context.FakeExecutionContext.Create false "build.fsx" (Array.toList args)
        Context.setExecutionContext (Context.RuntimeContext.Fake ctx)

        CoreTracing.setTraceListenersPrivate [
            { new ITraceListener with
                member x.Write data =
                    match data with
                    | TraceData.OpenTag(tag, m) ->
                        match tag with
                        | KnownTags.Task _ -> ()
                        | _ -> Log.start "%s" tag.Name 
                    | TraceData.CloseTag _ ->
                        Log.stop()
                    | TraceData.LogMessage(m, _) -> 
                        Log.line "%s" m
                    | TraceData.ImportantMessage m ->
                        Log.warn "%s" m
                    | TraceData.ErrorMessage m ->
                        Log.error "%s" m
                    | TraceData.TraceMessage(m,_) ->
                        Log.line "%s" m
                        ()
                    | _ ->  
                        ()
            }
        ]

    let workdir =
        args 
        |> Array.tryFind (fun p -> not (p.StartsWith "-") && Directory.Exists p)
        |> Option.defaultValue Environment.CurrentDirectory
    
    let dependenciesPath = 
        Path.Combine(workdir, "paket.dependencies")

    let releaseNotesPath =
        Directory.GetFiles(workdir, "*") 
        |> Array.tryFind (fun p -> Path.GetFileNameWithoutExtension(p).ToLower().Trim().Replace("_", "") = "releasenotes")
        |> Option.defaultValue (Path.Combine(workdir, "RELEASE_NOTES.md"))

    try
        Log.start "DotNet:Build"
        workdir |> DotNet.build (fun o ->
            { o with    
                NoLogo = true
                Configuration = DotNet.BuildConfiguration.Release
                Common = { o.Common with Verbosity = Some DotNet.Verbosity.Minimal; RedirectOutput = true }
            }
        )
        Log.stop()


        Log.start "Paket:Pack"

        let releaseNotes =
            if File.Exists releaseNotesPath then
                try ReleaseNotes.load releaseNotesPath |> Some
                with e ->
                    Log.warn "could not parse %s" (Path.Relative(releaseNotesPath, workdir))
                    None
            else
                Log.warn "could not find release notes"
                None



        if File.Exists dependenciesPath && File.Exists releaseNotesPath  then
            
            let version, releaseNotes =
                match releaseNotes with
                | Some notes -> 
                    notes.NugetVersion, notes.Notes
                | None ->   
                    Log.warn "using version 0.0.0.0"
                    "0.0.0.0", []
            let projectUrl = None //Some "http://github.com"
            let outputPath = Path.Combine(workdir, "bin", "pack")
            

            let deps = 
                try Paket.Dependencies(dependenciesPath)
                with e ->   
                    Log.error "paket error: %A" e
                    reraise()

            let templates =
                let templateFiles =
                    Directory.GetFiles(workdir, "*.template", SearchOption.AllDirectories)
                let parsed = 
                    deps.ListTemplateFiles()
                    |> List.map (fun f -> Path.GetFullPath f.FileName, f)
                    |> Map.ofList
                
                templateFiles |> Array.choose (fun p ->
                    match Map.tryFind p parsed with
                    | Some f -> Some f
                    | None ->
                        Log.warn "could not parse %A" (Path.Relative(p, workdir))
                        None
                )

            for f in templates do
                let packageId  = 
                    match f.Contents with
                    | Paket.TemplateFileContents.CompleteInfo(info, opt) -> 
                        info.Id
                    | Paket.TemplateFileContents.ProjectInfo(info, opt) -> 
                        match info.Id with
                        | Some id -> id
                        | None -> 
                            let proj = Directory.GetFiles(Path.GetDirectoryName f.FileName, "*.*proj") |> Array.head |> Path.GetFileNameWithoutExtension
                            proj

                deps.Pack(
                    Path.Combine(workdir, "bin", "pack"),
                    version = version,
                    releaseNotes = String.concat "\r\n" releaseNotes,
                    buildConfig = "Release",
                    interprojectReferencesConstraint = Some Paket.InterprojectReferencesConstraint.InterprojectReferencesConstraint.Fix,
                    ?projectUrl = projectUrl
                )

                let packageFile =
                    Path.Combine(outputPath, packageId + "." + version + ".nupkg")
                
                Log.line "packed %s" (Path.Relative(packageFile, workdir))

        Log.stop()

        Log.start "Git:Tag"
        match releaseNotes with
        | Some notes ->
            if Directory.Exists(Path.Combine(workdir, ".git")) then
                try Git.CommandHelper.directRunGitCommandAndFail workdir (sprintf "tag -m %s %s" notes.NugetVersion notes.NugetVersion)
                with _ -> Log.warn "tag %A already exists" notes.NugetVersion
            else
                Log.warn "cannot tag, not a git repository"

        | None ->   
            Log.warn "no version"
        Log.stop()

        Log.line "finished in %A" sw.Elapsed


        0
    with e ->   
        Log.error "unknown error: %A" e
        -1
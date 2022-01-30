open Paket.Core
open Fake.Core
open Fake.DotNet
open System.IO
open System
open System.Text.RegularExpressions
open Fake.Tools
open Fake.Api

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
                Console.WriteLine("{0}WRN {1}", indent, str)
                //Console.WriteLine("\u001b[1;33m{0}WRN {1}", indent, str)
            finally
                Console.ForegroundColor <- c
        )


    let error fmt =
        fmt |> Printf.kprintf (fun str -> 
            let c = Console.ForegroundColor
            try
                Console.ForegroundColor <- ConsoleColor.Red
                Console.WriteLine("{0}ERR {1}", indent, str)
                //Console.WriteLine("\u001b[1;31m{0}ERR {1}", indent, str)
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
    
    let files =
        args 
        |> Array.filter (fun p -> not (p.StartsWith "-") && File.Exists p)
        |> Array.toList

    let files =
        match files with
        | [] -> [workdir]
        | f -> f


    let dependenciesPath = 
        Path.Combine(workdir, "paket.dependencies")

    let releaseNotesPath =
        Directory.GetFiles(workdir, "*") 
        |> Array.tryFind (fun p -> Path.GetFileNameWithoutExtension(p).ToLower().Trim().Replace("_", "") = "releasenotes")
        |> Option.defaultValue (Path.Combine(workdir, "RELEASE_NOTES.md"))

    try
        Log.start "DotNet:Build"
        for f in files do
            Log.start "%s" (Path.Relative(f, workdir))
            f |> DotNet.build (fun o ->
                { o with    
                    NoLogo = true
                    Configuration = DotNet.BuildConfiguration.Release
                    Common = { o.Common with Verbosity = Some DotNet.Verbosity.Minimal; RedirectOutput = true }
                }
            )
            Log.stop()
        Log.stop()

        
        let githubInfo = 
            let (ret, a, b) = Git.CommandHelper.runGitCommand workdir "remote get-url origin"
            if ret then
                match a with
                | [url] -> 
                    let sshRx = Regex @"([a-zA-Z0-9_]+)@github.com:([^/]+)/([^/]+).git"
                    let m = sshRx.Match url
                    if m.Success then
                        Some (m.Groups.[2].Value, m.Groups.[3].Value)
                    else
                        None

                | _ ->
                    None
            else
                None


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



        let outputPath = Path.Combine(workdir, "bin", "pack")
        if File.Exists dependenciesPath && File.Exists releaseNotesPath  then
            
            let version, releaseNotes =
                match releaseNotes with
                | Some notes -> 
                    notes.NugetVersion, notes.Notes
                | None ->   
                    Log.warn "using version 0.0.0.0"
                    "0.0.0.0", []
            let projectUrl =
                githubInfo |> Option.map (fun (user, repo) -> 
                    sprintf "https://github.com/%s/%s/" user repo
                )
            

            let deps = 
                try Paket.Dependencies(dependenciesPath)
                with e ->   
                    Log.error "paket error: %A" e
                    reraise()

            deps.Pack(
                Path.Combine(workdir, "bin", "pack"),
                version = version,
                releaseNotes = String.concat "\r\n" releaseNotes,
                buildConfig = "Release",
                interprojectReferencesConstraint = Some Paket.InterprojectReferencesConstraint.InterprojectReferencesConstraint.Fix,
                ?projectUrl = projectUrl
            )

            let templateFiles =
                Directory.GetFiles(workdir, "*.template", SearchOption.AllDirectories)

            let packages =
                Directory.GetFiles(outputPath, "*.nupkg")

            let packageNameRx = Regex @"^(.*?)\.([0-9]+\.[0-9]+.*)\.nupkg$"

            for path in packages do
                let m = packageNameRx.Match (Path.GetFileName path)
                if m.Success then
                    let id = m.Groups.[1].Value.Trim()
                    let v = m.Groups.[2].Value.Trim()
                    if v = version then
                        Log.line "packed %s (%s)" id v
                    else
                        try File.Delete path
                        with _ -> ()
            
        Log.stop()

        
        let token = Environment.GetEnvironmentVariable "GITHUB_TOKEN"
        if not (isNull token) then
            Log.start "Git:Tag"
            let tagIsNew = 
                match releaseNotes with
                | Some notes ->
                    if Directory.Exists(Path.Combine(workdir, ".git")) then
                        try 
                            Git.CommandHelper.directRunGitCommandAndFail workdir (sprintf "tag -m %s %s" notes.NugetVersion notes.NugetVersion)
                            true
                        with _ -> 
                            Log.warn "tag %A already exists" notes.NugetVersion
                            false
                    else
                        Log.warn "cannot tag, not a git repository"
                        false

                | None ->   
                    Log.warn "no version"
                    false

            Log.stop()


            if tagIsNew then
                Log.start "Github:Release"
                async {
                    match githubInfo, releaseNotes with
                    | Some (user, repo), Some notes ->
                        let client = GitHub.createClientWithToken token
                        let rel = GitHub.draftNewRelease user repo notes.NugetVersion notes.SemVer.PreRelease.IsSome notes.Notes client

                        let mutable rel = rel
                        for pkg in Directory.GetFiles(outputPath, "*.nupkg") do
                            rel <- GitHub.uploadFile pkg rel

                        do! GitHub.publishDraft rel
                    | _ ->  
                        ()

                } |> Async.RunSynchronously
                Log.stop()
            else
                Log.warn "not publishing, unchanged version"
        else
            Log.warn "not publishing, no github token"

        Log.line "finished in %A" sw.Elapsed


        0
    with e ->   
        Log.error "unknown error: %A" e
        -1

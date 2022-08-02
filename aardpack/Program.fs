open Paket.Core
open Fake.Core
open Fake.DotNet
open System.IO
open System
open System.Text.RegularExpressions
open Fake.Tools
open Fake.Api
open System.Threading.Tasks

module Log =
    let mutable private indent = ""

    let private out = Console.Out

    let private consoleColorSupported =
    
        let o = Console.ForegroundColor
        try
            Console.ForegroundColor <- ConsoleColor.Yellow
            Console.ForegroundColor = ConsoleColor.Yellow
        finally
            Console.ForegroundColor <- o

    let start fmt =
        fmt |> Printf.kprintf (fun str -> 
            out.WriteLine("> {0}{1}", indent, str)
            indent <- indent + "  "
        )

    let stop() =
        if indent.Length >= 2 then indent <- indent.Substring(0, indent.Length - 2)
        else indent <- ""

    let line fmt =
        fmt |> Printf.kprintf (fun str -> 
            out.WriteLine("> {0}{1}", indent, str)
        )
        
    let warn fmt =
        fmt |> Printf.kprintf (fun str -> 
            let c = Console.ForegroundColor
            try
                Console.ForegroundColor <- ConsoleColor.Yellow
                out.WriteLine("> {0}WRN {1}", indent, str)
                //Console.WriteLine("\u001b[1;33m{0}WRN {1}", indent, str)
            finally
                Console.ForegroundColor <- c
        )


    let error fmt =
        fmt |> Printf.kprintf (fun str -> 
            let c = Console.ForegroundColor
            try
                Console.ForegroundColor <- ConsoleColor.Red
                out.WriteLine("> {0}ERR {1}", indent, str)
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

type ObservableTextWriter() =
    inherit TextWriter()

    let mutable newline = Environment.NewLine
    let mutable currentLine = new StringWriter()

    let event = Event<string>()

    interface IObservable<string> with
        member x.Subscribe obs = event.Publish.Subscribe obs

    override this.Close() = ()
    override this.Dispose _ = ()
    override this.DisposeAsync() = ValueTask.CompletedTask
    override this.Encoding = System.Text.Encoding.UTF8
    override this.Flush() = ()
    override this.FlushAsync() = Task.FromResult () :> Task
    override this.FormatProvider = System.Globalization.CultureInfo.InvariantCulture
    override this.NewLine
        with get () = newline
        and set v = newline <- v
    override this.Write(value: bool) = currentLine.Write value
    override this.Write(value: char) = currentLine.Write value
    override this.Write(buffer: char[]) = currentLine.Write buffer
    override this.Write(buffer: char[], index: int, count: int) = currentLine.Write(buffer, index, count)
    override this.Write(value: decimal) = currentLine.Write value
    override this.Write(value: float) = currentLine.Write value
    override this.Write(value: int) = currentLine.Write value
    override this.Write(value: int64) = currentLine.Write value
    override this.Write(value: obj) = currentLine.Write value
    override this.Write(buffer: ReadOnlySpan<char>) = currentLine.Write buffer
    override this.Write(value: float32) = currentLine.Write value
    override this.Write(value: string) = currentLine.Write value
    override this.Write(format: string, arg0: obj) = currentLine.Write(format, arg0)
    override this.Write(format: string, arg0: obj, arg1: obj) = currentLine.Write(format, arg0, arg1)
    override this.Write(format: string, arg0: obj, arg1: obj, arg2: obj) = currentLine.Write(format, arg0, arg1, arg2)
    override this.Write(format: string, arg: obj[]) = currentLine.Write(format, arg)
    override this.Write(value: Text.StringBuilder) = currentLine.Write(value)
    override this.Write(value: uint32) = currentLine.Write(value)
    override this.Write(value: uint64) = currentLine.Write(value)
    override this.WriteAsync(value: char) = currentLine.WriteAsync(value)
    override this.WriteAsync(buffer: char[], index: int, count: int) = 
        let str = System.String(buffer, index, count)
        currentLine.WriteAsync(buffer, index, count)
    override this.WriteAsync(buffer: ReadOnlyMemory<char>, cancellationToken: Threading.CancellationToken) = currentLine.WriteAsync(buffer, cancellationToken)
    override this.WriteAsync(value: string) = currentLine.WriteAsync(value)
    override this.WriteAsync(value: Text.StringBuilder, cancellationToken: Threading.CancellationToken) = currentLine.WriteAsync(value, cancellationToken)
    override this.WriteLine() =
        currentLine.ToString() |> event.Trigger
        currentLine.Dispose()
        currentLine <- new StringWriter()

    override this.WriteLine(value: bool) = this.Write value; this.WriteLine()
    override this.WriteLine(value: char) = this.Write value; this.WriteLine()
    override this.WriteLine(buffer: char[]) = this.Write buffer; this.WriteLine()
    override this.WriteLine(buffer: char[], index: int, count: int) = this.Write(buffer, index, count); this.WriteLine()
    override this.WriteLine(value: decimal) = this.Write value; this.WriteLine()
    override this.WriteLine(value: float) = this.Write value; this.WriteLine()
    override this.WriteLine(value: int) = this.Write value; this.WriteLine()
    override this.WriteLine(value: int64) = this.Write value; this.WriteLine()
    override this.WriteLine(value: obj) = this.Write value; this.WriteLine()
    override this.WriteLine(buffer: ReadOnlySpan<char>) = this.Write buffer; this.WriteLine()
    override this.WriteLine(value: float32) = this.Write value; this.WriteLine()
    override this.WriteLine(value: string) = this.Write value; this.WriteLine()
    override this.WriteLine(format: string, arg0: obj) = this.Write(format, arg0); this.WriteLine()
    override this.WriteLine(format: string, arg0: obj, arg1: obj) = this.Write(format, arg0, arg1); this.WriteLine()
    override this.WriteLine(format: string, arg0: obj, arg1: obj, arg2: obj) = this.Write(format, arg0, arg1, arg2); this.WriteLine()
    override this.WriteLine(format: string, arg: obj[]) = this.Write(format, arg); this.WriteLine()
    override this.WriteLine(value: Text.StringBuilder) = this.Write(value); this.WriteLine()
    override this.WriteLine(value: uint32) = this.Write(value); this.WriteLine()
    override this.WriteLine(value: uint64) = this.Write(value); this.WriteLine()
    override this.WriteLineAsync() = this.WriteLine(); Task.FromResult () :> Task
    override this.WriteLineAsync(value: char) = this.WriteLine(value); Task.FromResult () :> Task
    override this.WriteLineAsync(buffer: char[], index: int, count: int) = this.WriteLine(buffer, index, count); Task.FromResult () :> Task
    override this.WriteLineAsync(buffer: ReadOnlyMemory<char>, cancellationToken: Threading.CancellationToken) = this.WriteAsync(buffer, cancellationToken) |> ignore; Task.FromResult () :> Task
    override this.WriteLineAsync(value: string) = this.WriteLine(value); Task.FromResult () :> Task
    override this.WriteLineAsync(value: Text.StringBuilder, cancellationToken: Threading.CancellationToken) = this.WriteLineAsync(value, cancellationToken) |> ignore; Task.FromResult () :> Task

let sshRx = Regex @"([a-zA-Z0-9_]+)@github.com:([^/]+)/([^/]+)\.git$"
let httpsRx = Regex @"http(s)?://github.com/([^/]+)/([^/]+)$"

type MyWriter() =
    inherit TextWriter()

    let evt = Event<string>()

    interface IObservable<string> with
        member x.Subscribe obs = evt.Publish.Subscribe obs


    override x.Encoding = System.Text.Encoding.UTF8

    override x.Write(buffer : char[], index : int, count : int) =
        let str = System.String(buffer, index, count)
        for line in str.Split(x.NewLine) do
            evt.Trigger(line)


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

    let doBuild =
        args |> Array.forall (fun a -> a <> "--nobuild")

    let createTag =
        args |> Array.forall (fun a -> a <> "--notag")

    let createRelease =
        args |> Array.forall (fun a -> a <> "--norelease")

    let dependenciesPath = 
        Path.Combine(workdir, "paket.dependencies")

    let releaseNotesPath =
        Directory.GetFiles(workdir, "*") 
        |> Array.tryFind (fun p -> Path.GetFileNameWithoutExtension(p).ToLower().Trim().Replace("_", "") = "releasenotes")
        |> Option.defaultValue (Path.Combine(workdir, "RELEASE_NOTES.md"))

    try
        if doBuild then 
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
                    let m = sshRx.Match url
                    if m.Success then
                        Some (m.Groups.[2].Value, m.Groups.[3].Value)
                    else
                        let m = httpsRx.Match url
                        if m.Success then
                            Some (m.Groups.[2].Value, m.Groups.[3].Value)
                        else
                            None

                | _ ->
                    None
            else
                None

        match githubInfo with
        | Some(user,repo) ->    
            Log.line "organization: %s" user
            Log.line "repository:   %s" repo
        | None ->
            Log.line "not a github repository"

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

        if doBuild && File.Exists dependenciesPath && File.Exists releaseNotesPath  then
            
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
            if createTag then
                Log.start "Git:Tag"

                Git.CommandHelper.directRunGitCommandAndFail workdir "config --local user.name \"aardvark-platform\""
                Git.CommandHelper.directRunGitCommandAndFail workdir "config --local user.email \"admin@aardvarkians.com\""

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

                if tagIsNew then
                    Git.CommandHelper.directRunGitCommandAndFail workdir "push --tags"

                Log.stop()


            if createRelease then
                Log.start "Github:Release"


                let hash = 
                    match Git.CommandHelper.runGitCommand workdir "rev-parse HEAD" with
                    | (true, [hash], _) -> Some hash
                    | _ -> None

                match githubInfo, releaseNotes with
                | Some (user, repo), Some notes ->
                    let packages = if doBuild then Directory.GetFiles(outputPath, "*.nupkg") else (files |> List.toArray)
                    Log.start "%d files" packages.Length
                    for file in packages do
                        Log.line "%s" (Path.Relative(file, workdir))
                    Log.stop()


                    let oo = Console.Out
                    let oe = Console.Error
                    use o = new ObservableTextWriter()
                    use e = new ObservableTextWriter()

                    o.Add (fun l ->
                        Log.line "%s" l
                    )
                    
                    e.Add (fun l ->
                        Log.error "%s" l
                    )

                    try
                        Console.SetOut o
                        Console.SetError e
                        try
                            let client = GitHub.createClientWithToken token

                            let oldRelease =
                                try
                                    GitHub.getReleaseByTag user repo notes.NugetVersion client
                                    |> Async.RunSynchronously
                                    |> Some
                                with _ ->
                                    None

                            match oldRelease with
                            | Some _rel ->   
                                Log.warn "release %s already exists" notes.NugetVersion
                            | None ->   
                                client
                                |> GitHub.createRelease user repo notes.NugetVersion (fun p ->
                                    { 
                                        Name = notes.NugetVersion
                                        TargetCommitish = match hash with | Some h -> h | None -> p.TargetCommitish
                                        Draft = true
                                        Prerelease = notes.SemVer.PreRelease |> Option.isSome
                                        Body = String.concat "\r\n" notes.Notes
                                    }
                                )
                                |> GitHub.uploadFiles packages
                                |> GitHub.publishDraft
                                |> Async.RunSynchronously
                        finally
                            Console.SetOut oo
                            Console.SetError oe
                    with e ->
                        Log.error "failed: %A" e
                        exit -1
                | _ ->  
                    ()

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

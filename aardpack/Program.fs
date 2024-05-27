﻿open Paket.Core
open Fake.Core
open Fake.DotNet
open System.IO
open System
open System.Text.RegularExpressions
open System.Collections.Generic
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

[<Literal>]
let InvalidVersion = "0.0.0.0"

type PackTarget =
    { Path         : string
      Version      : string
      Prerelease   : bool
      ReleaseNotes : string list
      TemplateFile : string option
      ProjectName  : string option }

    member x.HasVersion =
        x.Version <> InvalidVersion

    member x.GetOutputPath(outputPath: string) =
        Path.Combine(outputPath, x.ProjectName |> Option.defaultValue "")

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

    let hasArgument (name: string) =
        args |> Array.exists ((=) $"--{name}")

    // If --no-build is specified, build & pack steps will be skipped and the files in arguments will be added to the release directly.
    let doBuild =
        not <| (hasArgument "nobuild" || hasArgument "no-build")

    // Note: Github release will create a tag regardless.
    let createTag =
        not <| (hasArgument "notag" || hasArgument "no-tag")

    // Do not actually create tags and push packages
    let dryRun =
        hasArgument "dry-run" || hasArgument "dryrun"

    // Create a tag and release for each project file specified as argument
    // E.g. Aardvark.Base.csproj results in a tag aardvark.base/1.2.3.4 and release titled Aardvark.Base - 1.2.3.4
    let perProject =
        doBuild && (hasArgument "per-project" || hasArgument "perproject")

    let createRelease =
        not <| (hasArgument "norelease" || hasArgument "no-release")

    let parseOnly =
        hasArgument "parseonly" || hasArgument "parse-only"

    let showVersion =
        hasArgument "version"

    let dependenciesPath =
        Path.Combine(workdir, "paket.dependencies")

    let tryFindReleaseNotes (quiet: bool) (directory: string) =
        let rec doit (current: string) =
            let filePath =
                Directory.GetFiles(current, "*")
                |> Array.tryFind (fun p -> Path.GetFileNameWithoutExtension(p).ToLower().Trim().Replace("_", "") = "releasenotes")

            if filePath.IsSome then filePath
            else
                let parent = Directory.GetParent current
                if isNull parent then
                    if not quiet then
                        Log.warn "failed to find releases notes for '%s'" directory
                    None
                else
                    doit parent.FullName

        doit directory

    let tryParseReleaseNotes =
        let cache = Dictionary<string, ReleaseNotes.ReleaseNotes>()

        fun (quiet: bool) (path: string) ->
            let fi = FileInfo path

            if fi.Exists then
                match cache.TryGetValue fi.FullName with
                | (true, notes) -> Some notes
                | _ ->
                    try
                        let notes = ReleaseNotes.load path
                        cache.[fi.FullName] <- notes
                        Some notes

                    with _ ->
                        if not quiet then
                            Log.warn "could not parse '%s'" (Path.Relative(path, workdir))
                        None
            else
                if not quiet then
                    Log.warn "could not find release notes in '%s'" (Path.Relative(path, workdir))
                None

    if showVersion then
        let asm = System.Reflection.Assembly.GetExecutingAssembly()

        let version =
            asm.GetCustomAttributes(true)
            |> Array.tryPick (function
                | :? System.Reflection.AssemblyInformationalVersionAttribute as att -> Some att.InformationalVersion
                | _ -> None
            )
            |> Option.defaultWith (fun _ ->
                string <| asm.GetName().Version
            )

        printfn "aardpack %s" version
        0

    elif parseOnly then
        let releaseNotes =
            tryFindReleaseNotes true workdir
            |> Option.bind (tryParseReleaseNotes true)

        match releaseNotes with
        | Some notes ->
            printfn "%s" notes.NugetVersion
        | None ->
            printfn "%s" InvalidVersion
        0

    else
        try
            if doBuild then
                Log.start "DotNet:Build"
                for f in files do
                    Log.start "%s" (Path.Relative(f, workdir))
                    f |> DotNet.build (fun o ->
                        { o with
                            NoLogo = true
                            MSBuildParams = { o.MSBuildParams with DisableInternalBinLog = true } // https://github.com/fsprojects/FAKE/issues/2595
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

            let targets =
                files |> List.map (fun f ->
                    let dir = Path.GetDirectoryName f

                    let version, prerelease, notes =
                        let releaseNotes =
                            dir
                            |> tryFindReleaseNotes false
                            |> Option.bind (tryParseReleaseNotes false)

                        match releaseNotes with
                        | Some notes ->
                            notes.NugetVersion,
                            notes.SemVer.PreRelease |> Option.isSome,
                            notes.Notes
                        | _ ->
                            InvalidVersion, false, []

                    let projectName =
                        match Path.GetExtension f with
                        | ".fsproj" | ".csproj" -> Some <| Path.GetFileNameWithoutExtension f
                        | _ -> None

                    let template =
                        let template = Path.Combine(dir, "paket.template")
                        if File.Exists template then Some template
                        else None

                    { Path         = f
                      Version      = version
                      Prerelease   = prerelease
                      ReleaseNotes = notes
                      TemplateFile = template
                      ProjectName  = projectName }
                )

            let outputPath = Path.Combine(workdir, "bin", "pack")

            if doBuild && File.Exists dependenciesPath then
                let projectUrl =
                    githubInfo |> Option.map (fun (user, repo) ->
                        sprintf "https://github.com/%s/%s/" user repo
                    )

                let deps =
                    try Paket.Dependencies(dependenciesPath)
                    with e ->
                        Log.error "paket error: %A" e
                        reraise()

                for target in targets do
                    let outputPath =
                        if perProject then target.GetOutputPath outputPath
                        else outputPath

                    deps.Pack(
                        outputPath,
                        version = target.Version,
                        releaseNotes = String.concat "\r\n" target.ReleaseNotes,
                        buildConfig = "Release",
                        interprojectReferencesConstraint = Some Paket.InterprojectReferencesConstraint.InterprojectReferencesConstraint.Fix,
                        ?projectUrl = projectUrl,
                        ?templateFile = target.TemplateFile
                    )

                    let packages =
                        Directory.GetFiles(outputPath, "*.nupkg")

                    let packageNameRx = Regex @"^(.*?)\.([0-9]+\.[0-9]+.*)\.nupkg$"

                    for path in packages do
                        let m = packageNameRx.Match (Path.GetFileName path)
                        if m.Success then
                            let id = m.Groups.[1].Value.Trim()
                            let v = m.Groups.[2].Value.Trim()
                            if v = target.Version then
                                Log.line "packed %s (%s)" id v
                            else
                                try File.Delete path
                                with _ -> ()

            Log.stop()

            let token = Environment.GetEnvironmentVariable "GITHUB_TOKEN"
            if dryRun || not (isNull token) then
                let globalReleaseNotes =
                    lazy (
                        tryFindReleaseNotes false workdir
                        |> Option.bind (tryParseReleaseNotes false)
                    )

                let runGitCommandAndFail dir cmd =
                    if dryRun then
                        Log.line $"{dir}: git {cmd}"
                    else
                        Git.CommandHelper.directRunGitCommandAndFail workdir cmd

                if createTag && not createRelease then
                    Log.start "Git:Tag"

                    runGitCommandAndFail workdir "config --local user.name \"aardvark-platform\""
                    runGitCommandAndFail workdir "config --local user.email \"admin@aardvarkians.com\""

                    let createAndPushTag (tag: string) =
                        Log.line "creating tag '%s'" tag

                        try
                            try
                                runGitCommandAndFail workdir $"tag -m {tag} {tag}"
                            with e ->
                                Log.warn "cannot create tag: %s" e.Message

                            runGitCommandAndFail workdir $"push origin {tag}"
                        with e ->
                            Log.warn "cannot push tag: %s" e.Message

                    if perProject then
                        for target in targets do
                            if target.HasVersion then
                                match target.ProjectName with
                                | Some name ->
                                    createAndPushTag $"{name.ToLowerInvariant()}/{target.Version}"
                                | _ ->
                                    Log.warn "cannot tag for '%s', no project file" (Path.Relative(target.Path, workdir))
                            else
                                Log.warn "cannot tag for '%s', no version" (Path.Relative(target.Path, workdir))
                    else
                        match globalReleaseNotes.Value with
                        | Some notes -> createAndPushTag notes.NugetVersion
                        | _ -> Log.warn "cannot tag, no version"

                    Log.stop()


                if createRelease then
                    Log.start "Github:Release"

                    let hash =
                        match Git.CommandHelper.runGitCommand workdir "rev-parse HEAD" with
                        | (true, [hash], _) -> Some hash
                        | _ -> None

                    match githubInfo with
                    | Some (user, repo) ->
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
                                let client =
                                    if dryRun then None
                                    else Some <| GitHub.createClientWithToken token

                                let createAndUploadRelease (releaseNotes: string list) (prerelease: bool) (tag: string) (name: string) (packages: string[]) =
                                    Log.line "creating release '%s' with tag '%s'" name tag

                                    Log.start "%d files" packages.Length
                                    for file in packages do
                                        Log.line "%s" (Path.Relative(file, workdir))
                                    Log.stop()

                                    let oldRelease =
                                        try
                                            client
                                            |> Option.map (fun client ->
                                                GitHub.getReleaseByTag user repo tag client
                                                |> Async.RunSynchronously
                                            )
                                        with _ ->
                                            None

                                    match client, oldRelease with
                                    | Some client, None ->
                                        client
                                        |> GitHub.createRelease user repo tag (fun p ->
                                            {
                                                Name = name
                                                TargetCommitish = match hash with | Some h -> h | None -> p.TargetCommitish
                                                Draft = true
                                                Prerelease = prerelease
                                                Body = String.concat "\r\n" releaseNotes
                                            }
                                        )
                                        |> GitHub.uploadFiles packages
                                        |> GitHub.publishDraft
                                        |> Async.RunSynchronously

                                    | _, Some _rel ->
                                        Log.warn "release with tag '%s' already exists" tag
                                    | _ ->
                                        ()

                                if perProject then
                                    for target in targets do
                                        if target.HasVersion then
                                            match target.ProjectName with
                                            | Some name ->
                                                let tag = $"{name.ToLowerInvariant()}/{target.Version}"
                                                let relname = $"{name} - {target.Version}"
                                                let packages = Directory.GetFiles(target.GetOutputPath(outputPath), "*.nupkg")
                                                createAndUploadRelease target.ReleaseNotes target.Prerelease tag relname packages
                                            | _ ->
                                                Log.warn "cannot create release for '%s', no project file" (Path.Relative(target.Path, workdir))
                                        else
                                            Log.warn "cannot create release for '%s', no version" (Path.Relative(target.Path, workdir))
                                else
                                    let packages =
                                        if doBuild then Directory.GetFiles(outputPath, "*.nupkg")
                                        else files |> List.toArray

                                    match globalReleaseNotes.Value with
                                    | Some n -> packages |> createAndUploadRelease n.Notes n.SemVer.PreRelease.IsSome n.NugetVersion n.NugetVersion
                                    | _ -> Log.warn "cannot create release, no version"
                            finally
                                Console.SetOut oo
                                Console.SetError oe
                        with e ->
                            Log.error "failed: %A" e
                            exit -1
                    | _ ->
                        ()

                    Log.stop()

            elif createTag || createRelease then
                Log.warn "not publishing, no github token"

            Log.line "finished in %A" sw.Elapsed
            0

        with e ->
            Log.error "unknown error: %A" e
            -1
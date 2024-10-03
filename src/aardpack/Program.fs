namespace aardpack

open Fake.Core
open Fake.DotNet
open System.IO
open System
open System.Text.RegularExpressions
open System.Collections.Generic
open Fake.Tools
open Fake.Api

type TargetPath =
    | File of path: string
    | Directory of path: string

    member x.Path =
        match x with
        | File f -> f
        | Directory d -> d

    member x.GetDirectoryName() =
        match x with
        | File f -> Path.GetDirectoryName f
        | Directory d -> d

type PackTarget =
    { Path         : string
      Version      : string
      Prerelease   : bool
      ReleaseNotes : string list
      TemplateFile : string option
      ProjectId    : string option
      Dependencies : Paket.Dependencies }

    member x.GetOutputPath(outputPath: string) =
        Path.Combine(outputPath, x.ProjectId |> Option.defaultValue "")

[<AutoOpen>]
module Utilities =

    // Tries to find a file in the given directory, recursively going up a level if not found.
    let tryFindFile (kind: string) (tryGetFileInDirectory: string -> string option) (parse: string -> 'T) =
        let cache = Dictionary<string, 'T>()

        let rec find (current: string) =
            match tryGetFileInDirectory current with
            | Some p -> Some <| FileInfo p
            | _ ->
                let parent = Directory.GetParent current
                if isNull parent then
                    None
                else
                    find parent.FullName

        fun (quiet: bool) (directory: string) ->
            match find directory with
            | Some fi ->
                match cache.TryGetValue fi.FullName with
                | (true, deps) -> Some deps
                | _ ->
                    try
                        let deps = parse fi.FullName
                        cache.[fi.FullName] <- deps
                        Some deps

                    with e ->
                        if not quiet then
                            Log.error "could not parse '%s': %s" fi.FullName e.Message
                        None
            | _ ->
                if not quiet then
                    Log.warn "could not find %s for '%s'" kind directory
                None

    let tryGetTemplateId (template: Paket.TemplateFile) =
        match template.Contents with
        | Paket.CompleteInfo (i, _) -> Some i.Id
        | Paket.ProjectInfo (i, _) -> i.Id

    let tryFindDependencies : string -> Paket.Dependencies option =
        let check (current: string) =
            let file = Path.Combine(current, "paket.dependencies")
            if File.Exists file then Some file else None

        tryFindFile "dependencies file" check Paket.Dependencies false

    let tryFindReleaseNotes (releaseNotesFile: string option) : bool -> string -> ReleaseNotes.ReleaseNotes option =
        let check (current: string) =
            if releaseNotesFile.IsSome then releaseNotesFile
            else
                Directory.GetFiles(current, "*")
                |> Array.tryFind (fun p -> Path.GetFileNameWithoutExtension(p).ToLower().Trim().Replace("_", "") = "releasenotes")

        tryFindFile "release notes" check (fun file ->
            let data =
                // Skip all invalid lines for preliminary release notes
                // https://github.com/fsprojects/FAKE/blob/04c2b476becaea55b2caa54420c2bbf64c901460/src/app/Fake.Core.ReleaseNotes/ReleaseNotes.fs#L232
                File.ReadAllLines(file)
                |> Array.skipWhile (fun line ->
                    let line = line.Trim('-', ' ')
                    if line.Length > 0 then
                        line.[0] <> '*' && line.[0] <> '#'
                    else
                        true
                    )

            ReleaseNotes.parse data
        )

module Program =

    let sshRx = Regex @"([a-zA-Z0-9_]+)@github.com:([^/]+)/([^/]+)\.git$"
    let httpsRx = Regex @"http(s)?://github.com/([^/]+)/([^/]+)$"

    [<EntryPoint>]
    let main argv =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let argv = argv |> Array.toList |> List.filter (String.isNullOrWhiteSpace >> not)

        do
            let ctx = Context.FakeExecutionContext.Create false "build.fsx" argv
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

        let workdir = Environment.CurrentDirectory

        let knownArgs = Set.ofList [ "--configuration"; "--release-notes"; "--output" ]
        let mutable args = Map.empty
        let removeArgs = HashSet<int>()

        for i = 0 to argv.Length - 1 do
            if knownArgs |> Set.contains argv.[i] then
                removeArgs.Add(i) |> ignore

                if i + 1 < argv.Length && not <| argv.[i + 1].StartsWith('-') then
                    args <- args |> Map.add argv[i] argv[i + 1]
                    removeArgs.Add(i + 1) |> ignore

        let tryGetArg (name: string) =
            args |> Map.tryFind $"--{name}"

        let argv =
            argv |> List.mapi (fun i x ->
                if removeArgs.Contains i then None
                else Some x
            )
            |> List.choose id

        let files =
            argv |> List.filter (fun p -> not (p.StartsWith "-") && File.Exists p)

        let files =
            match files with
            | [] -> [workdir]
            | f -> f

        let hasFlag (name: string) =
            argv |> List.exists ((=) $"--{name}")

        // If --no-build is specified, build & pack steps will be skipped and the files in arguments will be added to the release directly.
        let doBuild =
            not <| (hasFlag "nobuild" || hasFlag "no-build")

        // If true, skips the build step but packages normally in contrast to --no-build
        let skipBuild =
            hasFlag "skip-build" || hasFlag "skipbuild"

        // Note: Github release will create a tag regardless.
        let createTag =
            not <| (hasFlag "notag" || hasFlag "no-tag")

        // Do not actually create tags and push packages
        let dryRun =
            hasFlag "dry-run" || hasFlag "dryrun"

        // Create a tag and release for each project file specified as argument
        // E.g. Aardvark.Base.csproj results in a tag aardvark.base/1.2.3.4 and release titled Aardvark.Base - 1.2.3.4
        let perProject =
            doBuild && (hasFlag "per-project" || hasFlag "perproject")

        let createRelease =
            not <| (hasFlag "norelease" || hasFlag "no-release")

        let parseOnly =
            hasFlag "parseonly" || hasFlag "parse-only"

        let showVersion =
            hasFlag "version"

        let config =
            match tryGetArg "configuration" with
            | Some cfg ->
                match cfg.ToLowerInvariant() with
                | "debug" -> DotNet.BuildConfiguration.Debug
                | "release" -> DotNet.BuildConfiguration.Release
                | _ -> DotNet.BuildConfiguration.Custom cfg
            | _ ->
                DotNet.BuildConfiguration.Release

        let releaseNotesFile =
            match tryGetArg "release-notes" with
            | Some f -> Some f
            | _ -> None

        let outputPath =
            match tryGetArg "output" with
            | Some dir when not <| File.Exists dir ->
                Path.GetFullPath dir
            | _ ->
                Path.Combine(workdir, "bin", "pack")

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
            let version =
                match tryFindReleaseNotes releaseNotesFile true workdir with
                | Some notes -> notes.NugetVersion
                | _ -> "0.0.0.0"

            printfn "%s" version
            0

        else
            try
                if doBuild && not skipBuild then
                    Log.start "DotNet:Build"
                    for f in files do
                        Log.start "%s" (Path.Relative(f, workdir))
                        f |> DotNet.build (fun o ->
                            { o with
                                NoLogo = true
                                MSBuildParams = { o.MSBuildParams with DisableInternalBinLog = true } // https://github.com/fsprojects/FAKE/issues/2595
                                Configuration = config
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

                if doBuild then
                    Log.start "Paket:Pack"

                let tryGetPackTarget (dependencies: Paket.Dependencies) (template: Paket.TemplateFile option) (path: TargetPath) =
                    let dir = path.GetDirectoryName()

                    match tryFindReleaseNotes releaseNotesFile false dir with
                    | Some notes ->
                        let templateFile =
                            template |> Option.map (fun t -> t.FileName)

                        let projectId =
                            template |> Option.map (fun t ->
                                let fileName = Path.GetFileNameWithoutExtension t.FileName
                                tryGetTemplateId t |> Option.defaultValue fileName
                            )

                        Some {
                            Path         = path.Path
                            Version      = notes.NugetVersion
                            Prerelease   = notes.SemVer.PreRelease.IsSome
                            ReleaseNotes = notes.Notes
                            TemplateFile = templateFile
                            ProjectId    = projectId
                            Dependencies = dependencies
                        }

                    | _ -> None

                let rec tryGetPackTargets (fileOrDirectory: string) =
                    let path =
                        let full = Path.GetFullPath fileOrDirectory
                        if File.Exists full then File full
                        else Directory full

                    let dir = path.GetDirectoryName()

                    match tryFindDependencies dir with
                    | Some deps ->
                        let templateFiles = deps.ListTemplateFiles()

                        let template =
                            templateFiles |> List.tryFind (fun t ->
                                let templatePath = Path.GetFullPath t.FileName
                                Path.GetDirectoryName templatePath = dir
                            )

                        if perProject && template.IsNone then
                            templateFiles |> List.choose (fun t -> t.FileName |> Path.GetFullPath |> File |> tryGetPackTarget deps (Some t))
                        else
                            tryGetPackTarget deps template path |> Option.toList

                    | _ -> []

                let targets =
                    if doBuild then
                        let result =
                            files
                            |> List.collect tryGetPackTargets
                            |> List.distinctBy (fun target ->
                                target.Dependencies.RootPath, target.TemplateFile
                            )

                        Log.start "found %d pack targets" result.Length
                        for t in result do Log.line "%s" t.Path
                        Log.stop()

                        result
                    else
                        []

                if doBuild then
                    let projectUrl =
                        githubInfo |> Option.map (fun (user, repo) ->
                            sprintf "https://github.com/%s/%s/" user repo
                        )

                    for target in targets do
                        let outputPath =
                            if perProject then target.GetOutputPath outputPath
                            else outputPath

                        target.Dependencies.Pack(
                            outputPath,
                            version = target.Version,
                            releaseNotes = String.concat "\r\n" target.ReleaseNotes,
                            buildConfig = string config,
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

                        if doBuild then
                            for target in targets do
                                if perProject then
                                    match target.ProjectId with
                                    | Some name ->
                                        createAndPushTag $"{name.ToLowerInvariant()}/{target.Version}"
                                    | _ ->
                                        Log.error "cannot tag for '%s', project id not found but --per-project was specified" (Path.Relative(target.Path, workdir))
                                else
                                    createAndPushTag target.Version
                        else
                            match tryFindReleaseNotes releaseNotesFile false workdir with
                            | Some n ->
                                createAndPushTag n.NugetVersion
                            | _ ->
                                Log.warn "cannot create tag, no version"

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
                                        Log.start "creating release '%s' with tag '%s' (%d files)" name tag packages.Length
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

                                    if doBuild then
                                        for target in targets do
                                            let outputPath =
                                                if perProject then target.GetOutputPath outputPath
                                                else outputPath

                                            let packages = Directory.GetFiles(outputPath, "*.nupkg")

                                            if perProject then
                                                match target.ProjectId with
                                                | Some id ->
                                                    let tag = $"{id.ToLowerInvariant()}/{target.Version}"
                                                    let rel = $"{id} - {target.Version}"
                                                    createAndUploadRelease target.ReleaseNotes target.Prerelease tag rel packages
                                                | _ ->
                                                    let path = Path.Relative(target.Path, workdir)
                                                    Log.error "cannot create release for '%s', project id not found but --per-project was specified" path
                                            else
                                                createAndUploadRelease target.ReleaseNotes target.Prerelease target.Version target.Version packages
                                    else
                                        match tryFindReleaseNotes releaseNotesFile false workdir with
                                        | Some n ->
                                            let packages = files |> List.filter File.Exists |> List.toArray
                                            createAndUploadRelease n.Notes n.SemVer.PreRelease.IsSome n.NugetVersion n.NugetVersion packages
                                        | _ ->
                                            Log.warn "cannot create release, no version"
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
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

    let tryFindReleaseNotes : bool -> string -> ReleaseNotes.ReleaseNotes option =
        let check (current: string) =
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

        // If true, skips the build step but packages normally in contrast to --no-build
        let skipBuild =
            hasArgument "skip-build" || hasArgument "skipbuild"

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

        let config =
            if hasArgument "debug" then DotNet.BuildConfiguration.Debug
            else DotNet.BuildConfiguration.Release

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
                match tryFindReleaseNotes true workdir with
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

                Log.start "Paket:Pack"

                let tryGetPackTarget (dependencies: Paket.Dependencies) (template: Paket.TemplateFile option) (path: TargetPath) =
                    let dir = path.GetDirectoryName()

                    match tryFindReleaseNotes false dir with
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

                let outputPath = Path.Combine(workdir, "bin", "pack")

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

                        for target in targets do
                            if perProject then
                                match target.ProjectId with
                                | Some name ->
                                    createAndPushTag $"{name.ToLowerInvariant()}/{target.Version}"
                                | _ ->
                                    Log.error "cannot tag for '%s', project id not found but --per-project was specified" (Path.Relative(target.Path, workdir))
                            else
                                createAndPushTag target.Version

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
                                            let packages = Directory.GetFiles(target.GetOutputPath(outputPath), "*.nupkg")

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
                                        match tryFindReleaseNotes false workdir with
                                        | Some n ->
                                            let packages = List.toArray files
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
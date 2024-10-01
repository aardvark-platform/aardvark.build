namespace Aardvark.Build.Tool

open System
open System.IO
open ICSharpCode.SharpZipLib.Zip

module NativeDependenciesCommand =

    let private tryFindLibs (assemblyName: string) (path: string) =
        try
            Directory.GetDirectories(path, "*")
            |> Array.tryPick (fun path ->
                let name = Path.GetFileName(path).ToLower()

                if name = "lib" || name = "libs" then
                    let path = Path.Combine(path, "Native", assemblyName)
                    if Directory.Exists path then Some path
                    else None
                else
                    None
            )

        with e ->
            Log.warn $"Error while locating libs folder: {e.Message}"
            None

    let private zip (level: int) (sourcePath: string) (outputPath: string) =
        try
            let info = FileInfo(outputPath)
            if not info.Directory.Exists then
                info.Directory.Create()
            if info.Exists then
                info.Delete()

            use fs = File.OpenWrite outputPath
            use zip = new ZipOutputStream(fs)
            zip.SetLevel level

            let sourcePath = Path.withTrailingSlash sourcePath
            let files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)

            for file in files do
                let name = Path.toZipEntryName sourcePath file
                if String.IsNullOrEmpty name then
                    Log.warn $"Failed to determine entry name for: {file}"
                else
                    let entry = new ZipEntry(name)
                    zip.PutNextEntry entry

                    Log.debug $"{name}"

                    use s = File.OpenRead file
                    s.CopyTo(zip)

            zip.Finish()
            zip.Close()

        with e ->
            Log.warn $"Failed to pack native dependencies: {e.Message}"

    let run (args: Args) =
        let path = Path.GetDirectoryName args.["project-path"]
        let zipPath = args.["zip-path"]
        let assemblyName = args.["assembly-name"]

        let force =
            match args |> Args.tryGet "force" with
            | Some f -> f.ToLowerInvariant() = "true"
            | _ -> false

        let root =
            match args |> Args.tryGet "repository-root" with
            | Some r -> Some r
            | _ ->
                Log.debug "Locating repository root for path: %s" path
                Utilities.locateRepositoryRoot path

        match root with
        | Some root ->
            Log.debug "Locating libs folder for path: %s" root

            match tryFindLibs assemblyName root with
            | Some libs ->
                let requirePack =
                    if File.Exists zipPath then
                        if force then
                            Log.info $"Forcing repack of native dependencies: {libs} -> {zipPath}"
                            true

                        elif File.getLastWriteTimeUtcSafe zipPath > Directory.getLastWriteTimeUtcSafe libs then
                            Log.info $"Native dependencies are up-to-date: {libs} -> {zipPath}"
                            false

                        else
                            Log.info $"Native dependencies are out-of-date: {libs} -> {zipPath}"
                            true

                    else
                        Log.info $"Packing native dependencies: {libs} -> {zipPath}"
                        true

                if requirePack then
                    zip 9 libs zipPath

            | _ ->
                Log.debug $"Did not find a libs folder"

        | None ->
            Log.warn "Could not find repository root (please specify AardvarkBuildRepositoryRoot Property)"

        0

namespace Aardvark.Build.Tool

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions

module internal Process =

    let run (writeOutputToConsole: bool) (directory: string option) (cmd: string) (args: string) =
        use p = new Process()
        p.StartInfo.FileName <- cmd
        p.StartInfo.Arguments <- args
        p.StartInfo.RedirectStandardOutput <- true
        p.StartInfo.RedirectStandardError <- true
        p.StartInfo.UseShellExecute <- false
        p.StartInfo.CreateNoWindow <- true
        p.StartInfo.WorkingDirectory <- directory |> Option.defaultValue null

        let output = ResizeArray<string>()
        p.OutputDataReceived.Add (fun args ->
            if writeOutputToConsole then
                Console.WriteLine args.Data

            if not <| String.IsNullOrWhiteSpace args.Data then
                lock output (fun _ -> output.Add args.Data)
        )

        let errors = ResizeArray<string>()
        p.ErrorDataReceived.Add (fun args ->
            if not <| String.IsNullOrWhiteSpace args.Data then
                lock errors (fun _ -> errors.Add args.Data)
        )

        p.Start() |> ignore
        p.BeginOutputReadLine()
        p.BeginErrorReadLine()
        p.WaitForExit()

        if p.ExitCode <> 0 || errors.Count > 0 then
            let sb = StringBuilder()
            sb.Append $"Command '{cmd} {args}' failed (status code = {p.ExitCode})" |> ignore
            if errors.Count > 0 then
                sb.Append $":" |> ignore

                for e in errors do
                    sb.Append $"{Environment.NewLine}    {e}" |> ignore
            else
                sb.Append "." |> ignore

            failwith <| sb.ToString()

        output

module internal Git =

    let run (repoPath: string) (args: string) =
        Process.run false (Some repoPath) "git" args

module internal File =

    let getLastWriteTimeUtcSafe (path: string) =
        try File.GetLastWriteTimeUtc(path)
        with _ -> DateTime.MaxValue

module internal Stream =

    let readAllLines (stream: Stream) =
        use r = new StreamReader(stream, leaveOpen = true)
        let result = ResizeArray<string>()

        let mutable line = r.ReadLine()
        while line <> null do
            result.Add line
            line <- r.ReadLine()

        result

module internal Directory =

    let getLastWriteTimeUtcSafe (path: string) =
        try
            let dir = Directory.GetLastWriteTimeUtc(path)

            // For some reason the call above does not detect changes to the content of the directory...
            let files =
                Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                |> Array.map File.GetLastWriteTimeUtc

            max dir (Array.max files)
        with _ ->
            DateTime.MaxValue

    let rec computeHash (path: string) =
        let sb = StringBuilder()

        if Directory.Exists (Path.Combine(path, ".git")) then
            for hash in Git.run path "log -n 1 --pretty=format:\"%%H\"" do
                sb.AppendLine hash |> ignore

            for s in Git.run path "status --porcelain" do
                sb.AppendLine s |> ignore
                let file = Path.Combine(path, s.Substring(3).Replace('/', Path.DirectorySeparatorChar))

                if File.Exists file then
                    for line in Git.run path $"hash-object \"{file}\"" do
                        sb.AppendLine line |> ignore

                elif Directory.Exists file then
                    let files = Directory.GetFiles(file, "*", SearchOption.AllDirectories)
                    for file in files do
                        sb.AppendLine (sprintf "?? %s" file) |> ignore
                        for line in Git.run path $"hash-object \"{file}\"" do
                            sb.AppendLine line |> ignore

            let bytes = Encoding.UTF8.GetBytes(sb.ToString())
            SHA1.HashData(bytes) |> Convert.ToBase64String

        else
            let date = getLastWriteTimeUtcSafe path
            date.ToFileTimeUtc() |> BitConverter.GetBytes |> Convert.ToBase64String

module internal Path =

    let withTrailingSlash (path: string) =
        if path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar) then path
        else path + string Path.DirectorySeparatorChar

    let toZipEntryName (root: string) (path: string) =
        if path.StartsWith root then
            path.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/')
        else
            null

    let getHashName (input: string) =
        Convert.ToBase64String(SHA1.HashData (Encoding.UTF8.GetBytes input))
            .Replace("/", "_")
            .Replace("=", "-")

module Map =
    let union (l : Map<'k, 'v>) (r : Map<'k, 'v>) =
        let mutable result = l
        for KeyValue(k,v) in r do
            result <- Map.add k v result
        result

    let unionMany (input : Map<'k, 'v> seq) =
        (Map.empty, input) ||> Seq.fold union

module internal Utilities =

    let private repositoryRootIndicators =
        let inline regex pat = Regex($"^{pat}$", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
        List.map regex [
            //"license(\.md)?"
            //"readme(\.md)?"
            @"paket\.dependencies"
            //"build\.(cmd|bat|ps1|sh)"
            //@"release(_|-)?notes(\.md)?"
        ]

    let isRepositoryRoot (directory: string) =
        if Directory.Exists (Path.Combine(directory, ".git")) then true
        else
            let files = Directory.GetFiles(directory, "*")
            files |> Array.exists (fun file ->
                let name = Path.GetFileName file
                repositoryRootIndicators |> List.exists (fun r -> r.IsMatch name)
            )

    let rec locateRepositoryRoot (directory: string) =
        try
            Log.debug "Locating: %s" directory

            if Directory.Exists directory then
                if isRepositoryRoot directory then Some directory
                else
                    let parent = Directory.GetParent directory
                    if isNull parent then None
                    else locateRepositoryRoot parent.FullName
            else
                None
        with e ->
            Log.warn $"Error while locating repository root: {e}"
            None

    let rec locateFile (predicate: string -> bool) (directory: string) =
        try
            Log.debug "Locating: %s" directory

            if Directory.Exists directory then
                let files = Directory.GetFiles directory
                let found = files |> Array.tryFind predicate

                if found.IsSome then found
                elif isRepositoryRoot directory then None
                else
                    let parent = Directory.GetParent directory
                    if isNull parent then None
                    else locateFile predicate parent.FullName
            else
                None
        with e ->
            Log.warn $"Error while locating file: {e}"
            None

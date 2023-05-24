namespace Aardvark.Build

open System
open System.IO
open Microsoft.Build.Utilities

[<AutoOpen>]
module PathExtensions =
    type Path with
        static member TryGetRelativePath(path : string, ?basePath : string) =
            let basePath = defaultArg basePath Environment.CurrentDirectory |> Path.GetFullPath
            let path = Path.GetFullPath path

            if path.StartsWith basePath then
                let rel =
                    let r = path.Substring(basePath.Length)
                    if r.Length > 0 && (r.[0] = Path.DirectorySeparatorChar || r.[0] = Path.AltDirectorySeparatorChar) then
                        r.Substring(1)
                    else
                        r
                Some rel
            else
                None

        static member Unix(path : string) =
            path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/')

module Zip =
    open ICSharpCode.SharpZipLib.Zip
    open System.Threading

    let zip (ct : CancellationToken) (level : int) (srcPath : string) =
        use ms = new MemoryStream()
        use zip = new ZipOutputStream(ms)
        zip.SetLevel level
        
        let files = Directory.GetFiles(srcPath, "*", SearchOption.AllDirectories)
        ct.ThrowIfCancellationRequested()

        for file in files do
            match Path.TryGetRelativePath(file, srcPath) with
            | Some rel ->   
                let name = Path.Unix rel
                let e = new ZipEntry(name)
                zip.PutNextEntry e
                
                use s = File.OpenRead file
                s.CopyTo(zip)
                ct.ThrowIfCancellationRequested()

                ()
            | None ->
                ()

        zip.Finish()
        zip.Close()
        ms.ToArray()

module Assembly =
    open Mono.Cecil
    open System.Threading

    let addNativeZip (ct : CancellationToken) (log : TaskLoggingHelper) (srcDirectory : string) (referencePaths : string[]) (dll : string) =
        let name = "native.zip"
        let disposables = System.Collections.Generic.List<IDisposable>()
        try
            let res = new DefaultAssemblyResolver()

            let opt = 
                ReaderParameters(
                    ReadWrite = true,
                    AssemblyResolver = res
                )

            res.add_ResolveFailure(
                AssemblyResolveEventHandler(fun _ name -> 
                    let path = 
                        referencePaths |> Array.tryFind (fun path ->
                            Path.GetFileNameWithoutExtension(path).ToLower().Trim() = name.Name.ToLower().Trim()
                        )
                    match path with
                    | Some path ->
                        try AssemblyDefinition.ReadAssembly(path, ReaderParameters(AssemblyResolver = res))
                        with _ -> null
                    | None ->
                        null
                )
            )
            let pdb = Path.ChangeExtension(dll, ".pdb")
            if File.Exists pdb then
                let data = File.ReadAllBytes pdb
                opt.SymbolStream <- new MemoryStream(data)
                opt.ReadSymbols <- true

            ct.ThrowIfCancellationRequested()
            let ass = AssemblyDefinition.ReadAssembly(dll, opt)
            ct.ThrowIfCancellationRequested()
            let old = ass.MainModule.Resources |> Seq.filter (fun r -> r.Name.EndsWith "native.zip") |> Seq.toArray
            for o in old do 
                ass.MainModule.Resources.Remove o |> ignore
            ct.ThrowIfCancellationRequested()

            let data = Zip.zip ct 9 srcDirectory
            ct.ThrowIfCancellationRequested()
            ass.MainModule.Resources.Add(new EmbeddedResource(name, ManifestResourceAttributes.Public, data))
            ct.ThrowIfCancellationRequested()

            let opt = WriterParameters()
            if File.Exists pdb then
                let s = File.Open(pdb, FileMode.Open)
                opt.WriteSymbols <- true
                opt.SymbolStream <- s
                opt.SymbolWriterProvider <- Mono.Cecil.Pdb.PdbWriterProvider()
                disposables.Add s

            ass.Write(opt)
            ct.ThrowIfCancellationRequested()
        finally
            for d in disposables do d.Dispose()


module Log =


    let private coloredConsole =
        try 
            let o = Console.ForegroundColor
            Console.ForegroundColor <- ConsoleColor.Yellow
            Console.ForegroundColor <- o
            true
        with _ -> 
            false

    let private withColor (color : ConsoleColor) (action : unit -> 'a) =
        if coloredConsole then
            let o = Console.ForegroundColor
            try 
                Console.ForegroundColor <- color
                action()
            finally
                Console.ForegroundColor <- o
        else
            action()

    let mutable private indent = ""

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
            withColor ConsoleColor.DarkYellow (fun () ->
                Console.WriteLine("{0}{1}", indent, str)
            )
        )

    let error fmt =
        fmt |> Printf.kprintf (fun str ->
            withColor ConsoleColor.Red (fun () ->
                Console.WriteLine("{0}{1}", indent, str)
            )
        )

        
module Native =
    open System.Runtime.InteropServices

    let arch =
        match RuntimeInformation.OSArchitecture with
        | Architecture.X64 -> "AMD64"
        | Architecture.X86 -> "x86"
        | Architecture.Arm -> "arm"
        | Architecture.Arm64 -> "arm64"
        | _ -> "unknown"

    let platform =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            "windows"
        else if RuntimeInformation.IsOSPlatform OSPlatform.Linux then
            "linux"
        else if RuntimeInformation.IsOSPlatform OSPlatform.OSX then
            "mac"
        else
            "unknown"




module Tools =
    let private libDirNames = ["lib"; "libs"]

    let private projectRootIndicators =
        let inline regex pat =
            System.Text.RegularExpressions.Regex("^" + pat + "$", Text.RegularExpressions.RegexOptions.IgnoreCase)
        List.map regex [
            //"license(\.md)?"
            //"readme(\.md)?"
            "paket\.dependencies"
            //"build\.(cmd|bat|ps1|sh)"
            @"release(_|-)?notes(\.md)?"
        ]

    let private releaseNotesRx =
        System.Text.RegularExpressions.Regex(@"^release(_|-)?notes(\.md)?$", Text.RegularExpressions.RegexOptions.IgnoreCase)

    type private Marker = class end
    let mutable private installed = 0
    let mutable private logger : TaskLoggingHelper = null

    let boot (log : TaskLoggingHelper) =
        if System.Threading.Interlocked.Exchange(&installed, 1) = 0 then
            logger <- log
            let root = Path.GetDirectoryName typeof<Marker>.Assembly.Location
            let inResolve = new System.Threading.ThreadLocal<bool>(fun _ -> false)
            System.AppDomain.CurrentDomain.add_AssemblyResolve (ResolveEventHandler(fun _ e ->
                if inResolve.Value then
                    null
                else
                    inResolve.Value <- true
                    let n = System.Reflection.AssemblyName e.Name
                    try
                        let loaded = 
                            AppDomain.CurrentDomain.GetAssemblies() 
                            |> Array.tryFind (fun a -> a.GetName().Name = n.Name)
                        match loaded with
                        | Some l -> l
                        | None ->
                            try
                                System.Reflection.Assembly.Load e.Name
                            with _ ->
                                let n = System.Reflection.AssemblyName e.Name
                                let dll = Path.Combine(root, n.Name + ".dll")
                                if File.Exists dll then 
                                    log.LogMessage $"loading {dll} (possible version mismatch)"
                                    try System.Reflection.Assembly.LoadFile dll
                                    with _ -> null
                                else
                                    null
                    finally
                        inResolve.Value <- false
            ))
            
    let rec findProjectRoot (path : string) =
        try
            if Directory.Exists path then 
                if Directory.Exists (Path.Combine(path, ".git")) then
                    Some path
                else
                    let files = Directory.GetFiles(path, "*")
                    let isRoot =
                        files |> Array.exists (fun file ->
                            let name = Path.GetFileName file
                            projectRootIndicators |> List.exists (fun r -> r.IsMatch name)
                        )

                    if isRoot then
                        Some path
                    else
                        let parent = Directory.GetParent path
                        if isNull parent then None
                        else findProjectRoot parent.FullName
            else
                None
        with _ ->   
            None

    let isReleaseNotesFile (path : string) =
        let name = Path.GetFileNameWithoutExtension(path).ToLower()
        releaseNotesRx.IsMatch name

    let rec findLibs (path : string) =
        let found =
            libDirNames |> List.tryPick (fun l ->   
                let p = Path.Combine(path, l, "Native")
                try if Directory.Exists p then Some p else None
                with _ -> None
            )
        match found with
        | Some path -> Some path
        | None ->  
            try
                let parent = Path.GetDirectoryName path
                if isNull parent then None
                else findLibs parent
            with _ ->   
                None

    open System.IO
    open System.Diagnostics

    let git dir fmt =
        fmt |> Printf.kprintf (fun cmd ->
            let info = ProcessStartInfo("git")
            info.Arguments <- cmd
            info.UseShellExecute <- false
            info.RedirectStandardOutput <- true
            info.RedirectStandardError <- true
            info.CreateNoWindow <- true
            info.WorkingDirectory <- dir
            let p = Process.Start(info)

            let output = System.Collections.Generic.List<string>()
            p.OutputDataReceived.Add (fun e ->
                if not (isNull e.Data) then output.Add e.Data
            )
            
            if not (isNull logger) then
                logger.LogMessage(sprintf "running git %s" cmd)
                p.OutputDataReceived.Add (fun e ->
                    if not (isNull e.Data) then logger.LogMessage(sprintf "    %s" e.Data)
                )
                p.ErrorDataReceived.Add (fun e ->
                    if not (isNull e.Data) then logger.LogMessage(sprintf "    %s" e.Data)
                )
            p.BeginOutputReadLine()
            p.BeginErrorReadLine()

            p.WaitForExit()
            if p.ExitCode <> 0 then 
                if not (isNull logger) then
                    logger.LogError(sprintf "git %s exited with code %d" cmd p.ExitCode)
                failwithf "git %s exited with code %d" cmd p.ExitCode
            
            Seq.toList output
        )


    let rec computeDirectoryHash (repoPath : string) =
        let sb = System.Text.StringBuilder()
        // use ms = new MemoryStream()
        // use w = new System.Security.Cryptography.CryptoStream(ms, System.Security.Cryptography.SHA1.Create(), System.Security.Cryptography.CryptoStreamMode.Write)
        // use sb = new StreamWriter(w)

        if Directory.Exists (Path.Combine(repoPath, ".git")) then
            for hash in git repoPath "log -n 1 --pretty=format:\"%%H\"" do
                sb.AppendLine hash |> ignore

            git repoPath "status --porcelain" |> List.iter (fun s ->
                sb.AppendLine s |> ignore
                let file = Path.Combine(repoPath, s.Substring(3).Replace('/', Path.DirectorySeparatorChar))

                if File.Exists file then
                    for line in git repoPath "hash-object \"%s\"" file do
                        sb.AppendLine line |> ignore
                elif Directory.Exists file then
                    let files = Directory.GetFiles(file, "*", SearchOption.AllDirectories)
                    for file in files do
                        sb.AppendLine (sprintf "?? %s" file) |> ignore
                        for line in git repoPath "hash-object \"%s\"" file do
                            sb.AppendLine line |> ignore
                else
                    failwithf "git status reported unknown file %s" file
            )
            let bytes = System.Text.Encoding.UTF8.GetBytes (sb.ToString())
            use sha = System.Security.Cryptography.SHA1.Create()
            sha.ComputeHash(bytes) |> System.Convert.ToBase64String

        else
            let dates = 
                Directory.GetFiles(repoPath, "*", SearchOption.AllDirectories)
                |> Array.map (fun f -> File.GetLastWriteTime f)
            if dates.Length > 0 then 
                let date = Array.max dates
                date.ToFileTimeUtc() |> System.BitConverter.GetBytes |> System.Convert.ToBase64String
            else
                "empty"

    // computeDirectoryHash "/Users/schorsch/Development/FSys";;

module Symlink =
    open System.Runtime.InteropServices

    module Mac =
        [<DllImport("libc")>]
        extern int symlink(string src, string linkName);

    module Linux =
        [<DllImport("libc")>]
        extern int symlink(string src, string linkName);

    let symlink (src : string) (name : string) =
        if File.Exists name then File.Delete name
        if RuntimeInformation.IsOSPlatform OSPlatform.Linux then 
            let ret = Linux.symlink(src, name)
            if ret <> 0 then Result.Error $"could not create symlink {name} {ret}"
            else Ok()
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then 
            let ret = Mac.symlink(src, name)
            if ret <> 0 then Result.Error $"could not create symlink {name} {ret}"
            else Ok()
        else 
            Error "symlinks not supported on Windows"


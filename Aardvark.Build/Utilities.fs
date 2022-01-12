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

    let addNativeZip (ct : CancellationToken) (srcDirectory : string) (dll : string) =
        let name = "native.zip"
        let disposables = System.Collections.Generic.List<IDisposable>()
        try
            let opt = ReaderParameters()
            opt.ReadWrite <- true

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
            "license(\.md)?"
            "readme(\.md)?"
            "paket\.dependencies"
            "build\.(cmd|bat|ps1|sh)"
        ]

    let private releaseNotesRx =
        System.Text.RegularExpressions.Regex(@"^release(_|-)?notes(\.md)?$", Text.RegularExpressions.RegexOptions.IgnoreCase)

    type private Marker = class end
    let mutable private installed = 0
    let boot (log : TaskLoggingHelper) =
        if System.Threading.Interlocked.Exchange(&installed, 1) = 0 then
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


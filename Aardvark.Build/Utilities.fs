namespace Aardvark.Build

open System
open System.IO

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

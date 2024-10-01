namespace Aardvark.Build.Tests

open System
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open FsUnit
open NUnit.Framework
open ICSharpCode.SharpZipLib.Zip

[<TestFixture>]
module BuildTests =

    let framework =
#if NET8_0
        Framework.Net8
#else
        Framework.Net48
#endif

    let mutable private testApp = Unchecked.defaultof<Assembly>

    [<OneTimeSetUp>]
    let Build() =
        let srcRoot = Path.Combine(__SOURCE_DIRECTORY__, "..", "..")
        DotNet.build Framework.Net8 <| Path.Combine(srcRoot, "Aardvark.Build", "Aardvark.Build.fsproj")
        DotNet.build framework <| Path.Combine(srcRoot, "Tests", "TestApp", "TestApp.fsproj")

        let assemblyName =
            if framework = Framework.Net48 then "TestApp.exe"
            else "TestApp.dll"

        testApp <- Assembly.LoadFile <| Path.GetFullPath assemblyName

    [<Test>]
    let ``[Build] Version``() =
        testApp.GetName().Version |> should equal (Version(9, 9, 0, 0))

        testApp.GetCustomAttributes(true)
        |> Array.pick (function
            | :? AssemblyInformationalVersionAttribute as att -> Some att.InformationalVersion
            | _ -> None
        )
        |> should startWith "9.9.9"

    [<Test>]
    let ``[Build] Native dependencies``() =
        use s = testApp.GetManifestResourceStream("native.zip")
        use zip = new ZipFile(s)

        zip.ReadEntry("remap.xml") |> should startWith "<configuration>"
        zip.ReadEntry("mac/arm64/MyNativeDep") |> should equal "hallo"

    [<Test>]
    let ``[Build] Pack``() =
        let fsproj = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "Tests", "TestApp", "TestApp.fsproj")

        try
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                let otherFramework = if framework = Framework.Net8 then Framework.Net48 else Framework.Net8
                DotNet.build otherFramework fsproj // Setup method builds the current framework

            DotNet.pack fsproj

            use zip = new ZipFile(Path.Combine("pack", "TestApp.9.9.9.nupkg"))
            zip.GetEntry("lib/net8.0/TestApp.dll") |> should not' (equal null)

            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                zip.GetEntry("lib/net48/TestApp.exe") |> should not' (equal null)

            let nuspec = zip.ReadEntry("TestApp.nuspec")
            nuspec.Contains("<releaseNotes>- Test") |> should be True
            nuspec.Contains("- Include ; some ; semicolons") |> should be True

        finally
            if Directory.Exists "pack" then Directory.Delete("pack", true)

    [<Test>]
    let ``[Build] Local sources``() =
        let libModule = testApp.GetType("TestApp.Lib")
        let valueProperty = libModule.GetProperty("Value")

        let value = unbox<string> <| valueProperty.GetValue(null)
        value |> should equal "LOCAL_OVERRIDE;LOCAL_OVERRIDE"
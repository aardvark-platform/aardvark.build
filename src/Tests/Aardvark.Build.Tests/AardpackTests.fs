namespace Aardvark.Build.Tests

#if NET8_0
open System
open System.IO
open System.Runtime.InteropServices
open FsUnit
open NUnit.Framework
open ICSharpCode.SharpZipLib.Zip

[<TestFixture>]
module AardpackTests =

    let config =
    #if DEBUG
        "Debug"
    #else
        "Release"
    #endif

    let testLibAProj =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "TestLibA", "TestLibA.fsproj")

    let testLibBProj =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "TestLibB", "TestLibB.fsproj")

    let aardpackPath =
        let binary = if RuntimeInformation.IsOSPlatform OSPlatform.Windows then "aardpack.exe" else "aardpack"
        Path.Combine("..", "..", "aardpack", "net8.0", binary)

    let releaseNotesPath =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "TestApp", "RELEASE_NOTES.md")

    let rec getRandomName() =
        let name = Path.GetRandomFileName()
        if File.Exists name || Directory.Exists name then getRandomName()
        else name

    let rec tempDir (action: string -> 'T) =
        let name = getRandomName()
        Directory.CreateDirectory name |> ignore

        try
            action name
        finally
            if Directory.Exists name then Directory.Delete(name, true)

    module private Aardpack =

        let run (args: string) =
            Process.run true None aardpackPath $"{args} --configuration {config} --dry-run" // Don't ever forget --dry-run here
            |> String.concat Environment.NewLine

    [<SetUp>]
    let Setup() =
        Assert.That(aardpackPath, Does.Exist)
        Assert.That(releaseNotesPath, Does.Exist)

    [<Test>]
    let ``[Aardpack] Parse only``() =
        let output = Aardpack.run $"--parse-only --release-notes \"{releaseNotesPath}\""
        output |> should equal "9.9.9"

    [<Test>]
    let ``[Aardpack] Build, pack, and release``() =
        tempDir (fun outputDir ->
            let output = Aardpack.run $"--output \"{outputDir}\" \"{testLibAProj}\""
            output |> should contain "DotNet:Build"
            output |> should contain "Paket:Pack"
            output |> should contain "Github:Release"
            output |> should contain "creating release '1.2.3' with tag '1.2.3' (1 files)"

            let nupkg = Path.Combine(outputDir, "TestLibA.1.2.3.nupkg")
            Assert.That(nupkg, Does.Exist)

            let files = Directory.GetFiles(outputDir)
            Assert.That(files, Has.Length.EqualTo(1))

            use zip = new ZipFile(nupkg)
            let nuspec = zip.ReadEntry("TestLibA.nuspec")
            nuspec |> should contain "<version>1.2.3</version>"
            nuspec |> should contain "<releaseNotes>- TestLibA changes"
        )

    [<Test>]
    let ``[Aardpack] Per project``() =
        tempDir (fun outputDir ->
            let output = Aardpack.run $"--per-project --output \"{outputDir}\" \"{testLibAProj}\" \"{testLibBProj}\""
            output |> should contain "DotNet:Build"
            output |> should contain "Paket:Pack"
            output |> should contain "Github:Release"
            output |> should contain "creating release 'TestLibA - 1.2.3' with tag 'testliba/1.2.3' (1 files)"
            output |> should contain "creating release 'TestLibB - 3.2.1' with tag 'testlibb/3.2.1' (1 files)"

            let getNupkgPath (name: string) (version: string) =
                Path.Combine(outputDir, name, $"{name}.{version}.nupkg")

            let nupkgA = getNupkgPath "TestLibA" "1.2.3"
            Assert.That(nupkgA, Does.Exist)

            let filesA = Directory.GetFiles(Path.GetDirectoryName nupkgA)
            Assert.That(filesA, Has.Length.EqualTo(1))

            use zip = new ZipFile(nupkgA)
            let nuspec = zip.ReadEntry("TestLibA.nuspec")
            nuspec |> should contain "<version>1.2.3</version>"
            nuspec |> should contain "<releaseNotes>- TestLibA changes"
            nuspec |> should contain "<dependency id=\"TestLibB\" version=\"[3.2.1,3.3.0)\" />"

            let nupkgB = getNupkgPath "TestLibB" "3.2.1"
            Assert.That(nupkgA, Does.Exist)

            let filesB = Directory.GetFiles(Path.GetDirectoryName nupkgB)
            Assert.That(filesB, Has.Length.EqualTo(1))

            use zip = new ZipFile(nupkgB)
            let nuspec = zip.ReadEntry("TestLibB.nuspec")
            nuspec |> should contain "<version>3.2.1</version>"
            nuspec |> should contain "<releaseNotes>- TestLibB changes"
        )

    [<Test>]
    let ``[Aardpack] Skip build``() =
        tempDir (fun outputDir ->
            let output = Aardpack.run $"--skip-build --output \"{outputDir}\" \"{testLibAProj}\""
            output |> should contain "Paket:Pack"
            output |> should contain "Github:Release"
            output |> should contain "creating release '1.2.3' with tag '1.2.3' (1 files)"
            output |> should not' (contain "DotNet:Build")

            let nupkg = Path.Combine(outputDir, "TestLibA.1.2.3.nupkg")
            Assert.That(nupkg, Does.Exist)
        )

    [<Test>]
    let ``[Aardpack] No build``() =
        let output = Aardpack.run $"--no-build --release-notes \"{releaseNotesPath}\" {releaseNotesPath}"
        output |> should contain "Github:Release"
        output |> should contain "creating release '9.9.9' with tag '9.9.9' (1 files)"
        output |> should contain (Path.GetFullPath releaseNotesPath)
        output |> should not' (contain "Paket:Pack")
        output |> should not' (contain "Git:Tag")

    [<Test>]
    let ``[Aardpack] No release``() =
        let output = Aardpack.run $"--no-build --no-release --release-notes \"{releaseNotesPath}\" {releaseNotesPath}"
        output |> should contain "Git:Tag"
        output |> should contain "git tag -m 9.9.9 9.9.9"
        output |> should not' (contain "Paket:Pack")
        output |> should not' (contain "Github:Release")

#endif
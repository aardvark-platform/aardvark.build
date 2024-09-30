using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aardvark.Build
{
    public abstract class Task : ToolTask
    {
        private string outputFile;

        private string argumentsFile;

        public bool DesignTime { get; set; }

        public string Verbosity { get; set; }

        [Required]
        public string ToolAssembly { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

        protected override string ToolName => "dotnet";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        private string GetScratchFile()
        {
            var file = new FileInfo(Path.Combine(IntermediateOutputPath, Path.GetRandomFileName()));

            if (file.Exists) return GetScratchFile();
            else
            {
                if (!file.Directory.Exists) Directory.CreateDirectory(file.Directory.FullName);
                return file.FullName;
            }
        }

        protected abstract string Command { get; }

        protected abstract List<string> CommandArgs { get; }

        protected virtual void ProcessOutput(string[] output) { }

        protected override string GenerateCommandLineCommands()
        {
            if (argumentsFile == null)
            {
                throw new NullReferenceException("File for command arguments is not specified.");
            }

            var args = CommandArgs;
            args.Add($"--output-file={outputFile}");
            args.Add($"--verbosity={Verbosity}");
            File.WriteAllLines(argumentsFile, args);

            return $"{ToolAssembly} {Command} \"{argumentsFile}\"";
        }

        protected override string GenerateFullPathToTool() => ToolName;

        public override bool Execute()
        {
            if (DesignTime) return true;

            outputFile = GetScratchFile();
            argumentsFile = GetScratchFile();

            try
            {
                return base.Execute();
            }
            finally
            {
                if (File.Exists(outputFile))
                {
                    var output = File.ReadAllLines(outputFile);
                    File.Delete(outputFile);
                    outputFile = null;

                    ProcessOutput(output);
                }

                if (File.Exists(argumentsFile))
                {
                    File.Delete(argumentsFile);
                    argumentsFile = null;
                }
            }
        }
    }
}

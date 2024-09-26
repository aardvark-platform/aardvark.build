using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aardvark.Build
{
    internal class MemoryMappedLog : IDisposable
    {
        public const long SizeInBytes = 1024 * 1024;

        public string Name { get; private set; }

        private MemoryMappedFile handle;

        // On platforms other than Windows, the mapped memory has to be backed by a file.
        public static bool IsPersisted =>
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public MemoryMappedLog()
        {
            if (!IsPersisted)
            {
                Name = Guid.NewGuid().ToString();
                handle = MemoryMappedFile.CreateNew(Name, SizeInBytes, MemoryMappedFileAccess.ReadWrite);
            }
            else
            {
                Name = Path.GetTempFileName();
                var stream = new FileStream(Name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                handle = MemoryMappedFile.CreateFromFile(stream, null, SizeInBytes, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
            }
        }

        public List<string> ReadAllLines()
        {
            var result = new List<string>();
            using var stream = handle.CreateViewStream(0L, SizeInBytes, MemoryMappedFileAccess.Read);
            using var reader = new StreamReader(stream);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim('\0');
                if (line != string.Empty) result.Add(line);
            }

            return result;
        }

        public void Dispose()
        {
            if (Name != null && IsPersisted && File.Exists(Name))
            {
                try { File.Delete(Name); } catch { }
                Name = null;
            }

            handle?.Dispose();
            handle = null;
        }
    }

    public abstract class Task : ToolTask
    {
        private MemoryMappedLog logOutput;

        private MemoryMappedLog logInfo;

        private MemoryMappedLog logWarn;

        private MemoryMappedLog logError;

        public bool DesignTime { get; set; }

        public string Verbosity { get; set; }

        [Required]
        public string ToolAssembly { get; set; }

        protected override string ToolName => "dotnet";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.Low;

        protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

        protected string PathArg(string path)
        {
            // Funky stuff happening with backlashes and quotes...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && path != null)
                path = path.Replace(@"\", "/");

            return $"\"{path}\"";
        }

        protected abstract string Command { get; }

        protected virtual void ProcessOutput(List<string> output) { }

        protected override string GenerateCommandLineCommands()
        {
            return $"{ToolAssembly} {Command} " +
               $"--log-size={MemoryMappedLog.SizeInBytes} " +
               $"--log-persisted={MemoryMappedLog.IsPersisted} " +
               $"--log-output={PathArg(logOutput.Name)} " +
               $"--log-info={PathArg(logInfo.Name)} " +
               $"--log-warn={PathArg(logWarn.Name)} " +
               $"--log-error={PathArg(logError.Name)} " +
               $"--verbosity={Verbosity}";
        }

        protected override string GenerateFullPathToTool() => ToolName;


        private List<string> ReadAndDisposeLog(ref MemoryMappedLog log)
        {
            var result = log.ReadAllLines();
            log.Dispose();
            log = null;
            return result;
        }

        public override bool Execute()
        {
            if (DesignTime) return true;

            logOutput = new MemoryMappedLog();
            logInfo = new MemoryMappedLog();
            logWarn = new MemoryMappedLog();
            logError = new MemoryMappedLog();

            try
            {
                return base.Execute();
            }
            finally
            {
                foreach (var msg in ReadAndDisposeLog(ref logInfo))
                {
                    Log.LogMessage(MessageImportance.High, $"[Aardvark.Build] {msg}");
                }

                foreach (var msg in ReadAndDisposeLog(ref logWarn))
                {
                    Log.LogWarning(msg);
                }

                foreach (var msg in ReadAndDisposeLog(ref logError))
                {
                    Log.LogError(msg);
                }

                var output = ReadAndDisposeLog(ref logOutput);
                ProcessOutput(output);
            }
        }
    }
}

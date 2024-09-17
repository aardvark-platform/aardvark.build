using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Aardvark.Build
{
    public class NativeDependenciesTask : Task
    {
        public bool Force { get; set; }

        public string RepositoryRoot { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Output]
        public string ZipArchivePath { get; set; }

        protected override string Command
        {
            get
            {
                var path = Path.GetDirectoryName(ProjectPath);

                return "native-deps " +
                       $"--force={Force} " +
                       $"--path={PathArg(path)} " +
                       $"--output-path={PathArg(OutputPath)} " +
                       $"--assembly-name=\"{AssemblyName}\" " +
                       $"--root={PathArg(RepositoryRoot)}";
            }
        }

        protected override void ProcessOutput(List<string> output)
        {
            if (output.Count >= 1)
            {
                ZipArchivePath = output[0];
            }
        }
    }
}

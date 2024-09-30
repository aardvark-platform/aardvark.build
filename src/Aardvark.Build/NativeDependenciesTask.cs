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

        [Output]
        public string ZipArchivePath { get; set; }

        protected override string Command => "native-deps";

        protected override List<string> CommandArgs
        {
            get
            {
                var path = Path.GetDirectoryName(ProjectPath);

                return new([
                    $"--force={Force}",
                    $"--path={path}",
                    $"--output-path={IntermediateOutputPath}",
                    $"--assembly-name={AssemblyName}",
                    $"--root={RepositoryRoot}"
                ]);
            }
        }

        protected override void ProcessOutput(string[] output)
        {
            if (output.Length >= 1)
            {
                ZipArchivePath = output[0];
            }
        }
    }
}

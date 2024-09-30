using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.IO;

namespace Aardvark.Build
{
    public class LocalSourcesTask : Task
    {
        public string RepositoryRoot { get; set; }

        [Required]
        public string[] InputReferences { get; set; }

        [Required]
        public string[] InputCopyLocal { get; set; }

        [Required]
        public string ProjectPath { get; set; }

        [Output]
        public string[] AddReferences { get; set; }

        [Output]
        public string[] RemoveReferences { get; set; }

        [Output]
        public string[] AddCopyLocal { get; set; }

        [Output]
        public string[] RemoveCopyLocal { get; set; }

        protected override string Command => "local-sources";

        protected override List<string> CommandArgs
        {
            get
            {
                var path = Path.GetDirectoryName(ProjectPath);
                var references = string.Join(";", InputReferences);
                var copyLocal = string.Join(";", InputCopyLocal);

                return new([
                    $"--path={path}",
                    $"--references={references}",
                    $"--copy-local={copyLocal}",
                    $"--root={RepositoryRoot}"
                ]);
            }
        }

        protected override void ProcessOutput(string[] output)
        {
            if (output.Length >= 4)
            {
                AddReferences = output[0].Split(';');
                RemoveReferences = output[1].Split(';');
                AddCopyLocal = output[2].Split(';');
                RemoveCopyLocal = output[3].Split(';');
            }
        }
    }
}

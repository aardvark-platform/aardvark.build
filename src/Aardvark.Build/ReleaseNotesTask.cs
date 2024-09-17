using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Aardvark.Build
{
    public class ReleaseNotesTask : Task
    {
        public string RepositoryRoot { get; set; }

        [Required]
        public string ProjectPath { get; set; }

        [Output]
        public string NugetVersion { get; set; }

        [Output]
        public string AssemblyVersion { get; set; }

        [Output]
        public string ReleaseNotes { get; set; }

        protected override string Command
        {
            get
            {
                var path = string.IsNullOrEmpty(RepositoryRoot) ? Path.GetDirectoryName(ProjectPath) : RepositoryRoot;
                return $"notes --path={PathArg(path)}";
            }
        }

        protected override void ProcessOutput(List<string> output)
        {
            if (output.Count >= 2)
            {
                NugetVersion = output[0];
                AssemblyVersion = output[1];

                var notes = new StringBuilder();
                for (int i = 2 ; i < output.Count; i++)
                    notes.AppendLine(output[i]);

                ReleaseNotes = notes.ToString();
            }
        }
    }
}

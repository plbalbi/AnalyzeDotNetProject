using System;
using System.Linq;
using NuGet.ProjectModel;
using CommandLine;
using Microsoft.Build.Evaluation;
using System.Collections.Generic;
using Microsoft.Build.Execution;

namespace AnalyzeDotNetProject
{
    class Program
    {
        public class Options {
            [Option('p', "path", Required = true, HelpText = "Project path")]
            public string ProjectPath { get; set; }
        }
        private const string COMMA_SEPARATOR = ", ";

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(Program.RunFromOptions);
        }

        public static void test(string projectFilePath)
        {
            ProjectCollection pc = new ProjectCollection();
            Dictionary<string, string> GlobalProperty = new Dictionary<string, string>
            {
                { "Configuration", "Debug" },
                { "Platform", "x86" },
                { "TargetFramework", "netcoreapp3.1" },
                { "UseLegacySdkResolver", "true" }
            };
            BuildRequestData BuidlRequest = new BuildRequestData(projectFilePath, GlobalProperty, null, new string[] { "GenerateRestoreGraphFile" }, null);
            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(new BuildParameters(pc), BuidlRequest);
        }

        private static void RunFromOptions(Options options)
        {
            var dependencyGraphService = new DependencyGraphService();
            var dependencyGraph = dependencyGraphService.GenerateDependencyGraph(options.ProjectPath);

            test(options.ProjectPath);

            foreach(var project in dependencyGraph.Projects.Where(p => p.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference))
            {
                // Generate lock file
                var lockFileService = new LockFileService();
                var lockFile = lockFileService.GetLockFile(project.FilePath, project.RestoreMetadata.OutputPath);

                foreach(var targetFramework in project.TargetFrameworks)
                {
                    Console.WriteLine($"{project.Name}  [{targetFramework.FrameworkName}]");

                    var lockFileTargetFramework = lockFile.Targets.FirstOrDefault(t => t.TargetFramework.Equals(targetFramework.FrameworkName));
                    if (lockFileTargetFramework != null)
                    {
                        foreach(var dependency in targetFramework.Dependencies)
                        {
                            var projectLibrary = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == dependency.Name);

                            ReportDependency(projectLibrary, lockFileTargetFramework, 1);
                        }
                    }
                }
            }
        }

        private static void ReportDependency(LockFileTargetLibrary projectLibrary, LockFileTarget lockFileTargetFramework, int indentLevel)
        {
            // First indent
            Console.Write(String.Concat(Enumerable.Repeat("| ", (indentLevel-1)*2)));
            // Then use the last two character to make the heriarchy identifier
            Console.Write("|-");
            Console.WriteLine($"{projectLibrary.Name}, v{projectLibrary.Version}{(projectLibrary.Framework != string.Empty ? COMMA_SEPARATOR + projectLibrary.Framework : string.Empty)}");

            foreach (var childDependency in projectLibrary.Dependencies)
            {
                var childLibrary = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == childDependency.Id);

                ReportDependency(childLibrary, lockFileTargetFramework, indentLevel + 1);
            }
        }
    }
}

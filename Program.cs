using System;
using System.Linq;
using NuGet.ProjectModel;
using CommandLine;
using System.Diagnostics;

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

        private static void RunFromOptions(Options options)
        {
            var dependencyGraphService = new DependencyGraphService();
            var dependencyGraph = dependencyGraphService.GenerateDependencyGraph(options.ProjectPath);

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

							if (projectLibrary == null)
							{
							    Console.WriteLine($"Could not find matching library for dependency: {dependency.Name} - {dependency.LibraryRange.VersionRange}");
							    continue;
							}

                            ReportDependency(projectLibrary, lockFileTargetFramework, 1);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Target not found for TargetFramework: {targetFramework.FrameworkName}");
				    }
                }
            }
        }

        private static void WriteIndentationForLevel(int level)
        { 
            // First indent
            Console.Write(String.Concat(Enumerable.Repeat("| ", (level-1)*2)));
            // Then use the last two character to make the heriarchy identifier
            Console.Write("|-");
		}

        private static void ReportDependency(LockFileTargetLibrary projectLibrary, LockFileTarget lockFileTargetFramework, int indentLevel)
        {
            WriteIndentationForLevel(indentLevel);

            Console.WriteLine($"{projectLibrary.Name}, v{projectLibrary.Version}{(projectLibrary.Framework != string.Empty ? COMMA_SEPARATOR + projectLibrary.Framework : string.Empty)}");

            foreach (var childDependency in projectLibrary.Dependencies)
            {
                var childLibrary = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == childDependency.Id);

                if (childLibrary == null)
                {
                    WriteIndentationForLevel(indentLevel + 1);
                    Console.WriteLine($"Could not find matching library for dependency: {childDependency.Id} - {childDependency.VersionRange}");
                    continue;
				}

                ReportDependency(childLibrary, lockFileTargetFramework, indentLevel + 1);
            }
        }
    }
}

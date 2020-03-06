using System;
using System.Linq;
using NuGet.ProjectModel;

namespace AnalyzeDotNetProject
{
    class Program
    {
        private const string COMMA_SEPARATOR = ", ";

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Missing an argument");
                Environment.Exit(1);
            }

            // Replace to point to your project or solution
            string projectPath = args[0];

            var dependencyGraphService = new DependencyGraphService();
            var dependencyGraph = dependencyGraphService.GenerateDependencyGraph(projectPath);

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

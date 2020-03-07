using System;
using System.Linq;
using NuGet.ProjectModel;
using CommandLine;
using Microsoft.Build.Evaluation;
using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;

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

            Dictionary<string, string> environmentVarsToSet = new Dictionary<string, string>
            {
                {"MSBuildExtensionsPath", "/usr/local/share/dotnet/sdk/3.1.102/" },
                {"MSBuildLoadMicrosoftTargetsReadOnly", "true" },
                {"MSBuildSDKsPath", "/usr/local/share/dotnet/sdk/3.1.102/Sdks" },
            };

            foreach (var envVarName in environmentVarsToSet.Keys)
            {
                Environment.SetEnvironmentVariable(envVarName, environmentVarsToSet[envVarName]);
			}

            ProjectCollection pc = new ProjectCollection();
            Dictionary<string, string> GlobalProperty = new Dictionary<string, string>
            {
                { "Configuration", "Debug" },
                { "Platform", "x86" },
                { "TargetFramework", "netcoreapp3.1" },
                { "UseLegacySdkResolver", "true" },
                {"MSBuildExtensionsPath", "/usr/local/share/dotnet/sdk/3.1.102/" },
                {"MSBuildLoadMicrosoftTargetsReadOnly", "true" },
                {"MSBuildSDKsPath", "/usr/local/share/dotnet/sdk/3.1.102/Sdks" },
            };
            BuildRequestData buildRequest = new BuildRequestData(projectFilePath, GlobalProperty, null, new string[] { "GenerateRestoreGraphFile" }, null);
            BuildManager.DefaultBuildManager.BeginBuild(new BuildParameters
            {
                // NodeExeLocation = "/usr/local/share/dotnet/sdk/3.1.102/Sdk/MSBuild.dll",
                Loggers = new[] { new ConsoleLogger(Microsoft.Build.Framework.LoggerVerbosity.Detailed) },
            });
            var buildSubmission = BuildManager.DefaultBuildManager.PendBuildRequest(buildRequest);
            BuildResult buildResult = buildSubmission.Execute();
        }

        private static void RunFromOptions(Options options)
        {
            var dependencyGraphService = new DependencyGraphService();
            var dependencyGraph = dependencyGraphService.GenerateDependencyGraph(options.ProjectPath);

            test(options.ProjectPath);
            Environment.Exit(1);

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

﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools.FunctionalTests
{
    public class ProjectToolScenario : IDisposable
    {
        private const string NugetConfigFileName = "NuGet.config";
        private static readonly string TestProjectSourceRoot = Path.Combine(AppContext.BaseDirectory, "TestProjects");
        private readonly ITestOutputHelper _logger;

        public ProjectToolScenario()
            : this(null)
        {
        }

        public ProjectToolScenario(ITestOutputHelper logger)
        {
            _logger = logger;
            _logger?.WriteLine($"The temporary test folder is {TempFolder}");
            WorkFolder = Path.Combine(TempFolder, "work");

            CreateTestDirectory();
        }

        public string TempFolder { get; } = Path.Combine(Path.GetDirectoryName(FindNugetConfig()), "testWorkDir", Guid.NewGuid().ToString("N"));

        public string WorkFolder { get; }

        public void AddTestProjectFolder(string projectName)
        {
            var srcFolder = Path.Combine(TestProjectSourceRoot, projectName);
            var destinationFolder = Path.Combine(WorkFolder, Path.GetFileName(projectName));
            _logger?.WriteLine($"Copying project {srcFolder} to {destinationFolder}");

            Directory.CreateDirectory(destinationFolder);

            foreach (var directory in Directory.GetDirectories(srcFolder, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(srcFolder, destinationFolder));
            }

            foreach (var file in Directory.GetFiles(srcFolder, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(srcFolder, destinationFolder), true);
            }
        }

        public Task RestoreAsync(string project)
        {
            _logger?.WriteLine($"Restoring msbuild project in {project}");
            return ExecuteCommandAync(project, "restore");
        }

        public Task BuildAsync(string project)
        {
            _logger?.WriteLine($"Building {project}");
            return ExecuteCommandAync(project, "build");
        }

        private Task ExecuteCommandAync(string project, params string[] arguments)
        {
            project = Path.Combine(WorkFolder, project);
            var spec = new ProcessSpec
            {
                Executable = DotNetMuxer.MuxerPathOrDefault(),
                WorkingDirectory = project,
                Arguments = arguments,
            };

            var process = new AwaitableProcess(spec, _logger);
            process.Start();
            return process.Task;
        }

        private void CreateTestDirectory()
        {
            Directory.CreateDirectory(WorkFolder);

            var nugetConfigFilePath = FindNugetConfig();

            var tempNugetConfigFile = Path.Combine(WorkFolder, NugetConfigFileName);
            File.Copy(nugetConfigFilePath, tempNugetConfigFile);
        }

        public IEnumerable<string> GetDotnetWatchArguments()
        {
            // this launches a new .NET Core process using the runtime of the current test app
            // and the version of dotnet-watch that this test app is compiled against
            var thisAssembly = Path.GetFileNameWithoutExtension(GetType().GetTypeInfo().Assembly.Location);

            var args = new List<string>
            {
                "exec",
                "--depsfile",
                Path.Combine(AppContext.BaseDirectory, thisAssembly + ".deps.json"),
                "--runtimeconfig",
                Path.Combine(AppContext.BaseDirectory, thisAssembly + ".runtimeconfig.json")
            };

            if (AppContext.GetData("FX_DEPS_FILE") is string currentFxVersion)
            {
                // This overrides the version of shared fx in the runtimeconfig.json file.
                // Tests do this to ensure dotnet-watch is executing on the version of Microsoft.NETCore.App
                // used by the current test project. The test project can target multiple version of
                // netcoreapp1.x in order to ensure the tool runs when it is lifted to higher netcore versions
                // due to inclusion of the 'prefercliruntime' file in our nupkg.

                var version = new FileInfo(currentFxVersion).Directory.Name;
                args.Add("--fx-version");
                args.Add(version);
            }

            args.Add(Path.Combine(AppContext.BaseDirectory, "dotnet-watch.dll"));

            return args;
        }

        private static string FindNugetConfig()
        {
            var currentDirPath = AppContext.BaseDirectory;

            string nugetConfigFile;
            while (true)
            {
                nugetConfigFile = Path.Combine(currentDirPath, NugetConfigFileName);
                if (File.Exists(nugetConfigFile))
                {
                    break;
                }

                currentDirPath = Path.GetDirectoryName(currentDirPath);
            }

            return nugetConfigFile;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(TempFolder, recursive: true);
            }
            catch
            {
                Console.WriteLine($"Failed to delete {TempFolder}. Retrying...");
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Directory.Delete(TempFolder, recursive: true);
            }
        }
    }
}

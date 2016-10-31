// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    public class ProjectIdResolver : IDisposable
    {
        private const string TargetsFileName = "FindUserSecretsProperty.targets";
        private readonly ILogger _logger;
        private readonly string _workingDirectory;
        private readonly List<string> _tempFiles = new List<string>();

        public ProjectIdResolver(ILogger logger, string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            _logger = logger;
        }

        public string Resolve(string project, string configuration = Constants.DefaultConfiguration)
        {
            var finder = new MsBuildProjectFinder(_workingDirectory);
            var projectFile = finder.FindMsBuildProject(project);

            _logger.LogDebug(Resources.Message_Project_File_Path, projectFile);

            var targetFile = GetTargetFile();
            var outputFile = Path.GetTempFileName();
            _tempFiles.Add(outputFile);

            var commandOutput = new List<string>();
            var commandResult = Command.CreateDotNet("msbuild",
                new[] {
                    targetFile,
                    "/nologo",
                    "/t:_FindUserSecretsProperty",
                    $"/p:Project={projectFile}",
                    $"/p:OutputFile={outputFile}",
                    $"/p:Configuration={configuration}"
                })
                .CaptureStdErr()
                .CaptureStdOut()
                .OnErrorLine(l => commandOutput.Add(l))
                .OnOutputLine(l => commandOutput.Add(l))
                .Execute();

            if (commandResult.ExitCode != 0)
            {
                _logger.LogDebug(string.Join(Environment.NewLine, commandOutput));
                throw new GracefulException(Resources.FormatError_ProjectFailedToLoad(projectFile));
            }

            var id = File.ReadAllText(outputFile)?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                throw new GracefulException(Resources.FormatError_ProjectMissingId(projectFile));
            }

            return id;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                TryDelete(file);
            }
        }

        private string GetTargetFile()
        {
            var assemblyDir = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);

            // targets should be in one of these locations, depending on test setup and tools installation
            var searchPaths = new[]
            {
                AppContext.BaseDirectory,
                assemblyDir, // next to assembly
                Path.Combine(assemblyDir, "../../tools"), // inside the nupkg
            };

            var foundFile = searchPaths
                .Select(dir => Path.Combine(dir, TargetsFileName))
                .Where(File.Exists)
                .FirstOrDefault();

            if (foundFile != null)
            {
                return foundFile;
            }

            // This should only really happen during testing. Current build system doesn't give us a good way to ensure the
            // test project has an always-up to date version of the targets file.
            // TODO cleanup after we switch to an MSBuild system in which can specify "CopyToOutputDirectory: Always" to resolve this issue
            var outputPath = Path.GetTempFileName();
            using (var resource = GetType().GetTypeInfo().Assembly.GetManifestResourceStream(TargetsFileName))
            using (var stream = new FileStream(outputPath, FileMode.Create))
            {
                resource.CopyTo(stream);
            }

            // cleanup
            _tempFiles.Add(outputPath);

            return outputPath;
        }

        private static void TryDelete(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // whatever
            }
        }
    }
}
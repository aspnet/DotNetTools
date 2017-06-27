// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    public class ProjectIdResolver
    {
        private const string DefaultConfig = "Debug";
        private readonly IReporter _reporter;
        private readonly string _workingDirectory;

        public ProjectIdResolver(IReporter reporter, string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            _reporter = reporter;
        }

        public string Resolve(string project, string configuration)
        {
            var finder = new MsBuildProjectFinder(_workingDirectory);
            var projectFile = finder.FindMsBuildProject(project);
            EnsureProjectExtensionTargetsExist(projectFile);

            _reporter.Verbose(Resources.FormatMessage_Project_File_Path(projectFile));

            configuration = !string.IsNullOrEmpty(configuration)
                ? configuration
                : DefaultConfig;

            var outputFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                var args = new[]
                {
                    "msbuild",
                    projectFile,
                    "/nologo",
                    "/t:_ExtractUserSecretsMetadata", // defined in ProjectIdResolverTargets.xml
                    $"/p:_UserSecretsMetadataFile={outputFile}",
                    $"/p:Configuration={configuration}"
                };
                var psi = new ProcessStartInfo
                {
                    FileName = DotNetMuxer.MuxerPathOrDefault(),
                    Arguments = ArgumentEscaper.EscapeAndConcatenate(args),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

#if DEBUG
                _reporter.Verbose($"Invoking '{psi.FileName} {psi.Arguments}'");
#endif

                var process = Process.Start(psi);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _reporter.Verbose(process.StandardOutput.ReadToEnd());
                    _reporter.Verbose(process.StandardError.ReadToEnd());
                    throw new InvalidOperationException(Resources.FormatError_ProjectFailedToLoad(projectFile));
                }

                var id = File.ReadAllText(outputFile)?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidOperationException(Resources.FormatError_ProjectMissingId(projectFile));
                }
                return id;

            }
            finally
            {
                TryDelete(outputFile);
            }
        }

        private void EnsureProjectExtensionTargetsExist(string projectFile)
        {
            // relies on MSBuildProjectExtensionsPath and Microsoft.Common.targets to import this file
            // into the target project
            var projectExtensionsPath = Path.Combine(
                Path.GetDirectoryName(projectFile),
                "obj",
                $"{Path.GetFileName(projectFile)}.usersecrets.targets");

            Directory.CreateDirectory(Path.GetDirectoryName(projectExtensionsPath));

            // should overwrite the file always. Hypothetically, another version of the user-secrets tool
            // could have already put a file here. We want to ensure the target file matches the currently
            // running tool
            using (var resource = GetType().GetTypeInfo().Assembly.GetManifestResourceStream("ProjectIdResolverTargets.xml"))
            using (var stream = new FileStream(projectExtensionsPath, FileMode.Create))
            {
                resource.CopyTo(stream);
            }
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

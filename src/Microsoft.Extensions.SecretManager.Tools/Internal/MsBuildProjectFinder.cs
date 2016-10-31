﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    internal class MsBuildProjectFinder
    {
        private readonly string _directory;

        public MsBuildProjectFinder(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException(Resources.Common_StringNullOrEmpty, nameof(directory));
            }

            _directory = directory;
        }

        public string FindMsBuildProject(string project)
        {
            var projectPath = project ?? _directory;

            if (!Path.IsPathRooted(projectPath))
            {
                projectPath = Path.Combine(_directory, projectPath);
            }

            if (Directory.Exists(projectPath))
            {
                var projects = Directory.EnumerateFileSystemEntries(projectPath, "*.*proj", SearchOption.TopDirectoryOnly)
                    .Where(f => !".xproj".Equals(Path.GetExtension(f), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (projects.Count > 1)
                {
                    throw new GracefulException(Resources.FormatError_MultipleProjectsFound(projectPath));
                }

                if (projects.Count == 0)
                {
                    throw new GracefulException(Resources.FormatError_NoProjectsFound(projectPath));
                }

                return projects[0];
            }

            if (!File.Exists(projectPath))
            {
                throw new GracefulException(Resources.FormatError_ProjectPath_NotFound(projectPath));
            }

            return projectPath;
        }
    }
}
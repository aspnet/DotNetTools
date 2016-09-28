// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.Extensions.SecretManager.Tools
{
    internal class GracefulProjectFinder : MsBuildProjectFinder
    {
        public GracefulProjectFinder(string directory) 
            : base(directory)
        {
        }

        protected override Exception FileDoesNotExist(string filePath)
            => new GracefulException(Resources.FormatError_ProjectPath_NotFound(filePath));

        protected override Exception MultipleProjectsFound(string directory)
            => new GracefulException($"Multiple MSBuild project files found in '{directory}'. Specify which to use with the --project option.");

        protected override Exception NoProjectsFound(string directory)
            => new GracefulException($"Could not find a MSBuild project file in '{directory}'. Specify which project to use with the --project option.");
    }
}
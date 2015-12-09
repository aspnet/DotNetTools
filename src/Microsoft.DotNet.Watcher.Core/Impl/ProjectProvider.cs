// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using DotNetProject = Microsoft.DotNet.ProjectModel.Project;

namespace Microsoft.DotNet.Watcher.Core
{
    public class ProjectProvider : IProjectProvider
    {
        public bool TryReadProject(string projectFile, out IProject project, out string errors)
        {
            DotNetProject runtimeProject;
            if (!TryGetProject(projectFile, out runtimeProject, out errors))
            {
                project = null;
                return false;
            }

            errors = null;
            project = new Project(runtimeProject);

            return true;
        }
        
        // Same as TryGetProject but it doesn't throw
        private bool TryGetProject(string projectFile, out DotNetProject project, out string errorMessage)
        {
            try
            {
                var errors = new List<DiagnosticMessage>();
                if (!ProjectReader.TryGetProject(projectFile, out project, errors))
                {
                    errorMessage = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
                }
                else
                {
                    errorMessage = null;
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            project = null;
            return false;
        }
    }
}

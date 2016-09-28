// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    public static class ProjectContextExtensions
    {
        public static string GetUserSecretsId(this IProjectContext context)
        {
            var userSecretsId = context.FindProperty("UserSecretsId");

            if (string.IsNullOrEmpty(userSecretsId))
            {
                throw new GracefulException(Resources.FormatError_ProjectMissingId(context.ProjectFullPath));
            }

            return userSecretsId;
        }
    }
}

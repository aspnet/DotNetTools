// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.SecretManager
{
    internal class SecretManagerFactory 
    {
        private readonly Lazy<ProjectLocalSecretsManager> _secretManager;
        private readonly ConfiguredProject _project;

        [ImportingConstructor]
        public SecretManagerFactory(ConfiguredProject project, SVsServiceProvider vsServiceProvider)
        {
            _project = project;

            var serviceProvider = new Lazy<IServiceProvider>(() => vsServiceProvider);

            _secretManager = new Lazy<ProjectLocalSecretsManager>(() =>
            {
                var propertiesProvider = _project.Services.ProjectPropertiesProvider;
                return new ProjectLocalSecretsManager(propertiesProvider, serviceProvider);
            });
        }

        [ExportVsProfferedProjectService(typeof(SVsProjectLocalSecrets))]
        [AppliesTo("LocalUserSecrets")]
        public ProjectLocalSecretsManager ProjectLocalSecretsManager => _secretManager.Value;
    }
}

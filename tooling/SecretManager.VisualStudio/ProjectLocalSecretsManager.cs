// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.Extensions.SecretManager.VisualStudio
{
    using Task = System.Threading.Tasks.Task;

    public class ProjectLocalSecretsManager : IVsProjectSecrets, SVsProjectLocalSecrets
    {
        private const string UserSecretsPropertyName = "UserSecretsId";

        private readonly IProjectProperties _projectProperties;

        public ProjectLocalSecretsManager(IProjectProperties properties)
        {
            _projectProperties = properties;
        }

        public string SanitizeName(string name) => name;

        public IReadOnlyCollection<char> GetInvalidCharactersFrom(string name) => Array.Empty<char>();

        public Task AddSecretAsync(string name, string value, CancellationToken cancellationToken = default)
            => SetSecretAsync(name, value, cancellationToken);

        public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValue(name);

            var store = await GetOrCreateStoreAsync();

            store.Set(name, value);

            if (!cancellationToken.IsCancellationRequested)
            {
                store.Save();
            }
        }

        public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValue(name);
            var store = await GetOrCreateStoreAsync();
            return store[name];
        }

        public async Task<IReadOnlyCollection<string>> GetSecretNamesAsync(CancellationToken cancellationToken = default)
        {
            var store = await GetOrCreateStoreAsync();
            return store.ReadOnlyKeys;
        }

        public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(CancellationToken cancellationToken = default)
        {
            return await GetOrCreateStoreAsync();
        }

        public async Task<bool> RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValue(name);

            var store = await GetOrCreateStoreAsync();

            var result = store.Remove(name);

            if (!cancellationToken.IsCancellationRequested)
            {
                store.Save();
            }

            return result;
        }

        private void EnsureKeyNameIsValue(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(nameof(name));
            }
        }

        private async Task<SecretsStore> GetOrCreateStoreAsync()
        {
            var userSecretsId = await _projectProperties.GetEvaluatedPropertyValueAsync(UserSecretsPropertyName);

            if (string.IsNullOrEmpty(userSecretsId))
            {
                userSecretsId = Guid.NewGuid().ToString();
                await _projectProperties.SetPropertyValueAsync(UserSecretsPropertyName, userSecretsId);
            }

            return new SecretsStore(userSecretsId);
        }
    }
}

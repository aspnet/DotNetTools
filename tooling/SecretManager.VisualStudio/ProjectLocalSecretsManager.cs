// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Extensions.SecretManager.VisualStudio
{
    public class ProjectLocalSecretsManager : IVsProjectSecrets, SVsProjectLocalSecrets
    {
        private const string UserSecretsPropertyName = "UserSecretsId";

        private readonly IProjectPropertiesProvider _propertiesProvider;

        public ProjectLocalSecretsManager(IProjectPropertiesProvider propertiesProvider)
        {
            _propertiesProvider = propertiesProvider ?? throw new ArgumentNullException(nameof(propertiesProvider));
        }

        public string SanitizeName(string name) => name;

        public IReadOnlyCollection<char> GetInvalidCharactersFrom(string name) => Array.Empty<char>();

        public async Task AddSecretAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValid(name);
            var store = await GetOrCreateStoreAsync();
            if (store.ContainsKey(name))
            {
                throw new ArgumentException("A secret with this name already exists.", nameof(name));
            }

            store.Set(name, value);
            cancellationToken.ThrowIfCancellationRequested();
            store.Save();
        }

        public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValid(name);

            var store = await GetOrCreateStoreAsync();

            store.Set(name, value);
            cancellationToken.ThrowIfCancellationRequested();
            store.Save();
        }

        public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValid(name);
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
            EnsureKeyNameIsValid(name);

            var store = await GetOrCreateStoreAsync();

            var result = store.Remove(name);

            cancellationToken.ThrowIfCancellationRequested();
            store.Save();

            return result;
        }

        private void EnsureKeyNameIsValid(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException(nameof(name));
            }
        }

        private async Task<SecretsStore> GetOrCreateStoreAsync()
        {
            var userSecretsId = await _propertiesProvider.GetCommonProperties().GetEvaluatedPropertyValueAsync(UserSecretsPropertyName);

            if (string.IsNullOrEmpty(userSecretsId))
            {
                // TODO how should this be handled?
                throw new InvalidOperationException("UserSecrets cannot be read or altered on this project because 'UserSecretsId' is not set");
            }

            // TODO figure out why this API causes compiler error:
            // "Cannot find the interop type that matches the embedded interop type 'Microsoft.VisualStudio.Shell.Interop.IVsTask'."
            //await TaskScheduler.Default;

            return new SecretsStore(userSecretsId);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.SecretManager;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.SecretManager
{
    /// <summary>
    /// Provides an thread-safe access the secrets.json file based on the UserSecretsId property in a configured project. 
    /// </summary>
    internal class ProjectLocalSecretsManager : Microsoft.VisualStudio.Shell.IVsProjectSecrets, Microsoft.VisualStudio.Shell.SVsProjectLocalSecrets
    {
        private const string UserSecretsPropertyName = "UserSecretsId";

        private readonly AsyncSemaphore _semaphore;
        private readonly IProjectPropertiesProvider _propertiesProvider;

        public ProjectLocalSecretsManager(IProjectPropertiesProvider propertiesProvider)
        {
            _propertiesProvider = propertiesProvider ?? throw new ArgumentNullException(nameof(propertiesProvider));
            _semaphore = new AsyncSemaphore(1);
        }

        public string SanitizeName(string name) => name;

        public IReadOnlyCollection<char> GetInvalidCharactersFrom(string name) => Array.Empty<char>();

        public async Task AddSecretAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValid(name);
            await TaskScheduler.Default;

            using (await _semaphore.EnterAsync())
            {
                var store = await GetOrCreateStoreAsync();
                if (store.ContainsKey(name))
                {
                    throw new ArgumentException("A secret with this name already exists.", nameof(name));
                }

                store.Set(name, value);
                cancellationToken.ThrowIfCancellationRequested();
                store.Save();
            }
        }

        public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValid(name);
            await TaskScheduler.Default;

            using (await _semaphore.EnterAsync())
            {
                var store = await GetOrCreateStoreAsync();

                store.Set(name, value);
                cancellationToken.ThrowIfCancellationRequested();
                store.Save();
            }
        }

        public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValid(name);
            await TaskScheduler.Default;

            using (await _semaphore.EnterAsync())
            {
                var store = await GetOrCreateStoreAsync();
                return store[name];
            }
        }

        public async Task<IReadOnlyCollection<string>> GetSecretNamesAsync(CancellationToken cancellationToken = default)
        {
            await TaskScheduler.Default;

            using (await _semaphore.EnterAsync())
            {
                var store = await GetOrCreateStoreAsync();
                return store.ReadOnlyKeys;
            }
        }


        public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(CancellationToken cancellationToken = default)
        {
            await TaskScheduler.Default;

            using (await _semaphore.EnterAsync())
            {
                return await GetOrCreateStoreAsync();
            }
        }

        public async Task<bool> RemoveSecretAsync(string name, CancellationToken cancellationToken = default)
        {
            EnsureKeyNameIsValid(name);
            await TaskScheduler.Default;

            using (await _semaphore.EnterAsync())
            {
                var store = await GetOrCreateStoreAsync();

                var result = store.Remove(name);

                cancellationToken.ThrowIfCancellationRequested();
                store.Save();

                return result;
            }
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

        private async Task<SecretStore> GetOrCreateStoreAsync()
        {
            var userSecretsId = await _propertiesProvider.GetCommonProperties().GetEvaluatedPropertyValueAsync(UserSecretsPropertyName);

            if (string.IsNullOrEmpty(userSecretsId))
            {
                userSecretsId = Guid.NewGuid().ToString();
                await _propertiesProvider.GetCommonProperties().SetPropertyValueAsync(UserSecretsPropertyName, userSecretsId);
            }

            return new SecretStore(userSecretsId);
        }
    }
}

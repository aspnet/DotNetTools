// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.SecretManager
{
    /// <summary>
    /// Provides read and write access to the secrets.json file for local user secrets.
    /// This is not thread-safe.
    /// This object is meant to have a short lifetime.
    /// When calling <see cref="Save"/>, this will overwrite the secrets.json file. It does not check for concurrency issues if another process has edited this file.
    /// </summary>
    internal class SecretsStore : IReadOnlyDictionary<string, string>
    {
        private readonly Dictionary<string, string> _secrets;

        public SecretsStore(string userSecretsId)
        {
           UserSecretsId = userSecretsId;
            _secrets = Load(userSecretsId);
        }

        protected string UserSecretsId { get; }

        public string this[string key] => _secrets[key];

        public int Count => _secrets.Count;

        public IReadOnlyCollection<string> ReadOnlyKeys => _secrets.Keys;

        public IEnumerable<string> Keys => _secrets.Keys;

        public IEnumerable<string> Values => _secrets.Values;

        public bool ContainsKey(string key) => _secrets.ContainsKey(key);

        public IEnumerable<KeyValuePair<string, string>> AsEnumerable() => _secrets;

        public void Clear() => _secrets.Clear();

        public bool TryGetValue(string key, out string value) => _secrets.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _secrets.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _secrets.GetEnumerator();

        public virtual void Set(string key, string value) => _secrets[key] = value;

        public virtual bool Remove(string key)
        {
            if (_secrets.ContainsKey(key))
            {
                _secrets.Remove(key);
                return true;
            }

            return false;
        }

        public virtual void Save()
        {
            var secretsFilePath = PathHelper.GetSecretsPathFromSecretsId(UserSecretsId);

            Directory.CreateDirectory(Path.GetDirectoryName(secretsFilePath));

            var contents = new JObject();
            if (_secrets != null)
            {
                foreach (var secret in _secrets.AsEnumerable())
                {
                    contents[secret.Key] = secret.Value;
                }
            }

            File.WriteAllText(secretsFilePath, contents.ToString(), Encoding.UTF8);
        }

        protected virtual Dictionary<string, string> Load(string userSecretsId)
        {
            var secretsFilePath = PathHelper.GetSecretsPathFromSecretsId(userSecretsId);

            return new ConfigurationBuilder()
                .AddJsonFile(secretsFilePath, optional: true)
                .Build()
                .AsEnumerable()
                .Where(i => i.Value != null)
                .ToDictionary(i => i.Key, i => i.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}

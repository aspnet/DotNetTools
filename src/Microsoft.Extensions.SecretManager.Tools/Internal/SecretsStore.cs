// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    internal class SecretsStore
    {
        private readonly string _secretsFilePath;
        private IDictionary<string, string> _secrets;

        public SecretsStore(string userSecretsId, ILogger logger)
        {
            if (userSecretsId == null)
            {
                throw new ArgumentNullException(nameof(userSecretsId));
            }

            _secretsFilePath = PathHelper.GetSecretsPathFromSecretsId(userSecretsId);
            
            logger.LogDebug(Resources.Message_Secret_File_Path, _secretsFilePath);

            _secrets = new ConfigurationBuilder()
                .AddJsonFile(_secretsFilePath, optional: true)
                .Build()
                .AsEnumerable()
                .Where(i => i.Value != null)
                .ToDictionary(i => i.Key, i => i.Value, StringComparer.OrdinalIgnoreCase);
        }

        public int Count => _secrets.Count;

        public bool ContainsKey(string key) => _secrets.ContainsKey(key);

        public IEnumerable<KeyValuePair<string, string>> AsEnumerable() => _secrets;

        public void Clear() => _secrets.Clear();

        public void Set(string key, string value) => _secrets[key] = value;

        public void Remove(string key)
        {
            if (_secrets.ContainsKey(key))
            {
                _secrets.Remove(key);
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_secretsFilePath));

            var contents = new JObject();
            if (_secrets != null)
            {
                foreach (var secret in _secrets.AsEnumerable())
                {
                    contents[secret.Key] = secret.Value;
                }
            }

            File.WriteAllText(_secretsFilePath, contents.ToString(), Encoding.UTF8);
        }
    }
}
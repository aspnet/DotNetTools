// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.SecretManager.Tests;
using Microsoft.Extensions.SecretManager.Tools;
using Xunit;

namespace Microsoft.Extensions.Configuration.UserSecrets.Tests
{
    public class ConfigurationExtensionTests
    {

        [Fact]
        public void AddUserSecrets_Does_Not_Fail_On_Non_Existing_File_Explicitly_Passed()
        {
            var builder = new ConfigurationBuilder()
                                .AddUserSecrets(userSecretsId: Guid.NewGuid().ToString());
        }

        [Fact]
        public void AddUserSecrets_Does_Not_Fail_On_Non_Existing_File()
        {
            var projectPath = UserSecretHelper.GetTempSecretProject();

            var builder = new ConfigurationBuilder().SetBasePath(projectPath).AddUserSecrets();
            var configuration = builder.Build();
            Assert.Equal(null, configuration["Facebook:AppSecret"]);

            UserSecretHelper.DeleteTempSecretProject(projectPath);
        }

        [Fact]
        public void AddUserSecrets_With_An_Existing_Secret_File()
        {
            string userSecretsId;
            var projectPath = UserSecretHelper.GetTempSecretProject(out userSecretsId);

            var logger = new TestLogger();
            var secretManager = new Program() { Logger = logger };

            secretManager.Run(new string[] { "set", "Facebook:AppSecret", "value1", "-p", projectPath });

            var builder = new ConfigurationBuilder().SetBasePath(projectPath).AddUserSecrets();

            var configuration = builder.Build();
            Assert.Equal("value1", configuration["Facebook:AppSecret"]);

            UserSecretHelper.DeleteTempSecretProject(projectPath);
        }

        [Fact]
        public void AddUserSecrets_With_SecretsId_Passed_Explicitly()
        {
            string userSecretsId;
            var projectPath = UserSecretHelper.GetTempSecretProject(out userSecretsId);

            var logger = new TestLogger();
            var secretManager = new Program() { Logger = logger };

            secretManager.Run(new string[] { "set", "Facebook:AppSecret", "value1", "-p", projectPath });

            var builder = new ConfigurationBuilder()
                                .AddUserSecrets(userSecretsId: userSecretsId);
            var configuration = builder.Build();

            Assert.Equal("value1", configuration["Facebook:AppSecret"]);
            UserSecretHelper.DeleteTempSecretProject(projectPath);
        }
    }
}
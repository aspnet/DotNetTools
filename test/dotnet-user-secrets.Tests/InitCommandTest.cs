// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration.UserSecrets.Tests;
using Microsoft.Extensions.SecretManager.Tools.Internal;
using Microsoft.Extensions.Tools.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Extensions.SecretManager.Tools.Tests
{
    public class InitCommandTests : IClassFixture<UserSecretsTestFixture>
    {
        private UserSecretsTestFixture _fixture;
        private ITestOutputHelper _output;
        private TestConsole _console;
        private StringBuilder _textOutput;

        public InitCommandTests(UserSecretsTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _textOutput = new StringBuilder();

            _console = new TestConsole(output)
            {
                Error = new StringWriter(_textOutput),
                Out = new StringWriter(_textOutput),
            };
        }

        private CommandContext MakeCommandContext() => new CommandContext(null, new TestReporter(_output), _console);

        [Fact]
        public void AddsSecretIdToProject()
        {
            var projectDir = _fixture.CreateProject(null);

            new InitCommand(null, null).Execute(MakeCommandContext(), projectDir);

            var idResolver = new ProjectIdResolver(MakeCommandContext().Reporter, projectDir);

            Assert.False(string.IsNullOrWhiteSpace(idResolver.Resolve(null, null)));
        }

        [Fact]
        public void AddsSpecificSecretIdToProject()
        {
            const string SecretId = "TestSecretId";

            var projectDir = _fixture.CreateProject(null);

            new InitCommand(SecretId, null).Execute(MakeCommandContext(), projectDir);

            var idResolver = new ProjectIdResolver(MakeCommandContext().Reporter, projectDir);

            Assert.Equal(SecretId, idResolver.Resolve(null, null));
        }

        [Fact]
        public void AddsEscapedSpecificSecretIdToProject()
        {
            const string SecretId = @"<lots of XML invalid values>&";

            var projectDir = _fixture.CreateProject(null);

            new InitCommand(SecretId, null).Execute(MakeCommandContext(), projectDir);

            var idResolver = new ProjectIdResolver(MakeCommandContext().Reporter, projectDir);

            Assert.Equal(SecretId, idResolver.Resolve(null, null));
        }

        [Fact]
        public void DoesNotGenerateIdForProjectWithSecretId()
        {
            const string SecretId = "AlreadyExists";

            var projectDir = _fixture.CreateProject(SecretId);

            new InitCommand(null, null).Execute(MakeCommandContext(), projectDir);

            var idResolver = new ProjectIdResolver(MakeCommandContext().Reporter, projectDir);

            Assert.Equal(SecretId, idResolver.Resolve(null, null));
        }

        [Fact]
        public void OverridesIdForProjectWithSecretId()
        {
            const string SecretId = "AlreadyExists";
            const string NewId = "TestValue";

            var projectDir = _fixture.CreateProject(SecretId);

            new InitCommand(NewId, null).Execute(MakeCommandContext(), projectDir);

            var idResolver = new ProjectIdResolver(MakeCommandContext().Reporter, projectDir);

            Assert.Equal(NewId, idResolver.Resolve(null, null));
        }

        [Fact]
        public void FailsForInvalidId()
        {
            string secretId = $"invalid{Path.GetInvalidPathChars()[0]}secret-id";

            var projectDir = _fixture.CreateProject(null);

            Assert.Throws<ArgumentException>(() =>
            {
                new InitCommand(secretId, null).Execute(MakeCommandContext(), projectDir);
            });
        }
    }
}

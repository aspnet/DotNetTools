// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.ProjectModel.Tests;
using Microsoft.Extensions.SecretManager.Tools.Internal;
using Moq;
using Xunit;
using Microsoft.Extensions.ProjectModel;
using System;

namespace Microsoft.Extensions.SecretManager.Tools.Tests
{
    public class MsBuildProjectContextFactoryTest : IClassFixture<MsBuildFixture>
    {
        private readonly MsBuildFixture _fixture;

        public MsBuildProjectContextFactoryTest(MsBuildFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void ReadsSimpleCsProj()
        {
            var xml = @"<Project ToolsVersion=""14.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" />

  <PropertyGroup>
    <RootNamespace>Microsoft.TestProject</RootNamespace>
    <ProjectName>TestProject</ProjectName>
    <OutputType>Library</OutputType>
    <TargetFrameworkIdentifier>.NETCoreApp</TargetFrameworkIdentifier>
    <TargetFrameworkVersion>v1.0</TargetFrameworkVersion>
    <UserSecretsId>abc123</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include=""**\*.cs"" Exclude=""Excluded.cs"" />
  </ItemGroup>

  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>";

            var projectFile = new Mock<IFileInfo>();
            projectFile.Setup(f => f.Exists).Returns(true);
            projectFile.Setup(f => f.PhysicalPath).Returns("/tmp/consoleapp.csproj");
            projectFile.Setup(f => f.CreateReadStream()).Returns(() => new MemoryStream(Encoding.Unicode.GetBytes(xml)));
            var project = new MsBuildProjectContextBuilder()
                .UseMsBuild(_fixture.GetMsBuildContext())
                .AsDesignTimeBuild()
                .WithProjectFile(projectFile.Object)
                .WithBuildTargets(Array.Empty<string>())
                .Build();

            var id = project.GetUserSecretsId();

            Assert.Equal("abc123", id);
        }

        [Fact]
        public void MissingId()
        {
            var xml = @"<Project ToolsVersion=""14.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
  </PropertyGroup>
</Project>";

            var projectFile = new Mock<IFileInfo>();
            projectFile.Setup(f => f.Exists).Returns(true);
            projectFile.Setup(f => f.PhysicalPath).Returns("/projectdir/file.csproj");
            projectFile.Setup(f => f.CreateReadStream()).Returns(() => new MemoryStream(Encoding.Unicode.GetBytes(xml)));
            var project = new MsBuildProjectContextBuilder()
                .AsDesignTimeBuild()
                .UseMsBuild(_fixture.GetMsBuildContext())
                .WithProjectFile(projectFile.Object)
                .WithBuildTargets(Array.Empty<string>())
                .Build();

            Assert.Throws<GracefulException>(() => project.GetUserSecretsId());
        }
    }
}

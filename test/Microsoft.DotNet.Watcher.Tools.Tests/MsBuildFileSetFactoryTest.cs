// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNetWatcher.Tools.Tests
{
    public class MsBuildFileSetFactoryTest : IDisposable
    {
        private Stack<IDisposable> _disposables = new Stack<IDisposable>();
        private ILogger _logger;
        public MsBuildFileSetFactoryTest(ITestOutputHelper output)
        {
            _logger = new XunitLogger(output);
        }

        [Fact]
        public async Task SingleTfm()
        {
            var tempDir = new TemporaryDirectory();
            _disposables.Push(tempDir);

            TemporaryCSharpProject target;

            tempDir
                .SubDir("src")
                    .SubDir("Project1")
                        .WithCSharpProject("Project1", out target)
                        .WithTargetFrameworks("netcoreapp1.0")
                        .WithDefaultGlobs()
                        .Dir()
                        .WithFile("Program.cs")
                        .WithFile("Class1.cs")
                        .SubDir("obj").WithFile("ignored.cs").Up()
                        .SubDir("Properties").WithFile("Strings.resx").Up()
                    .Up()
                .Up()
                .Create();

            var filesetFactory = new MsBuildFileSetFactory(_logger, target.Path);
            var createTask = filesetFactory.CreateAsync(CancellationToken.None);
            var finished = await Task.WhenAny(createTask, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(createTask, finished);

            AssertEx.EqualFileList(
                tempDir.Root,
                new[]
                {
                    "src/Project1/Project1.csproj",
                    "src/Project1/Program.cs",
                    "src/Project1/Class1.cs",
                    "src/Project1/Properties/Strings.resx",
                },
                createTask.Result
            );
        }

        [Fact]
        public async Task MultiTfm()
        {
            var tempDir = new TemporaryDirectory();
            _disposables.Push(tempDir);

            TemporaryCSharpProject target;
            tempDir
                .SubDir("src")
                    .SubDir("Project1")
                        .WithCSharpProject("Project1", out target)
                        .WithTargetFrameworks("netcoreapp1.0", "net451")
                        .WithItem("Compile", "Class1.netcore.cs", "'$(TargetFramework)'=='netcoreapp1.0'")
                        .WithItem("Compile", "Class1.desktop.cs", "'$(TargetFramework)'=='net451'")
                        .Dir()
                        .WithFile("Class1.netcore.cs")
                        .WithFile("Class1.desktop.cs")
                        .WithFile("Class1.notincluded.cs")
                    .Up()
                .Up()
                .Create();

            var filesetFactory = new MsBuildFileSetFactory(_logger, target.Path);
            var createTask = filesetFactory.CreateAsync(CancellationToken.None);
            var finished = await Task.WhenAny(createTask, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(createTask, finished);

            AssertEx.EqualFileList(
                tempDir.Root,
                new[]
                {
                    "src/Project1/Project1.csproj",
                    "src/Project1/Class1.netcore.cs",
                    "src/Project1/Class1.desktop.cs",
                },
                createTask.Result
            );
        }

        [Fact]
        public async Task ProjectReferences_OneLevel()
        {
            var tempDir = new TemporaryDirectory();
            _disposables.Push(tempDir);

            TemporaryCSharpProject target;
            TemporaryCSharpProject proj2;
            tempDir
                .SubDir("src")
                    .SubDir("Project2")
                        .WithCSharpProject("Project2", out proj2)
                        .WithTargetFrameworks("netstandard1.1")
                        .WithDefaultGlobs()
                        .Dir()
                        .WithFile("Class2.cs")
                    .Up()
                    .SubDir("Project1")
                        .WithCSharpProject("Project1", out target)
                        .WithTargetFrameworks("netcoreapp1.0", "net451")
                        .WithProjectReference(proj2)
                        .WithDefaultGlobs()
                        .Dir()
                        .WithFile("Class1.cs")
                    .Up()
                .Up()
                .Create();

            var filesetFactory = new MsBuildFileSetFactory(_logger, target.Path);
            var createTask = filesetFactory.CreateAsync(CancellationToken.None);
            var finished = await Task.WhenAny(createTask, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(createTask, finished);

            AssertEx.EqualFileList(
                tempDir.Root,
                new[]
                {
                    "src/Project2/Project2.csproj",
                    "src/Project2/Class2.cs",
                    "src/Project1/Project1.csproj",
                    "src/Project1/Class1.cs",
                },
                createTask.Result
            );
        }

        [Fact]
        public async Task TransitiveProjectReferences_TwoLevels()
        {
            var tempDir = new TemporaryDirectory();
            _disposables.Push(tempDir);

            TemporaryCSharpProject target;
            TemporaryCSharpProject proj2;
            TemporaryCSharpProject proj3;
            tempDir
                .SubDir("src")
                    .SubDir("Project3")
                        .WithCSharpProject("Project3", out proj3)
                        .WithTargetFrameworks("netstandard1.0")
                        .WithDefaultGlobs()
                        .Dir()
                        .WithFile("Class3.cs")
                    .Up()
                    .SubDir("Project2")
                        .WithCSharpProject("Project2", out proj2)
                        .WithTargetFrameworks("netstandard1.1")
                        .WithProjectReference(proj3)
                        .WithDefaultGlobs()
                        .Dir()
                        .WithFile("Class2.cs")
                    .Up()
                    .SubDir("Project1")
                        .WithCSharpProject("Project1", out target)
                        .WithTargetFrameworks("netcoreapp1.0", "net451")
                        .WithProjectReference(proj2)
                        .WithDefaultGlobs()
                        .Dir()
                        .WithFile("Class1.cs")
                    .Up()
                .Up()
                .Create();

            var filesetFactory = new MsBuildFileSetFactory(_logger, target.Path);
            var createTask = filesetFactory.CreateAsync(CancellationToken.None);
            var finished = await Task.WhenAny(createTask, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(createTask, finished);

            AssertEx.EqualFileList(
                tempDir.Root,
                new[]
                {
                    "src/Project3/Project3.csproj",
                    "src/Project3/Class3.cs",
                    "src/Project2/Project2.csproj",
                    "src/Project2/Class2.cs",
                    "src/Project1/Project1.csproj",
                    "src/Project1/Class1.cs",
                },
                createTask.Result
            );
        }

        public void Dispose()
        {
            while (_disposables.Count > 0)
            {
                _disposables.Pop().Dispose();
            }
        }
    }
}
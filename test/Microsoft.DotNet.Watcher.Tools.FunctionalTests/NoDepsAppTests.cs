﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools.FunctionalTests
{
    public class NoDepsAppTests : IDisposable
    {
        private readonly WatchableApp _app;

        public NoDepsAppTests(ITestOutputHelper logger)
        {
            _app = new WatchableApp("NoDepsApp", logger);
        }

        [Fact]
        public async Task RestartProcessOnFileChange()
        {
            await _app.StartWatcherAsync(new[] { "--no-exit" });
            var pid = await _app.GetProcessId().OrTimeout();

            // Then wait for it to restart when we change a file
            var fileToChange = Path.Combine(_app.SourceDirectory, "Program.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await _app.HasRestarted().OrTimeout();
            var pid2 = await _app.GetProcessId().OrTimeout();
            Assert.NotEqual(pid, pid2);

            // first app should have shut down
            Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
        }

        [Fact]
        public async Task RestartProcessThatTerminatesAfterFileChange()
        {
            await _app.StartWatcherAsync();
            var pid = await _app.GetProcessId().OrTimeout();
            await _app.HasExited().OrTimeout(); // process should exit after run

            var fileToChange = Path.Combine(_app.SourceDirectory, "Program.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await _app.HasRestarted().OrTimeout();
            var pid2 = await _app.GetProcessId().OrTimeout();
            Assert.NotEqual(pid, pid2);
            await _app.HasExited().OrTimeout(); // process should exit after run
        }

        public void Dispose()
        {
            _app.Dispose();
        }
    }
}

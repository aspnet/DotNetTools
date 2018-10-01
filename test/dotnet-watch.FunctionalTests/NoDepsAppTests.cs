// Copyright (c) .NET Foundation. All rights reserved.
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
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        private readonly WatchableApp _app;
        private readonly ITestOutputHelper _output;

        public NoDepsAppTests(ITestOutputHelper logger)
        {
            _app = new WatchableApp("NoDepsApp", logger);
            _output = logger;
        }

        [Fact]
        public async Task RestartProcessOnFileChange()
        {
            await _app.StartWatcherAsync(new[] { "--no-exit" });
            var pid = await _app.GetProcessId();

            // Then wait for it to restart when we change a file
            var fileToChange = Path.Combine(_app.SourceDirectory, "Program.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await _app.HasRestarted();
            Assert.DoesNotContain(_app.Process.Output, l => l.StartsWith("Exited with error code"));

            var pid2 = await _app.GetProcessId();
            Assert.NotEqual(pid, pid2);
        }

        [Fact]
        public async Task RestartProcessThatTerminatesAfterFileChange()
        {
            await _app.StartWatcherAsync();
            var pid = await _app.GetProcessId();
            await _app.HasExited(); // process should exit after run
            await _app.IsWaitingForFileChange();

            var fileToChange = Path.Combine(_app.SourceDirectory, "Program.cs");

            try
            {
                File.SetLastWriteTime(fileToChange, DateTime.Now);
                await _app.HasRestarted();
            }
            catch
            {
                // retry
                File.SetLastWriteTime(fileToChange, DateTime.Now);
                await _app.HasRestarted();
            }

            var pid2 = await _app.GetProcessId();
            Assert.NotEqual(pid, pid2);
            await _app.HasExited(); // process should exit after run
        }

        public void Dispose()
        {
            _app.Dispose();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools.FunctionalTests
{
    public class WatchableApp : IDisposable
    {
        private static readonly TimeSpan DefaultMessageTimeOut = TimeSpan.FromSeconds(30);

        private const string StartedMessage = "Started";
        private const string ExitingMessage = "Exiting";
        private const string WatchExitedMessage = "watch : Exited";
        private const string WaitingForFileChangeMessage = "watch : Waiting for a file to change";

        private readonly ITestOutputHelper _logger;
        private string _appName;
        private bool _prepared;


        public WatchableApp(string appName, ITestOutputHelper logger)
        {
            _logger = logger;
            _appName = appName;
            Scenario = new ProjectToolScenario(logger);
            Scenario.AddTestProjectFolder(appName);
            SourceDirectory = Path.Combine(Scenario.WorkFolder, appName);
        }

        public ProjectToolScenario Scenario { get; }

        public AwaitableProcess Process { get; protected set; }

        public string SourceDirectory { get; }

        public Task HasRestarted()
            => Process.GetOutputLineAsync(StartedMessage, DefaultMessageTimeOut);

        public async Task HasExited()
        {
            await Process.GetOutputLineAsync(ExitingMessage, DefaultMessageTimeOut);
            await Process.GetOutputLineStartsWithAsync(WatchExitedMessage, DefaultMessageTimeOut);
        }

        public async Task IsWaitingForFileChange()
        {
            await Process.GetOutputLineStartsWithAsync(WaitingForFileChangeMessage, DefaultMessageTimeOut);
        }

        public bool UsePollingWatcher { get; set; }

        public async Task<int> GetProcessId()
        {
            var line = await Process.GetOutputLineStartsWithAsync("PID =", DefaultMessageTimeOut);
            var pid = line.Split('=').Last();
            return int.Parse(pid);
        }

        public async Task PrepareAsync()
        {
            await Scenario.RestoreAsync(_appName);
            await Scenario.BuildAsync(_appName);
            _prepared = true;
        }

        public void Start(IEnumerable<string> arguments, [CallerMemberName] string name = null)
        {
            if (!_prepared)
            {
                throw new InvalidOperationException($"Call {nameof(PrepareAsync)} first");
            }

            var args = new List<string>
            {
                Scenario.DotNetWatchPath,
            };
            args.AddRange(arguments);

            var spec = new ProcessSpec
            {
                Executable = DotNetMuxer.MuxerPathOrDefault(),
                Arguments = args,
                WorkingDirectory = SourceDirectory,
                EnvironmentVariables =
                {
                    ["DOTNET_CLI_CONTEXT_VERBOSE"] = bool.TrueString,
                    ["DOTNET_USE_POLLING_FILE_WATCHER"] = UsePollingWatcher.ToString(),
                },
            };

            Process = new AwaitableProcess(spec, _logger);
            Process.Start();
        }

        public Task StartWatcherAsync([CallerMemberName] string name = null)
            => StartWatcherAsync(Array.Empty<string>(), name);

        public async Task StartWatcherAsync(string[] arguments, [CallerMemberName] string name = null)
        {
            if (!_prepared)
            {
                await PrepareAsync();
            }

            var args = new[] { "run", "--" }.Concat(arguments);
            Start(args, name);

            // Make this timeout long because it depends much on the MSBuild compilation speed.
            // Slow machines may take a bit to compile and boot test apps
            await Process.GetOutputLineAsync(StartedMessage, TimeSpan.FromMinutes(2));
        }

        public virtual void Dispose()
        {
            _logger?.WriteLine("Disposing WatchableApp");
            Process?.Dispose();
            Scenario?.Dispose();
        }
    }
}

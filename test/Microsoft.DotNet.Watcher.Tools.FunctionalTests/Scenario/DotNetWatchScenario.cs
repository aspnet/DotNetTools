﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Internal;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools.FunctionalTests
{
    public class DotNetWatchScenario : IDisposable
    {
        protected ProjectToolScenario Scenario { get; }
        public DotNetWatchScenario()
            : this(null)
        {
        }

        public DotNetWatchScenario(ITestOutputHelper logger)
        {
            Scenario = new ProjectToolScenario(logger);
        }

        public Process WatcherProcess { get; private set; }

        public bool UsePollingWatcher { get; set; }

        protected void RunDotNetWatch(IEnumerable<string> arguments, string workingFolder)
        {
            IDictionary<string, string> envVariables = null;
            if (UsePollingWatcher)
            {
                envVariables = new Dictionary<string, string>()
                {
                    ["DOTNET_USE_POLLING_FILE_WATCHER"] = "true"
                };
            }

            WatcherProcess = Scenario.ExecuteDotnetWatch(arguments, workingFolder, envVariables);
        }

        public virtual void Dispose()
        {
            if (WatcherProcess != null)
            {
                if (!WatcherProcess.HasExited)
                {
                    WatcherProcess.KillTree();
                }
                WatcherProcess.Dispose();
            }
            Scenario.Dispose();
        }
    }
}

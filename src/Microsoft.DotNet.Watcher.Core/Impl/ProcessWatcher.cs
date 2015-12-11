// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Watcher.Core
{
    public class ProcessWatcher : IProcessWatcher
    {
        private Process _runningProcess;

        public int Start(string executable, string arguments, string workingDir)
        {
            // This is not thread safe but it will not run in a multithreaded environment so don't worry
            if (_runningProcess != null)
            {
                throw new InvalidOperationException("The previous process is still running");
            }

            _runningProcess = new Process();
            _runningProcess.StartInfo = new ProcessStartInfo()
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = workingDir
            };

           
            _runningProcess.Start();

            return _runningProcess.Id;
        }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => _runningProcess?.Kill());
            
            return Task.Run(() => 
            {
                _runningProcess.WaitForExit();

                var exitCode = _runningProcess.ExitCode;
                _runningProcess = null;

                return exitCode;
            });
        }
    }
}
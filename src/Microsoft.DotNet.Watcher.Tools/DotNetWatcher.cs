// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher
{
    public class DotNetWatcher
    {
        private readonly IReporter _reporter;
        private readonly ProcessRunner _processRunner;

        public DotNetWatcher(IReporter reporter)
        {
            Ensure.NotNull(reporter, nameof(reporter));

            _reporter = reporter;
            _processRunner = new ProcessRunner(reporter);
        }

        public async Task WatchAsync(ProcessSpec processSpec, IFileSetFactory fileSetFactory,
            CancellationToken cancellationToken)
        {
            Ensure.NotNull(processSpec, nameof(processSpec));

            var cancelledTaskSource = new TaskCompletionSource<object>();
            cancellationToken.Register(state => ((TaskCompletionSource<object>) state).TrySetResult(null),
                cancelledTaskSource);

            while (true)
            {
                var fileSet = await fileSetFactory.CreateAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                using (var currentRunCancellationSource = new CancellationTokenSource())
                using (var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    currentRunCancellationSource.Token))
                using (var fileSetWatcher = new FileSetWatcher(fileSet))
                {
                    var fileSetTask = fileSetWatcher.GetChangedFileAsync(combinedCancellationSource.Token);
                    var processTask = _processRunner.RunAsync(processSpec, combinedCancellationSource.Token);

                    var args = ArgumentEscaper.EscapeAndConcatenate(processSpec.Arguments);
                    _reporter.Verbose($"Running {processSpec.ShortDisplayName()} with the following arguments: {args}");

                    _reporter.Output("Started");

                    var finishedTask = await Task.WhenAny(processTask, fileSetTask, cancelledTaskSource.Task);

                    // Regardless of the which task finished first, make sure everything is cancelled
                    // and wait for dotnet to exit. We don't want orphan processes
                    currentRunCancellationSource.Cancel();

                    await Task.WhenAll(processTask, fileSetTask);

                    if (processTask.Result == 0)
                    {
                        _reporter.Output("Exited");
                    }
                    else
                    {
                        _reporter.Error($"Exited with error code {processTask.Result}");
                    }

                    if (finishedTask == cancelledTaskSource.Task || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (finishedTask == processTask)
                    {
                        _reporter.Warn("Waiting for a file to change before restarting dotnet...");

                        // Now wait for a file to change before restarting process
                        await fileSetWatcher.GetChangedFileAsync(cancellationToken);
                    }

                    if (!string.IsNullOrEmpty(fileSetTask.Result))
                    {
                        _reporter.Output($"File changed: {fileSetTask.Result}");
                    }
                }
            }
        }
    }
}
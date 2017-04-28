// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Tools.Internal;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools.FunctionalTests
{
    public class AwaitableProcess : IDisposable
    {
        private Process _process;
        private readonly ProcessSpec _spec;
        private readonly TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private BufferBlock<string> _source;
        private ITestOutputHelper _logger;
        private int _reading;

        public AwaitableProcess(ProcessSpec spec, ITestOutputHelper logger)
        {
            _spec = spec;
            _logger = logger;
        }

        public void Start()
        {
            if (_process != null)
            {
                throw new InvalidOperationException("Already started");
            }

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = _spec.Executable,
                    WorkingDirectory = _spec.WorkingDirectory,
                    Arguments = ArgumentEscaper.EscapeAndConcatenate(_spec.Arguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            _process.EnableRaisingEvents = true;
            _process.Exited += OnExited;
            _process.Start();

            _logger.WriteLine($"{DateTime.Now}: process start: '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}'");
            StartProcessingOutput(_process.StandardOutput);
            StartProcessingOutput(_process.StandardError); ;
        }

        public Task Task => _tcs.Task;

        public Task<string> GetOutputLineAsync(string message)
            => GetOutputLineAsync(m => message == m);

        public async Task<string> GetOutputLineAsync(Predicate<string> predicate)
        {
            while (!_source.Completion.IsCompleted)
            {
                while (await _source.OutputAvailableAsync(_cts.Token))
                {
                    var next = await _source.ReceiveAsync(_cts.Token);
                    _logger.WriteLine($"{DateTime.Now}: recv: '{next}'");
                    if (predicate(next))
                    {
                        return next;
                    }
                }
            }

            return null;
        }

        public async Task<IList<string>> GetAllOutputLines()
        {
            var lines = new List<string>();
            while (!_source.Completion.IsCompleted)
            {
                while (await _source.OutputAvailableAsync(_cts.Token))
                {
                    var next = await _source.ReceiveAsync(_cts.Token);
                    _logger.WriteLine($"{DateTime.Now}: recv: '{next}'");
                    lines.Add(next);
                }
            }
            return lines;
        }

        private void StartProcessingOutput(StreamReader streamReader)
        {
            _source = _source ?? new BufferBlock<string>();
            Interlocked.Increment(ref _reading);
            Task.Run(async () =>
            {
                Task<string> line;
                while ((line = await Task.WhenAny(_tcs.Task, streamReader.ReadLineAsync())).Result != null)
                {
                    _logger.WriteLine($"{DateTime.Now}: post: '{line.Result}'");
                    _source.Post(line.Result);
                }

                if (Interlocked.Decrement(ref _reading) <= 0 || _tcs.Task.IsCompleted || _tcs.Task.IsCanceled)
                {
                    _source.Complete();
                }
            }).ConfigureAwait(false);
        }

        private void OnExited(object sender, EventArgs args)
        {
            _tcs.TrySetResult(null);
            _cts.Cancel();
        }

        public void Dispose()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.KillTree();
                _process.Exited -= OnExited;
            }

            _cts.Dispose();
        }
    }
}

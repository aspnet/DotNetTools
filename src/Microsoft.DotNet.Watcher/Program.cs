// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Watcher.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Watcher
{
    public class Program
    {
        private const string DotnetWatchArgumentSeparator = "--dotnet-args";

        private readonly ILoggerFactory _loggerFactory;

        public Program()
        {
            _loggerFactory = new LoggerFactory();

            var commandProvider = new CommandOutputProvider(PlatformServices.Default.Runtime);
            _loggerFactory.AddProvider(commandProvider);
        }

        public static int Main(string[] args)
        {
            using (CancellationTokenSource ctrlCTokenSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, ev) =>
                {
                    ctrlCTokenSource.Cancel();
                    ev.Cancel = false;
                };

                string[] watchArgs, dotnetArgs;
                SeparateWatchArguments(args, out watchArgs, out dotnetArgs);

                return new Program().MainInternal(watchArgs, dotnetArgs, ctrlCTokenSource.Token);
            }
        }

        internal static void SeparateWatchArguments(string[] args, out string[] watchArgs, out string[] dotnetArgs)
        {
            int argsIndex = -1;
            watchArgs = args.TakeWhile((arg, idx) =>
            {
                argsIndex = idx;
                return !string.Equals(arg, DotnetWatchArgumentSeparator, StringComparison.OrdinalIgnoreCase);
            }).ToArray();

            dotnetArgs = args.Skip(argsIndex + 1).ToArray();

            if (dotnetArgs.Length == 0)
            {
                // If no explicit dotnet arguments then all arguments get passed to dotnet
                dotnetArgs = watchArgs;
                watchArgs = new string[0];
            }
        }

        private int MainInternal(string[] watchArgs, string[] dotnetArgs, CancellationToken cancellationToken)
        {
            var app = new CommandLineApplication();
            app.Name = "dotnet-watch";
            app.FullName = "Microsoft .NET File Watcher";
            
            app.HelpOption("-?|-h|--help");

            // Show help information if no subcommand/option was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });
            
            var projectArg = app.Option(
                "--project <PATH>",
                "Path to the project.json file or the application folder. Defaults to the current folder if not provided. Will be passed to dotnet.",
                CommandOptionType.SingleValue);

            var workingDirArg = app.Option(
                "--workingDir <DIR>",
                "The working directory for dotnet. Defaults to the current directory.",
                CommandOptionType.SingleValue);

            // This option is here just to be displayed in help
            // it will not be parsed because it is removed before the code is executed
            app.Option(
                $"{DotnetWatchArgumentSeparator} <ARGS>",
                "Marks the arguments that will be passed to dotnet. Anything following this option is passed. If not specified, all the arguments are passed to dotnet.",
                CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                var projectToRun = projectArg.HasValue() ?
                    projectArg.Value() :
                    Directory.GetCurrentDirectory();

                if (!projectToRun.EndsWith("project.json", StringComparison.Ordinal))
                {
                    projectToRun = Path.Combine(projectToRun, "project.json");
                }

                var workingDir = workingDirArg.HasValue() ?
                    workingDirArg.Value() :
                    Directory.GetCurrentDirectory();

                var watcher = DotNetWatcher.CreateDefault(_loggerFactory);
                try
                {
                    watcher.WatchAsync(projectToRun, dotnetArgs, workingDir, cancellationToken).Wait();
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerExceptions.Count != 1 || !(ex.InnerException is TaskCanceledException))
                    {
                        throw;
                    }
                }


                return 1;
            });

            return app.Execute(watchArgs);
        }
    }
}

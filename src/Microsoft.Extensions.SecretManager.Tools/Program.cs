// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.SecretManager.Tools.Internal;

namespace Microsoft.Extensions.SecretManager.Tools
{
    public class Program
    {
        private ILogger _logger;
        private CommandOutputProvider _loggerProvider;
        private readonly TextWriter _consoleOutput;
        private readonly string _workingDirectory;

        public static int Main(string[] args)
        {
            HandleDebugFlag(ref args);

            int rc;
            new Program(Console.Out, Directory.GetCurrentDirectory()).TryRun(args, out rc);
            return rc;
        }

        internal Program(TextWriter consoleOutput, string workingDirectory)
        {
            _consoleOutput = consoleOutput;
            _workingDirectory = workingDirectory;

            var loggerFactory = new LoggerFactory();
            CommandOutputProvider = new CommandOutputProvider();
            loggerFactory.AddProvider(CommandOutputProvider);
            Logger = loggerFactory.CreateLogger<Program>();
        }

        public ILogger Logger
        {
            get { return _logger; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                _logger = value;
            }
        }

        public CommandOutputProvider CommandOutputProvider
        {
            get { return _loggerProvider; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                _loggerProvider = value;
            }
        }

        [Conditional("DEBUG")]
        private static void HandleDebugFlag(ref string[] args)
        {
            for (var i = 0; i < args.Length; ++i)
            {
                if (args[i] == "--debug")
                {
                    Console.WriteLine("Process ID " + Process.GetCurrentProcess().Id);
                    Console.WriteLine("Paused for debugger. Press ENTER to continue");
                    Console.ReadLine();

                    args = args.Take(i).Concat(args.Skip(i + 1)).ToArray();

                    return;
                }
            }
        }

        public bool TryRun(string[] args, out int returnCode)
        {
            try
            {
                returnCode = RunInternal(args);
                return true;
            }
            catch (Exception exception)
            {
                if (exception is GracefulException)
                {
                    if (exception.InnerException != null)
                    {
                        Logger.LogInformation(exception.InnerException.Message);
                    }

                    Logger.LogError(exception.Message);
                }
                else
                {
                    Logger.LogDebug(exception.ToString());
                    Logger.LogCritical(Resources.Error_Command_Failed, exception.Message);
                }
                returnCode = 1;
                return false;
            }
        }

        internal int RunInternal(params string[] args)
        {
            var options = CommandLineOptions.Parse(args, _consoleOutput);

            if (options == null)
            {
                return 1;
            }

            if (options.IsHelp)
            {
                return 2;
            }

            if (options.IsVerbose)
            {
                CommandOutputProvider.LogLevel = LogLevel.Debug;
            }

            var userSecretsId = ResolveId(options);
            var store = new SecretsStore(userSecretsId, Logger);
            options.Command.Execute(store, Logger);
            return 0;
        }

        internal string ResolveId(CommandLineOptions options)
        {
            if (!string.IsNullOrEmpty(options.Id))
            {
                return options.Id;
            }

            using (var resolver = new ProjectIdResolver(Logger, _workingDirectory))
            {
                return resolver.Resolve(options.Project, options.Configuration);
            }
        }
    }
}
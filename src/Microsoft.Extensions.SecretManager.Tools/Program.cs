// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.SecretManager.Tools.Internal;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.Extensions.SecretManager.Tools
{
    public class Program
    {
        private ILogger _logger;
        private CommandOutputProvider _loggerProvider;
        private readonly TextWriter _consoleOutput;
        private readonly string _workingDirectory;
        // TODO this is only for testing. Can remove this when this project builds with CLI preview3
        private readonly MsBuildContext _msBuildContext;

        public static int Main(string[] args)
        {
            HandleDebugFlag(ref args);

            int rc;
            new Program(Console.Out, Directory.GetCurrentDirectory(), MsBuildContext.FromCurrentDotNetSdk()).TryRun(args, out rc);
            return rc;
        }

        internal Program(TextWriter consoleOutput, string workingDirectory, MsBuildContext msbuildContext)
        {
            _consoleOutput = consoleOutput;
            _workingDirectory = workingDirectory;
            _msBuildContext = msbuildContext;

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

            var userSecretsId = !string.IsNullOrEmpty(options.Id)
                    ? options.Id
                    : ResolveIdFromProject(options.Project);

            var store = new SecretsStore(userSecretsId, Logger);
            options.Command.Execute(store, Logger);
            return 0;
        }

        private string ResolveIdFromProject(string projectPath)
        {
            var finder = new GracefulProjectFinder(_workingDirectory);
            var projectFile = finder.FindMsBuildProject(projectPath);

            Logger.LogDebug(Resources.Message_Project_File_Path, projectFile);

            var project = new MsBuildProjectContextBuilder()
                .UseMsBuild(_msBuildContext)
                .AsDesignTimeBuild()
                .WithBuildTargets(Array.Empty<string>())
                .WithProjectFile(projectFile)
                .Build();

            return project.GetUserSecretsId();
        }
    }
}
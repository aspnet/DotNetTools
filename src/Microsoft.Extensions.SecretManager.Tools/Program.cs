// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.SecretManager.Tools
{
    public class Program
    {
        private ILogger _logger;
        private CommandOutputProvider _loggerProvider;

        public Program()
        {
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

        public static int Main(string[] args)
        {
            return new Program().Run(args);
        }

        public int Run(string[] args)
        {
            try
            {
                var app = new CommandLineApplication();
                app.Name = "dotnet-user-secrets";
                app.Description = "Manages user secrets";
                app.ShortVersionGetter = () => GetInformationalVersion();

                app.HelpOption("-?|-h|--help");
                var optVerbose = app.Option("-v|--verbose", "Verbose output", CommandOptionType.NoValue);

                app.Command("set", c =>
                {
                    c.Description = "Sets the user secret to the specified value";

                    var optionProject = c.Option("-p|--project <PATH>", "Path to project, default is current directory", CommandOptionType.SingleValue);
                    var keyArg = c.Argument("[name]", "Name of the secret");
                    var valueArg = c.Argument("[value]", "Value of the secret");
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(() =>
                    {
                        var projectPath = optionProject.Value() ?? Directory.GetCurrentDirectory();

                        if (optVerbose.HasValue())
                        {
                            CommandOutputProvider.LogLevel = LogLevel.Debug;
                        }

                        ProcessSecretFile(projectPath, secrets =>
                        {
                            secrets[keyArg.Value] = valueArg.Value;
                        });

                        Logger.LogInformation(Resources.Message_Saved_Secret, keyArg.Value, valueArg.Value);
                        return 0;
                    });
                });

                app.Command("remove", c =>
                {
                    c.Description = "Removes the specified user secret";

                    var optionProject = c.Option("-p|--project <PATH>", "Path to project, default is current directory", CommandOptionType.SingleValue);
                    var keyArg = c.Argument("[name]", "Name of the secret");
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(() =>
                    {
                        var projectPath = optionProject.Value() ?? Directory.GetCurrentDirectory();

                        if (optVerbose.HasValue())
                        {
                            CommandOutputProvider.LogLevel = LogLevel.Debug;
                        }

                        ProcessSecretFile(projectPath, secrets =>
                        {
                            if (secrets[keyArg.Value] == null)
                            {
                                Logger.LogWarning(Resources.Error_Missing_Secret, keyArg.Value);
                            }
                            else
                            {
                                secrets.Remove(keyArg.Value);
                            }
                        });

                        return 0;
                    });
                });

                app.Command("list", c =>
                {
                    c.Description = "Lists all the application secrets";

                    var optionProject = c.Option("-p|--project <PATH>", "Path to project, default is current directory", CommandOptionType.SingleValue);
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(() =>
                    {
                        var projectPath = optionProject.Value() ?? Directory.GetCurrentDirectory();

                        if (optVerbose.HasValue())
                        {
                            CommandOutputProvider.LogLevel = LogLevel.Debug;
                        }

                        ProcessSecretFile(projectPath, secrets =>
                        {
                            PrintAll(secrets);
                        },
                        persist: false);
                        return 0;
                    });
                });

                app.Command("clear", c =>
                {
                    c.Description = "Deletes all the application secrets";

                    var optionProject = c.Option("-p|--project <PATH>", "Path to project, default is current directory", CommandOptionType.SingleValue);
                    c.HelpOption("-?|-h|--help");

                    c.OnExecute(() =>
                    {
                        var projectPath = optionProject.Value() ?? Directory.GetCurrentDirectory();

                        if (optVerbose.HasValue())
                        {
                            CommandOutputProvider.LogLevel = LogLevel.Debug;
                        }

                        ClearSecretFile(projectPath);

                        return 0;
                    });
                });

                // Show help information if no subcommand/option was specified.
                app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return 2;
                });

                return app.Execute(args);
            }
            catch (Exception exception)
            {
                Logger.LogCritical(Resources.Error_Command_Failed, exception.Message);
                return 1;
            }
        }

        private static string GetInformationalVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            var versionAttribute = attribute == null ?
                assembly.GetName().Version.ToString() :
                attribute.InformationalVersion;

            return versionAttribute;
        }

        private void PrintAll(JObject secrets)
        {
            if (secrets.Count == 0)
            {
                Logger.LogInformation(Resources.Error_No_Secrets_Found);
            }
            else
            {
                foreach (var secret in secrets)
                {
                    Logger.LogInformation(Resources.FormatMessage_Secret_Value_Format(secret.Key, secret.Value));
                }
            }
        }

        private void ProcessSecretFile(string projectPath, Action<JObject> observer, bool persist = true)
        {
            Logger.LogDebug(Resources.Message_Project_File_Path, projectPath);
            var secretsFilePath = PathHelper.GetSecretsPath(projectPath);
            Logger.LogDebug(Resources.Message_Secret_File_Path, secretsFilePath);
            var secretObj = File.Exists(secretsFilePath) ?
                            JObject.Parse(File.ReadAllText(secretsFilePath)) :
                            new JObject();

            observer(secretObj);

            if (persist)
            {
                WriteSecretsFile(secretsFilePath, secretObj);
            }
        }

        private void ClearSecretFile(string projectPath)
        {
            Logger.LogDebug(Resources.Message_Project_File_Path, projectPath);
            var secretsFilePath = PathHelper.GetSecretsPath(projectPath);
            Logger.LogDebug(Resources.Message_Secret_File_Path, secretsFilePath);

            WriteSecretsFile(secretsFilePath, new JObject());
        }

        private static void WriteSecretsFile(string secretsFilePath, JObject contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(secretsFilePath));
            File.WriteAllText(secretsFilePath, contents.ToString());
        }
    }
}
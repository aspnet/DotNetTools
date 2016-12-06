// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    public class SetCommand
    {
        public static void Configure(CommandLineApplication command, CommandLineOptions options, IConsole console)
        {
            command.Description = "Sets the user secret to the specified value";
            command.ExtendedHelpText = @"
Additional Info:
  This command will also handle piped input. Piped input is expected to be a valid JSON format.

Examples:
  dotnet user-secrets set ConnStr ""User ID=bob;Password=***""
  cat secrets.json | dotnet user-secrets set
";

            command.HelpOption();

            var nameArg = command.Argument("[name]", "Name of the secret");
            var valueArg = command.Argument("[value]", "Value of the secret");

            command.OnExecute(() =>
            {
                if (console.IsInputRedirected && nameArg.Value == null)
                {
                    options.Command = new FromStdInStrategy();
                    return 0;
                }

                if (string.IsNullOrEmpty(nameArg.Value))
                {
                    console.Error.WriteLine(Resources.FormatError_MissingArgument("name").Red());
                    return 1;
                }

                if (valueArg.Value == null)
                {
                    console.Error.WriteLine(Resources.FormatError_MissingArgument("value").Red());
                    return 1;
                }

                options.Command = new ForOneValueStrategy(nameArg.Value, valueArg.Value);
                return 0;
            });
        }

        public class FromStdInStrategy : ICommand
        {
            public void Execute(CommandContext context)
            {
                // parses stdin with the same parser that Microsoft.Extensions.Configuration.Json would use
                var provider = new ReadableJsonConfigurationProvider();
                using (var stream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(stream, Encoding.Unicode, 1024, true))
                    {
                        writer.Write(context.Console.In.ReadToEnd()); // TODO buffer?
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    provider.Load(stream);
                }

                foreach (var k in provider.CurrentData)
                {
                    context.SecretStore.Set(k.Key, k.Value);
                }

                context.Logger.LogInformation(Resources.Message_Saved_Secrets, provider.CurrentData.Count);

                context.SecretStore.Save();
            }
        }

        public class ForOneValueStrategy : ICommand
        {
            private readonly string _keyName;
            private readonly string _keyValue;

            public ForOneValueStrategy(string keyName, string keyValue)
            {
                _keyName = keyName;
                _keyValue = keyValue;
            }

            public void Execute(CommandContext context)
            {
                context.SecretStore.Set(_keyName, _keyValue);
                context.SecretStore.Save();
                context.Logger.LogInformation(Resources.Message_Saved_Secret, _keyName, _keyValue);
            }
        }
    }
}
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    internal class InitCommand : ICommand
    {
        public string _overrideId { get; }

        public static void Configure(CommandLineApplication command, CommandLineOptions options)
        {
            command.Description = "Set a user secrets ID to enable secret storage";
            command.HelpOption();

            command.OnExecute(() =>
            {
                options.Command = new InitCommand(options.Id);
            });
        }

        public InitCommand(string id)
        {
            _overrideId = id;
        }

        public void Execute(CommandContext context)
        {
            throw new NotImplementedException();
        }
    }
}

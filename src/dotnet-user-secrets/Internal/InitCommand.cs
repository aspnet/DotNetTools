using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    internal class InitCommand : ICommand
    {
        public string OverrideId { get; }
        public string ProjectPath { get; }
        public string WorkingDirectory { get; private set; } = Directory.GetCurrentDirectory();

        public static void Configure(CommandLineApplication command, CommandLineOptions options)
        {
            command.Description = "Set a user secrets ID to enable secret storage";
            command.HelpOption();

            command.OnExecute(() =>
            {
                options.Command = new InitCommand(options.Id, options.Project);
            });
        }

        public InitCommand(string id, string project)
        {
            OverrideId = id;
            ProjectPath = project;
        }

        private static string ResolveProjectPath(string name, string path)
        {
            var finder = new MsBuildProjectFinder(path);
            return finder.FindMsBuildProject(name);
        }

        public void Execute(CommandContext context)
        {
            var projectPath = ResolveProjectPath(ProjectPath, WorkingDirectory);
        }

        public void Execute(CommandContext context, string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            Execute(context);
        }
    }
}

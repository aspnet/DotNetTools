using Microsoft.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    public class InitCommand : ICommand
    {
        public string OverrideId { get; }
        public string ProjectPath { get; }
        public string WorkingDirectory { get; private set; } = Directory.GetCurrentDirectory();

        internal static void Configure(CommandLineApplication command, CommandLineOptions options)
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

        private static bool HasConditionalParent(XElement el)
        {
            return el.Ancestors().Any(ancestor =>
                ancestor.Attributes().Any(attribute =>
                    attribute.Name == "Condition"));
        }

        public void Execute(CommandContext context, string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            Execute(context);
        }

        public void Execute(CommandContext context)
        {
            var projectPath = ResolveProjectPath(ProjectPath, WorkingDirectory);

            // Load the project file as XML
            var projectDocument = XDocument.Load(projectPath);

            // Confirm a UserSecretsId isn't already set
            if (projectDocument.XPathSelectElements("//UserSecretsId").Any())
            {
                // TODO: i18n
                context.Reporter.Error($"MSBuild project '{projectPath}' already contains a UserSecretsId.");
                // TODO: correct error reporting approach
                throw new NotImplementedException();
            }

            // Accept the `--id` CLI option to the main app
            string newSecretsId = string.IsNullOrWhiteSpace(OverrideId)
                ? Guid.NewGuid().ToString()
                : OverrideId;

            // Find the first non-conditional PropertyGroup
            var propertyGroup = projectDocument.Root.DescendantNodes()
                .FirstOrDefault(node => node is XElement el
                    && el.Name == "PropertyGroup"
                    && el.Attributes().All(attr =>
                        attr.Name != "Condition")) as XElement;

            // No valid property group, create a new one
            if (propertyGroup == null)
            {
                propertyGroup = XElement.Parse(@"<PropertyGroup></PropertyGroup>");
                projectDocument.Root.AddFirst(propertyGroup);
            }

            // Add UserSecretsId element
            var userSecretsElement = XElement.Parse(@"<UserSecretsId></UserSecretsId>");
            userSecretsElement.SetValue(newSecretsId);

            propertyGroup.Add(userSecretsElement);

            projectDocument.Save(projectPath);

            // TODO: i18n
            context.Reporter.Output($"Successfully added UserSecretsId '{newSecretsId}' to MSBuild project '{projectPath}'");
        }
    }
}

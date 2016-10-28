// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNetWatcher.Tools.Tests
{
    public class TemporaryCSharpProject
    {
        private const string Template =
 @"<Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props"" />
  <PropertyGroup>
    {0}
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    {1}
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)/Microsoft.CSharp.targets"" />
</Project>";

        private const string DefaultGlobs =
@"<Compile Include=""**/*.cs"" Exclude=""obj/**/*;bin/**/*"" />
<EmbeddedResource Include=""**/*.resx"" Exclude=""obj/**/*;bin/**/*"" />";

        private readonly string _filename;
        private readonly TemporaryDirectory _directory;
        private string[] _tfms;
        private List<string> _items = new List<string>();

        public TemporaryCSharpProject(string name, TemporaryDirectory directory)
        {
            _filename = name + ".csproj";
            _directory = directory;
        }

        public string Path => System.IO.Path.Combine(_directory.Root, _filename);

        public TemporaryCSharpProject WithTargetFrameworks(params string[] tfms)
        {
            _tfms = tfms;
            return this;
        }

        public TemporaryCSharpProject WithItem(string itemName, string include, string condition = null)
        {
            var item = $"<{itemName} Include=\"{include}\"";
            if (condition != null)
            {
                item += $" Condition=\"{condition}\"";
            }
            item += " />";
            _items.Add(item);
            return this;
        }

        public TemporaryCSharpProject WithProjectReference(TemporaryCSharpProject reference)
        {
            if (ReferenceEquals(this, reference))
            {
                throw new InvalidOperationException("Can add project reference to selv");
            }

            return WithItem("ProjectReference", reference.Path);
        }

        public TemporaryCSharpProject WithDefaultGlobs()
        {
            _items.Add(DefaultGlobs);
            return this;
        }

        public TemporaryDirectory Dir() => _directory;

        public void Create()
        {
            var tfm = _tfms == null || _tfms.Length == 0
                ? string.Empty
                : _tfms.Length == 1
                    ? $"<TargetFramework>{_tfms[0]}</TargetFramework>"
                    : $"<TargetFrameworks>{string.Join(";", _tfms)}</TargetFrameworks>";

            _directory.CreateFile(_filename, string.Format(Template, tfm, string.Join("\r\n", _items)));
        }
    }
}
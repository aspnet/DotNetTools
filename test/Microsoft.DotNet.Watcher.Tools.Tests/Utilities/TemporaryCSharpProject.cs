﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.DotNet.Watcher.Tools.Tests
{
    public class TemporaryCSharpProject
    {
        private const string Template =
 @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    {0}
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    {1}
  </ItemGroup>
</Project>";

        private readonly string _filename;
        private readonly TemporaryDirectory _directory;
        private List<string> _items = new List<string>();
        private List<string> _properties = new List<string>();

        public TemporaryCSharpProject(string name, TemporaryDirectory directory)
        {
            Name = name;
            _filename = name + ".csproj";
            _directory = directory;
        }

        public string Name { get; }
        public string Path => System.IO.Path.Combine(_directory.Root, _filename);

        public TemporaryCSharpProject WithTargetFrameworks(params string[] tfms)
        {
            Debug.Assert(tfms.Length > 0);
            var propertySpec = new PropertySpec
            {
                Value = string.Join(";", tfms)
            };
            propertySpec.Name = tfms.Length == 1
                ? "TargetFramework"
                : "TargetFrameworks";

            return WithProperty(propertySpec);
        }

        public TemporaryCSharpProject WithProperty(string name, string value)
            => WithProperty(new PropertySpec { Name = name, Value = value });

        public TemporaryCSharpProject WithProperty(PropertySpec property)
        {
            var sb = new StringBuilder();
            sb.Append('<').Append(property.Name).Append('>')
                .Append(property.Value)
                .Append("</").Append(property.Name).Append('>');
            _properties.Add(sb.ToString());
            return this;
        }

        public TemporaryCSharpProject WithItem(string itemName, string include, string condition = null)
            => WithItem(new ItemSpec { Name = itemName, Include = include, Condition = condition });

        public TemporaryCSharpProject WithItem(ItemSpec item)
        {
            var sb = new StringBuilder("<");
            sb.Append(item.Name).Append(" ");
            if (item.Include != null) sb.Append(" Include=\"").Append(item.Include).Append('"');
            if (item.Remove != null) sb.Append(" Remove=\"").Append(item.Remove).Append('"');
            if (item.Update != null) sb.Append(" Update=\"").Append(item.Update).Append('"');
            if (item.Exclude != null) sb.Append(" Exclude=\"").Append(item.Exclude).Append('"');
            if (item.Condition != null) sb.Append(" Exclude=\"").Append(item.Condition).Append('"');
            if (!item.Watch) sb.Append(" Watch=\"false\" ");
            sb.Append(" />");
            _items.Add(sb.ToString());
            return this;
        }

        public TemporaryCSharpProject WithProjectReference(TemporaryCSharpProject reference, bool watch = true)
        {
            if (ReferenceEquals(this, reference))
            {
                throw new InvalidOperationException("Can add project reference to self");
            }

            return WithItem(new ItemSpec { Name = "ProjectReference", Include = reference.Path, Watch = watch });
        }

        public TemporaryDirectory Dir() => _directory;

        public void Create()
        {
            _directory.CreateFile(_filename, string.Format(Template, string.Join("\r\n", _properties), string.Join("\r\n", _items)));
        }

        public class ItemSpec
        {
            public string Name { get; set; }
            public string Include { get; set; }
            public string Exclude { get; set; }
            public string Update { get; set; }
            public string Remove { get; set; }
            public bool Watch { get; set; } = true;
            public string Condition { get; set; }
        }

        public class PropertySpec
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }
    }
}

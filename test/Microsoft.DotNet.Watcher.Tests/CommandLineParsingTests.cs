// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
using Xunit.Abstractions;
using Xunit.Runner.DotNet;

namespace Microsoft.DotNet.Watcher.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class CommandLineParsingTests
    {
        [Fact]
        public void NoWatcherArgs()
        {
            var args = "--arg1 v1 --arg2 v2".Split(' ');

            string[] watcherArgs, dotnetArgs;
            Program.SeparateWatchArguments(args, out watcherArgs, out dotnetArgs);

            Assert.Empty(watcherArgs);
            Assert.Equal(args, dotnetArgs);
        }

        [Fact]
        public void ArgsForBothDotnetAndWatcher()
        {
            var args = "--arg1 v1 --arg2 v2 --dotnet-args --arg3 --arg4 v4".Split(' ');

            string[] watcherArgs, dotnetArgs;
            Program.SeparateWatchArguments(args, out watcherArgs, out dotnetArgs);

            Assert.Equal(new string[] {"--arg1", "v1", "--arg2", "v2" }, watcherArgs);
            Assert.Equal(new string[] { "--arg3", "--arg4", "v4" }, dotnetArgs);
        }

        [Fact]
        public void MultipleSeparators()
        {
            var args = "--arg1 v1 --arg2 v2 --dotnet-args --arg3 --dotnetArgs --arg4 v4".Split(' ');

            string[] watcherArgs, dotnetArgs;
            Program.SeparateWatchArguments(args, out watcherArgs, out dotnetArgs);

            Assert.Equal(new string[] { "--arg1", "v1", "--arg2", "v2" }, watcherArgs);
            Assert.Equal(new string[] { "--arg3", "--dotnetArgs", "--arg4", "v4" }, dotnetArgs);
        }
    }
}

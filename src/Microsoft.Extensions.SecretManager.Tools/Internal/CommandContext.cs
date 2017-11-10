// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.Extensions.SecretManager.Tools.Internal
{
    internal class CommandContext
    {
        public CommandContext(
            SecretStore store,
            IReporter reporter,
            IConsole console)
        {
            SecretStore = store;
            Reporter = reporter;
            Console = console;
        }

        public IConsole Console { get; }
        public IReporter Reporter { get; }
        public SecretStore SecretStore { get; }
    }
}

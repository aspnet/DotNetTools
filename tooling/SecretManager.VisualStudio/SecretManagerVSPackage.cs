// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.SecretManager
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [Guid(SecretManagerVSPackage.PackageGuidString)]
    public sealed class SecretManagerVSPackage : Package
    {
        public const string PackageGuidString = "d6b82ee5-8f4b-41f2-a581-fefa7f57a239";
    }
}

//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.SqlTools.Samples.LanguageServerHost.Services
{
    public class LanguageServerSettings
    {
        public int MaxNumberOfProblems { get; set; }

        public LanguageServerSettings()
        {
            MaxNumberOfProblems = 100;
        }
    }
}

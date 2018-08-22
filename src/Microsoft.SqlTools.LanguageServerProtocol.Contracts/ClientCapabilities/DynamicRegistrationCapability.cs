﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.SqlTools.LanguageServerProtocol.Contracts.ClientCapabilities
{
    public class DynamicRegistrationCapability
    {
        /// <summary>
        /// Whether the capabilitiy supports dynamic registration
        /// </summary>
        public bool? DynamicRegistration { get; set; }
    }
}
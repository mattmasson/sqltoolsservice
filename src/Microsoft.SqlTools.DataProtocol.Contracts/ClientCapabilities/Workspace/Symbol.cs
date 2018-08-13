﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using Microsoft.SqlTools.DataProtocol.Contracts.Common;

namespace Microsoft.SqlTools.DataProtocol.Contracts.ClientCapabilities.Workspace
{
    /// <summary>
    /// Capabilities specific to the workspace/symbol request
    /// </summary>
    public class SymbolCapabilities : DynamicRegistrationCapability
    {
        /// <summary>
        /// Specific capabilities for the SymbolKind in a workspace/symbol request. Can be
        /// <c>null</c>
        /// </summary>
        public SymbolKindCapabilities SymbolKind { get; set; }
    }

    /// <summary>
    /// Specific capabilities for the SymbolKind in a workspace/symbol request
    /// </summary>
    public class SymbolKindCapabilities
    {
        /// <summary>
        /// The symbol kind values the client supports. When this property exists, the client also
        /// guarantees that it will handle values outside its set gracefully and falls back to a
        /// default value when unknown.
        /// 
        /// If this property is not present the client only supports the symbol kinds from File to
        /// Array as defined in the initial version of the protocol.
        /// </summary>
        public List<SymbolKind> ValueSet { get; set; }
    }
}
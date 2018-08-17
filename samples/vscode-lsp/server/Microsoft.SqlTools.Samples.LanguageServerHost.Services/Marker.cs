//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Samples.LanguageServerHost.Services.Contracts;

namespace Microsoft.SqlTools.Samples.LanguageServerHost.Services
{
    public class Marker
    {
        #region Properties

        /// <summary>
        /// Gets or sets the marker's message string.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the marker's message level.
        /// </summary>
        public DiagnosticSeverity Level { get; set; }

        /// <summary>
        /// Gets or sets the Region where the marker should appear.
        /// </summary>
        public Region Region { get; set; }

        #endregion
    }
}

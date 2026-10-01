////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Raised when a stylesheet compiled with errors. Carries the diagnostics that were
    /// reported during the compile: the reporter's own channel is Console.Error by default,
    /// so without this the caller learned only that the compile failed.
    /// </summary>
    internal sealed class XsltCompilationFailure : XPathException
    {
        private readonly IList<IXmlProcessingError> errors;

        public XsltCompilationFailure(string message, IList<IXmlProcessingError> errors, int totalErrorCount)
            : base(message)
        {
            this.errors = errors ?? new List<IXmlProcessingError>();
            TotalErrorCount = totalErrorCount;
        }

        /// <summary>The retained diagnostics, oldest first. Capped - see TotalErrorCount.</summary>
        public IList<IXmlProcessingError> Errors => errors;

        /// <summary>How many errors the compile reported; may exceed Errors.Count.</summary>
        public int TotalErrorCount { get; }
    }
}

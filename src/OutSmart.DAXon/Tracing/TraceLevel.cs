////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Tracing
{
    internal sealed class TraceLevel
    {
        /// <summary>
        /// No tracing *
        /// </summary>
        public const int NONE = 0;
        /// <summary>
        /// Function and template calls *
        /// </summary>
        public const int LOW = 1;
        /// <summary>
        /// Instructions (or the equivalent in XQuery)
        /// </summary>
        public const int NORMAL = 2;
        /// <summary>
        /// All expressions
        /// </summary>
        public const int HIGH = 3;
    }
}
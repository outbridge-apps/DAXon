////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;

// Stub PromoterToX classes (TypeChecker.cs references unqualified).
// Java source has these as nested in Converter; we expose them in OutSmart.DAXon.Values
// since TypeChecker has `using OutSmart.DAXon.Values`.
namespace OutSmart.DAXon.Values
{
    internal sealed class PromoterToBase64Binary : Converter
    {
        public PromoterToBase64Binary() { }
    }
}

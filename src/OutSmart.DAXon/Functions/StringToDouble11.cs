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

namespace OutSmart.DAXon.Functions
{
    internal sealed class StringToDouble11 : StringToDouble
    {
        private static readonly StringToDouble11 _instance = new StringToDouble11();
        public StringToDouble11() { }
        public static StringToDouble11 GetInstance() => _instance;
        // upstream: the ONE thing XSD 1.1 adds over 1.0 — "+INF" is a legal lexical form
        private protected override bool PlusInfAllowed => true;

        protected override double SignedPositiveInfinity()
        {
            return double.PositiveInfinity;
        }
    }
}

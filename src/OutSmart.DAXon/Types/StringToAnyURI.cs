////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Types
{
    internal sealed class StringToAnyURI : StringConverter
    {
        private readonly StringConverter inner;
        public StringToAnyURI(object x) : base(x as ConversionRules) { inner = new StringConverter.StringToAnyURI(x as ConversionRules); }
        // Delegates to the proven nested converter (BuiltInAtomicType binds that one; this top-level
        // copy is what ConversionRules binds - it used to throw NIE if that registry path went live).
        public override IConversionResult ConvertString(UnicodeString input) => inner.ConvertString(input);
    }
}

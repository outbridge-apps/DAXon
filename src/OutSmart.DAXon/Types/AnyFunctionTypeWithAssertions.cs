////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.XQuery;

namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// The item type <c>function(*)</c> carrying one or more annotation assertions (<c>%ns:name function(*)</c>).
    /// The behaviour of annotation assertions is implementation-defined and "can only further restrict the set
    /// of functions matched" — Saxon-HE applies no restriction beyond "is a function item", so this type matches
    /// exactly the functions AnyFunctionType matches; it simply carries the assertions for export/inspection.
    /// Ported from upstream (was a hollow excluded stub that did not implement ItemType, so the (ItemType)
    /// cast in XPathParser.ParseFunctionItemType threw InvalidCastException).
    /// </summary>
    internal sealed class AnyFunctionTypeWithAssertions : AnyFunctionType
    {
        private readonly AnnotationList assertions;
        private readonly Configuration config;

        public AnyFunctionTypeWithAssertions(AnnotationList assertions, Configuration config)
        {
            this.assertions = assertions;
            this.config = config;
        }
    }
}

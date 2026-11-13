////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Patterns
{
    // XPath 4.0 name-test unions: the parser refuses them before this is built, as 4.0 is out of reach in this edition.
    internal sealed class NameTestUnion : NodeTest
    {
        public override double DefaultPriority => 0;
        public NameTestUnion() { }
        public static NameTestUnion WithTests() => new NameTestUnion();
        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation) => false;
    }
}

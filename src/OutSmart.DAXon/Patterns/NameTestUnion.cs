////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Patterns
{
    // Real NameTestUnion.cs (excluded) has STATIC WithTests(IList<NodeTest>); the stub had an
    // INSTANCE WithTests(object[]), so XPathParser's NameTestUnion.WithTests(tests) (tests is
    // IList<NodeTest>) hit CS0120 (object reference required) and the CastInjector wrapped the arg in
    // (object[]). Match the real static signature so the call binds directly with no injected cast.
    internal sealed class NameTestUnion : NodeTest
    {
        public override double DefaultPriority => 0;
        public NameTestUnion() { }
        public static NameTestUnion WithTests() => new NameTestUnion();
        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation) => false;
    }
}

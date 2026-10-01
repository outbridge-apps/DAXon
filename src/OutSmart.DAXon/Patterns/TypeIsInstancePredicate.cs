////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Patterns
{
    // TypeIsInstancePredicate — wraps a System.Type as INodePredicate for Children() callers.
    internal sealed class TypeIsInstancePredicate : INodePredicate
    {
        private readonly System.Type _t;
        public TypeIsInstancePredicate(System.Type t) { _t = t; }
        public bool Test(NodeInfo node) => _t?.IsInstanceOfType(node) ?? false;
        public static implicit operator Predicate<NodeInfo>(TypeIsInstancePredicate p) => p.Test;
        // batch5: real NodeSelector.Of takes the compat Java predicate delegate
        public static implicit operator Func<NodeInfo, bool>(TypeIsInstancePredicate p) => p.Test;
    }
}

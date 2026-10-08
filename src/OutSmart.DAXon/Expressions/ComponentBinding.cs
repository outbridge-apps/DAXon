////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Expressions
{
    public class ComponentBinding
    {
        private readonly SymbolicName symbolicName;
        private readonly Component target;
        public ComponentBinding(SymbolicName name, Component target)
        {
            this.symbolicName = name;
            this.target = target;
        }

        public virtual SymbolicName GetSymbolicName()
        {
            return symbolicName;
        }

        public virtual Component GetTarget()
        {
            return target;
        }
    }
}
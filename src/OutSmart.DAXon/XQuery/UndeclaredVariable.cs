////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Expressions.Instructions;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.XQuery
{
    internal sealed class UndeclaredVariable : GlobalVariable
    {
        public UndeclaredVariable()
        {
        }

        public void TransferReferences(GlobalVariable var)
        {
            foreach (IBindingReference @ref in references)
            {
                var.RegisterReference(@ref);
            }

            references = new List<IBindingReference>();
        }

        public override void Compile(Executable exec, int slot)
        {
            throw new NotSupportedException("Attempt to compile a place-holder for an undeclared variable");
        }
    }
}

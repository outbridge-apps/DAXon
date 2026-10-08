////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class implements the function function-name(), which is a standard function in XPath 3.0
    /// </summary>
    internal sealed class FunctionName : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IFunctionItem f = (IFunctionItem)arguments[0].Head();
            StructuredQName name = f.GetFunctionName();
            if (name == null)
            {
                return EmptySequence.GetInstance();
            }
            else if (name.HasURI(NamespaceUri.ANONYMOUS))
            {

                // Used for inline functions
                return EmptySequence.GetInstance();
            }
            else
            {
                return new QNameValue(name, BuiltInAtomicType.QNAME);
            }
        }
    }
}

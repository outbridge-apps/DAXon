////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the resolve-QName function in XPath 2.0
    /// </summary>
    internal sealed class ResolveQName : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            AtomicValue lex = (AtomicValue)arguments[0].Head();
            if (lex == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                return ResolveQNameFn(lex.GetStringValue(), (NodeInfo)arguments[1].Head());
            }
        }

        public static QNameValue ResolveQNameFn(string lexicalQName, NodeInfo element)
        {
            INamespaceResolver resolver = element.AllNamespaces;
            StructuredQName qName = StructuredQName.FromLexicalQName(lexicalQName, true, false, resolver);
            return new QNameValue(qName, BuiltInAtomicType.QNAME);
        }
    }
}

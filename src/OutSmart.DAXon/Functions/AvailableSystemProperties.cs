////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.functions.extfn.EXPathArchive.Archive;
//import com.saxonica.functions.extfn.EXPathBinaryFunctionSet;
//import com.saxonica.functions.extfn.EXPathFileFunctionSet;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    internal sealed class AvailableSystemProperties : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IList<AtomicValue> myList = new List<AtomicValue>();
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "version"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "vendor"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "vendor-url"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "product-name"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "product-version"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "is-schema-aware"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "supports-serialization"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "supports-backwards-compatibility"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "supports-namespace-axis"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "supports-streaming"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "supports-dynamic-evaluation"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "supports-higher-order-functions"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "xpath-version"));
            myList.Add(new QNameValue("xsl", NamespaceUri.XSLT, "xsd-version"));
            if (context.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
            {
                foreach (string s in (new string[0]))
                {
                    myList.Add(new QNameValue("", NamespaceUri.NULL, s));
                }
            }

            return new AtomicArray(myList);
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            public AnonymousSystemFunctionCall(AvailableSystemProperties parent, Expression[] arguments) : base(parent, arguments)
            {
            }
            // Suppress early evaluation
            public override Expression PreEvaluate(ExpressionVisitor visitor)
            {
                return this;
            }
        }
    }
}
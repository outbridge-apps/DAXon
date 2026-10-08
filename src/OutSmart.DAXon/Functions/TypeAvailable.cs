////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the XSLT fn:type-available() function.
    /// </summary>
    internal sealed class TypeAvailable : SystemFunction
    {
        private bool TypeAvailableFn(string lexicalName, Configuration config)
        {
            StructuredQName qName;
            try
            {
                if (lexicalName.IndexOf(':') < 0 && !lexicalName.StartsWith("Q{", StringComparison.Ordinal))
                {
                    NamespaceUri defaultNS = GetRetainedStaticContext().GetURIForPrefix("", true);
                    qName = new StructuredQName("", defaultNS, lexicalName);
                }
                else
                {
                    qName = StructuredQName.FromLexicalQName(lexicalName, false, true, GetRetainedStaticContext());
                }
            }
            catch (XPathException e)
            {
                throw e.WithErrorCode("XTDE1428");
            }

            NamespaceUri uri = qName.GetNamespaceUri();
            if (uri.Equals(NamespaceUri.JAVA_TYPE))
            {
                try
                {
                    string className = JavaExternalObjectType.LocalNameToClassName(qName.GetLocalPart());
                    config.GetType(className, false);
                    return true;
                }
                catch (XPathException err)
                {
                    return false;
                }
            }
            else
            {
                ISchemaType type = config.GetSchemaType(qName);
                if (type == null)
                {
                    return false;
                }

                return config.XsdVersion != 10 || !(type is BuiltInAtomicType) || ((BuiltInAtomicType)type).IsAllowedInXSD10();
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            string lexicalQName = arguments[0].Head().GetStringValue();
            return BooleanValue.Get(TypeAvailableFn(lexicalQName, context.GetConfiguration()));
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            try
            {
                if (arguments[0] is StringLiteral)
                {
                    bool b = TypeAvailableFn(((StringLiteral)arguments[0]).Stringify(), GetRetainedStaticContext().GetConfiguration());
                    return Literal.MakeLiteral(BooleanValue.Get(b));
                }
            }
            catch (XPathException e)
            {
            }

            return base.MakeFunctionCall(arguments);
        }
    }
}

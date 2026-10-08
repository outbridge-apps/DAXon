////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the fn:QName() function
    /// </summary>
    internal sealed class QNameFn : SystemFunction
    {
        public static QNameValue ExpandedQName(StringValue @namespace, StringValue lexical)
        {
            string uri;
            if (@namespace == null)
            {
                uri = null;
            }
            else
            {
                uri = @namespace.GetStringValue();
            }

            try
            {
                string[] parts = NameChecker.GetQNameParts(lexical.GetStringValue());

                // The QNameValue constructor does not check the prefix
                if (!(parts[0].Length == 0) && !NameChecker.IsValidNCName(parts[0]))
                {
                    throw new XPathException("Malformed prefix in QName: '" + parts[0] + '\'', "FOCA0002");
                }

                return new QNameValue(parts[0], NamespaceUri.Of(uri), parts[1], BuiltInAtomicType.QNAME, true);
            }
            catch (QNameException e)
            {
                throw new XPathException(e.GetMessage(), "FOCA0002");
            }
            catch (XPathException err)
            {
                throw err.ReplacingErrorCode("FORG0001", "FOCA0002");
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return ExpandedQName((StringValue)arguments[0].Head(), (StringValue)arguments[1].Head());
        }

        public override Elaborator GetElaborator()
        {
            return new QNameFnElaborator();
        }

        internal sealed class QNameFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall sfc = (SystemFunctionCall)GetExpression();
                if (sfc.GetArity() == 2)
                {
                    IItemEvaluator arg0eval = sfc.GetArg(0).MakeElaborator().ElaborateForItem();
                    IItemEvaluator arg1eval = sfc.GetArg(1).MakeElaborator().ElaborateForItem();
                    return (context) => ExpandedQName((StringValue)arg0eval.Eval(context), (StringValue)arg1eval.Eval(context));
                }
                else
                {
                    IItemEvaluator arg0eval = sfc.GetArg(0).MakeElaborator().ElaborateForItem();
                    INamespaceResolver resolver = sfc.GetRetainedStaticContext();
                    return (context) =>
                    {
                        IItem @in = arg0eval.Eval(context);
                        if (@in == null)
                        {
                            return null;
                        }

                        StructuredQName qn = StructuredQName.FromLexicalQName(@in.GetStringValue(), false, true, resolver);
                        return new QNameValue(qn, BuiltInAtomicType.QNAME);
                    };
                }
            }
        }
    }
}

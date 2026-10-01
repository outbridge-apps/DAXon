////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath string-length() function
    /// </summary>
    internal sealed class StringLength_1 : ScalarSystemFunction
    {
        public override IntegerValue[] IntegerBounds => new IntegerValue[]
            {
                Int64Value.ZERO,
                Expression.MAX_STRING_LENGTH
            };

        public override ISequence ResultWhenEmpty()
        {
            return Int64Value.ZERO;
        }

        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            if (arg is StringValue)
            {
                return Int64Value.MakeIntegerValue(((StringValue)arg).Length());
            }
            else
            {
                UnicodeString s;
                try
                {
                    s = arg.UnicodeStringValue;
                }
                catch (NotSupportedException e)
                {
                    throw new XPathException("Cannot get the string value of a function item", "FOTY0013");
                }

                return Int64Value.MakeIntegerValue(s.Length());
            }
        }

        public override Elaborator GetElaborator()
        {
            return new StringLengthFnElaborator();
        }

        internal sealed class StringLengthFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                IUnicodeStringEvaluator argEval = arg.MakeElaborator().ElaborateForUnicodeString(true);

                // string-length(.) on a Tiny node: the length is computable from the node arrays
                // alone — materializing the string just to take its length is the dominant cost of
                // aggregates like sum(//x/string-length(.)). The type checker wraps the bare `.` in
                // fn:string(), so unwrap that first; string(node) is the node's string value, whose
                // length is the same structural walk. Attribute/namespace wrappers index different
                // arrays and fall through to the generic evaluator.
                Expression probe = Expressions.Elaboration.TransparentWrappers.Unwrap(arg,
                    Expressions.Elaboration.Peel.StringFn | Expressions.Elaboration.Peel.Converter
                    | Expressions.Elaboration.Peel.Atomizer | Expressions.Elaboration.Peel.CardinalityChecker);
                if (probe is ContextItemExpression)
                {
                    return (context) =>
                    {
                        IItem it = context.GetContextItem();
                        if ((it is Trees.Tiny.TinyParentNodeImpl || it is Trees.Tiny.TinyTextImpl
                                || it is Trees.Tiny.WhitespaceTextImpl || it is Trees.Tiny.TinyTextualElement)
                            && ((Trees.Tiny.TinyNodeImpl)it).tree.TypeArray == null)
                        {
                            Trees.Tiny.TinyNodeImpl tn = (Trees.Tiny.TinyNodeImpl)it;
                            return Int64Value.MakeIntegerValue(Trees.Tiny.TinyParentNodeImpl.GetStringValueLength(tn.tree, tn.nodeNr));
                        }

                        UnicodeString s = argEval.Eval(context);
                        return Int64Value.MakeIntegerValue(s.Length());
                    };
                }

                return (context) =>
                {
                    UnicodeString str = argEval.Eval(context);
                    return Int64Value.MakeIntegerValue(str.Length());
                };
            }
        }
    }
}
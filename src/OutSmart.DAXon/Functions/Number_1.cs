////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    internal sealed class Number_1 : ScalarSystemFunction
    {
        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            return ToNumber((AtomicValue)arg);
        }

        public override ISequence ResultWhenEmpty()
        {
            return DoubleValue.NaN;
        }

        public override Expression TypeCheckCaller(FunctionCall caller, ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            if (caller.GetArg(0).IsCallOn(typeof(Number_1)))
            {

                // happens through repeated rewriting
                caller.SetArg(0, ((FunctionCall)caller.GetArg(0)).GetArg(0));
            }

            return caller;
        }

        public static DoubleValue ToNumber(AtomicValue arg0)
        {
            if (arg0 is BooleanValue)
            {
                return (DoubleValue)Converter.BooleanToDouble.INSTANCE.Convert(arg0);
            }
            else if (arg0 is NumericValue)
            {
                return (DoubleValue)Converter.NumericToDouble.INSTANCE.Convert(arg0).AsAtomic();
            }
            else if (arg0 is StringValue && !(arg0 is AnyURIValue))
            {

                // Always use the XSD 1.1 rules, which permit "+INF"
                IConversionResult cr = StringToDouble11.GetInstance().Convert(arg0);
                if (cr is ValidationFailure)
                {
                    return DoubleValue.NaN;
                }
                else
                {
                    return (DoubleValue)cr;
                }
            }
            else
            {
                return DoubleValue.NaN;
            }
        }

        public static DoubleValue Convert(AtomicValue value, Configuration config)
        {
            try
            {
                if (value == null)
                {
                    return DoubleValue.NaN;
                }

                if (value is BooleanValue)
                {
                    return new DoubleValue(((BooleanValue)value).GetBooleanValue() ? 1 : 0);
                }

                if (value is DoubleValue)
                {
                    return (DoubleValue)value;
                }

                if (value is NumericValue)
                {
                    return new DoubleValue(((NumericValue)value).GetDoubleValue());
                }

                if (value is StringValue && !(value is AnyURIValue))
                {
                    return config.GetConversionRules().StringToDoubleConverter.TryStringToNumber(value.UnicodeStringValue, out double d)
                        ? new DoubleValue(d)
                        : DoubleValue.NaN;
                }

                return DoubleValue.NaN;
            }
            catch (FormatException e)
            {
                return DoubleValue.NaN;
            }
        }

        public override Elaborator GetElaborator()
        {
            return new NumberFnElaborator();
        }

        internal sealed class NumberFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IItemEvaluator argEval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();

                // number($var) holding one untyped Tiny node: parse the node's string value with
                // the same StringToDouble11 primitive the generic path uses — minus the interim
                // UntypedAtomicValue. Anything else falls through to the generic evaluator.
                if (fnc.GetArg(0) is SingletonAtomizer sa && sa.BaseExpression is VariableReference)
                {
                    VariableReference vr = (VariableReference)sa.BaseExpression;
                    return (context) =>
                    {
                        if (vr.EvaluateVariable(context) is Trees.Tiny.TinyNodeImpl tn && tn.tree.TypeArray == null)
                        {
                            IConversionResult cr = StringToDouble11.GetInstance().ConvertString(tn.UnicodeStringValue);
                            return cr is ValidationFailure ? DoubleValue.NaN : (DoubleValue)cr;
                        }

                        return ToNumber((AtomicValue)argEval.Eval(context));
                    };
                }

                // number(.) on an untyped Tiny context node: same direct parse. The atomized
                // context item arrives as SingletonAtomizer or plain Atomizer depending on how
                // much the type checker could prove.
                if ((fnc.GetArg(0) is SingletonAtomizer sac && sac.BaseExpression is ContextItemExpression)
                    || (fnc.GetArg(0) is Atomizer atc && atc.BaseExpression is ContextItemExpression))
                {
                    return (context) =>
                    {
                        if (context.GetContextItem() is Trees.Tiny.TinyNodeImpl tn && tn.tree.TypeArray == null)
                        {
                            IConversionResult cr = StringToDouble11.GetInstance().ConvertString(tn.UnicodeStringValue);
                            return cr is ValidationFailure ? DoubleValue.NaN : (DoubleValue)cr;
                        }

                        return ToNumber((AtomicValue)argEval.Eval(context));
                    };
                }

                return (context) => ToNumber((AtomicValue)argEval.Eval(context));
            }
        }
    }
}

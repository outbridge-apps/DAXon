////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/functions/hof/CurriedFunction.java (replaces the hollow stub).
// A partially-applied function: also used to represent an inline function closure over captured variables.

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class CurriedFunction : AbstractFunction
    {
        private readonly IFunctionItem targetFunction;
        private readonly ISequence[] boundValues;
        private SpecificFunctionType functionType;

        public override IFunctionItemType FunctionItemType
        {
            get
            {
                if (functionType == null)
                {
                    IFunctionItemType baseItemType = targetFunction.FunctionItemType;
                    SequenceType resultType = SequenceType.ANY_SEQUENCE;
                    if (baseItemType is SpecificFunctionType)
                    {
                        resultType = baseItemType.ResultType;
                    }

                    int placeholders = 0;
                    foreach (ISequence boundArgument in boundValues)
                    {
                        if (boundArgument == null)
                        {
                            placeholders++;
                        }
                    }

                    SequenceType[] argTypes = new SequenceType[placeholders];
                    if (baseItemType is SpecificFunctionType)
                    {
                        for (int i = 0, j = 0; i < boundValues.Length; i++)
                        {
                            if (boundValues[i] == null)
                            {
                                argTypes[j++] = baseItemType.ArgumentTypes[i];
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < argTypes.Length; i++)
                        {
                            argTypes[i] = SequenceType.ANY_SEQUENCE;
                        }
                    }

                    functionType = new SpecificFunctionType(argTypes, resultType);
                }

                return functionType;
            }
        }

        public override string Description => "partially-applied function " + targetFunction.Description;

        /// <summary>
        /// Create a curried function.
        /// </summary>
        /// <param name="targetFunction">the function to be curried</param>
        /// <param name="boundValues">the values the arguments are bound to; null represents an unbound placeholder</param>
        public CurriedFunction(IFunctionItem targetFunction, ISequence[] boundValues)
        {
            this.targetFunction = targetFunction ?? throw new System.NullReferenceException();
            this.boundValues = boundValues;
        }

        public override StructuredQName GetFunctionName()
        {
            return null;
        }

        public override int GetArity()
        {
            int count = 0;
            foreach (ISequence v in boundValues)
            {
                if (v == null)
                {
                    count++;
                }
            }

            return count;
        }

        public override AnnotationList GetAnnotations()
        {
            return targetFunction.GetAnnotations();
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            ISequence[] newArgs = new ISequence[boundValues.Length];
            for (int i = 0, j = 0; i < newArgs.Length; i++)
            {
                if (boundValues[i] == null)
                {
                    newArgs[i] = args[j++];
                }
                else
                {
                    newArgs[i] = boundValues[i];
                }
            }

            IXPathContext c2 = targetFunction.MakeNewContext(context, null);
            if (targetFunction is UserFunction)
            {
                ((XPathContextMajor)c2).SetCurrentComponent(((UserFunction)targetFunction).DeclaringComponent);
            }

            return targetFunction.Call(c2, newArgs);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("curriedFunc");
            targetFunction.Export(@out);
            @out.StartElement("args");
            foreach (ISequence seq in boundValues)
            {
                if (seq == null)
                {
                    @out.StartElement("x");
                    @out.EndElement();
                }
                else
                {
                    Literal.ExportValue(seq, @out);
                }
            }

            @out.EndElement();
            @out.EndElement();
        }
    }
}

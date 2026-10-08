////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Operators;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of the fn:concat() function as defined in XPath 2.0, 3.0, and 3.1
    /// </summary>
    internal sealed class Concat31 : SystemFunction, IPushableFunction
    {

        public override IFunctionItemType FunctionItemType
        {
            get
            {
                SequenceType[] argTypes = new SequenceType[GetArity()];
                ArrayTools.Fill(argTypes, SequenceType.OPTIONAL_ATOMIC);
                return new SpecificFunctionType(argTypes, SequenceType.SINGLE_STRING);
            }
        }
        protected override ISequence ResultIfEmpty(int arg)
        {
            return null;
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            if (OperandArray.Every(arguments, (arg) => arg.GetCardinality() == StaticProperty.EXACTLY_ONE && arg.GetItemType() == BuiltInAtomicType.BOOLEAN))
            {

                // Warning if all the arguments are booleans: probably a misuse of the '||' operator
                visitor.StaticContext.IssueWarning("Did you intend to apply string concatenation to boolean operands? " + "Perhaps you intended 'or' rather than '||'. " + "To suppress this warning, use string() on the arguments.", DAXonErrorCode.SXWN9035, arguments[0].GetLocation());
            }

            return new AnonymousOptimized(this, arguments);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            UnicodeBuilder builder = new UnicodeBuilder(16);
            foreach (ISequence arg in arguments)
            {
                IItem head = arg.Head();
                if (head != null)
                {
                    builder.Accept(head.UnicodeStringValue);
                }
            }

            return new StringValue(builder.ToUnicodeString());
        }

        public override Elaborator GetElaborator()
        {
            return new ConcatElaborator();
        }

        // Eager-item elaborator: every argument's head is appended unconditionally, so each is
        // evaluated as a single item without the per-call lazy argument wrapping. Many-valued or
        // error-bearing arguments fall back to the generic function-call elaborator.
        private sealed class ConcatElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                int n = expr.GetArity();
                for (int a = 0; a < n; a++)
                {
                    if (Cardinality.AllowsMany(expr.GetArg(a).GetCardinality()) || ErrorExpression.IsContainedIn(expr.GetArg(a)))
                    {
                        SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                        generic.SetExpression(expr);
                        return generic.ElaborateForItem();
                    }
                }

                IItemEvaluator[] args = new IItemEvaluator[n];
                for (int a = 0; a < n; a++)
                {
                    args[a] = expr.GetArg(a).MakeElaborator().ElaborateForItem();
                }

                return (context) =>
                {
                    try
                    {
                        UnicodeBuilder builder = new UnicodeBuilder(16);
                        for (int a = 0; a < args.Length; a++)
                        {
                            IItem head = args[a].Eval(context);
                            if (head != null)
                            {
                                builder.Accept(head.UnicodeStringValue);
                            }
                        }

                        return new StringValue(builder.ToUnicodeString());
                    }
                    catch (XPathException e)
                    {
                        throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                    }
                };
            }
        }

        public void Process(Outputter destination, IXPathContext context, ISequence[] arguments)
        {
            IUniStringConsumer output = destination.GetStringReceiver(false, Loc.NONE);
            output.Open();
            foreach (ISequence arg in arguments)
            {
                IItem head = arg.Head();
                if (head != null)
                {
                    output.Accept(head.UnicodeStringValue);
                }
            }

            output.Close();
        }

        /// <summary>
        /// Get the required type of the nth argument
        /// </summary>
        public override SequenceType GetRequiredType(int arg)
        {
            return Details.paramTypes[0]; // concat() is a special case
        }

        private sealed class AnonymousOptimized : SystemFunctionCall.Optimized
        {

            private readonly Concat31 parent;
            public AnonymousOptimized(Concat31 parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            public override UnicodeString EvaluateAsString(IXPathContext context)
            {
                UnicodeBuilder buffer = new UnicodeBuilder(16);
                foreach (Operand o in Operands())
                {
                    IItem item = o.GetChildExpression().EvaluateItem(context);
                    if (item != null)
                    {
                        buffer.Accept(item.UnicodeStringValue);
                    }
                }

                return buffer.ToUnicodeString();
            }

            public override IItem EvaluateItem(IXPathContext context)
            {
                return new StringValue(EvaluateAsString(context));
            }
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
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
    /// Implementation of the fn:concat() function
    /// </summary>
    internal sealed class Concat : SystemFunction, IPushableFunction
    {

        public override IFunctionItemType FunctionItemType
        {
            get
            {
                SequenceType[] argTypes = new SequenceType[GetArity()];
                ArrayTools.Fill(argTypes, SequenceType.ATOMIC_SEQUENCE);
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
                if (arg is IItem)
                {

                    // common case, avoid creating a singleton iterator
                    builder.Accept(((IItem)arg).UnicodeStringValue);
                }
                else
                {
                    ISequenceIterator iter = arg.Iterate();
                    for (IItem item; (item = iter.Next()) != null;)
                    {
                        builder.Accept(item.UnicodeStringValue);
                    }
                }
            }

            return new StringValue(builder.ToUnicodeString());
        }

        public void Process(Outputter destination, IXPathContext context, ISequence[] arguments)
        {
            IUniStringConsumer output = destination.GetStringReceiver(false, Loc.NONE);
            output.Open();
            foreach (ISequence arg in arguments)
            {
                ISequenceIterator iter = arg.Iterate();
                for (IItem item; (item = iter.Next()) != null;)
                {
                    output.Accept(item.UnicodeStringValue);
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

            private readonly Concat parent;
            public AnonymousOptimized(Concat parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            public override UnicodeString EvaluateAsString(IXPathContext context)
            {
                UnicodeBuilder buffer = new UnicodeBuilder(16);
                foreach (Operand o in Operands())
                {
                    ISequenceIterator iter = o.GetChildExpression().Iterate(context);
                    for (IItem item; (item = iter.Next()) != null;)
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

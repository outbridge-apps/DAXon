////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    internal class ContextItemAccessorFunction : ContextAccessorFunction
    {
        public override IFunctionItem BindContext(IXPathContext context)
        {
            IItem ci = context.GetContextItem();
            if (ci == null)
            {
                ICallable callable = new CallableDelegate((context1, arguments) =>
                {
                    throw new XPathException("Context item for " + GetFunctionName().DisplayName + " is absent", "XPDY0002");
                });
                IFunctionItemType fit = new SpecificFunctionType(new SequenceType[] { }, SequenceType.ANY_SEQUENCE);
                return new CallableFunction(new SymbolicName.F(GetFunctionName(), 0), callable, fit);
            }

            ConstantFunction fn = new ConstantFunction(Evaluate(ci, context));
            fn.Details = Details;
            fn.SetRetainedStaticContext(GetRetainedStaticContext());
            return fn;
        }

        public virtual IGroundedValue Evaluate(IItem item, IXPathContext context)
        {
            SystemFunction f = SystemFunction.MakeFunction(Details.name.GetLocalPart(), GetRetainedStaticContext(), 1);
            return f.Call(context, new ISequence[] { item }).Materialize();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {

            // Shouldn't be called; but we handle it if it is
            IItem contextItem = context.GetContextItem();
            if (contextItem == null)
            {
                throw new XPathException("Dynamic call to context-dependent function with no bound context", "XPDY0002");
            }

            return Evaluate(contextItem, context);
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            Expression arg = new ContextItemExpression();
            if (GetFunctionName().HasURI(NamespaceUri.SAXON))
            {
                BuiltInFunctionSet.Entry entry = Details;
                try
                {
                    return entry.functionSet.MakeFunction(entry.name.GetLocalPart(), 1).MakeFunctionCall(arg);
                }
                catch (XPathException e)
                {
                    throw new UncheckedXPathException(e); // Should not happen
                }
            }
            else
            {
                return SystemFunction.MakeCall(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), arg);
            }
        }

        internal sealed class StringAccessor : ContextItemAccessorFunction
        {
            public override Expression MakeFunctionCall(Expression[] arguments)
            {
                Expression ci = new ContextItemExpression();
                Expression sv = SystemFunction.MakeCall("string", GetRetainedStaticContext(), ci);
                return SystemFunction.MakeCall(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), sv);
            }

            public override IGroundedValue Evaluate(IItem item, IXPathContext context)
            {
                SystemFunction f = SystemFunction.MakeFunction(Details.name.GetLocalPart(), GetRetainedStaticContext(), 1);
                StringValue val = new StringValue(item.UnicodeStringValue);
                return f.Call(context, new ISequence[] { val }).Materialize();
            }
        }

        internal sealed class Number_0 : ContextItemAccessorFunction
        {
            public override Expression MakeFunctionCall(Expression[] arguments)
            {
                Expression ci = new ContextItemExpression();
                Expression sv = SystemFunction.MakeCall("data", GetRetainedStaticContext(), ci);
                return SystemFunction.MakeCall(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), sv);
            }

            public override IGroundedValue Evaluate(IItem item, IXPathContext context)
            {
                SystemFunction f = SystemFunction.MakeFunction(Details.name.GetLocalPart(), GetRetainedStaticContext(), 1);
                IAtomicSequence val = item.Atomize();
                switch (val.GetLength())
                {
                    case 0:
                        return DoubleValue.NaN;
                    case 1:
                        return f.Call(context, new ISequence[] { val.Head() }).Materialize();
                    default:
                        XPathException err = new XPathException("When number() is called with no arguments, the atomized value of the context node must " + "not be a sequence of several atomic values", "XPTY0004");
                        err.SetIsTypeError(true);
                        throw err;
                }
            }
        }
    }
}
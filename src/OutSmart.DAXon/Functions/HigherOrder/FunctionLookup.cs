////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Ported from upstream net/sf/saxon/functions/hof/FunctionLookup.java (replaces the Phase 4.8c DEFERRED note
// in XSLT30FunctionSet). Supports the fn:function-lookup() function.

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class supports the function-lookup() function in XPath 3.0. It takes as arguments a function name
    /// (QName) and arity, and returns a function item representing that function if found, or an empty
    /// sequence if not found.
    /// </summary>
    internal sealed class FunctionLookup : ContextAccessorFunction
    {
        private IXPathContext boundContext = null;

        public FunctionLookup()
        {
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            PackageData pack = GetRetainedStaticContext().GetPackageData();
            if (pack is StylesheetPackage sp)
            {
                sp.SetRetainUnusedFunctions();
            }

            return base.MakeFunctionCall(arguments);
        }

        public override bool Equals(object o)
        {
            return base.Equals(o) && o is FunctionLookup fl &&
                ExpressionTool.EqualOrNull(GetRetainedStaticContext(), fl.GetRetainedStaticContext());
        }

        public override int GetHashCode()
        {
            // Included explicitly because Equals() is overridden.
            return base.GetHashCode();
        }

        /// <summary>
        /// Bind a context item to appear as part of the function's closure. If this method has been called,
        /// the supplied context item is used in preference to the one at the point where the function is
        /// actually called.
        /// </summary>
        public override IFunctionItem BindContext(IXPathContext context)
        {
            FunctionLookup bound = (FunctionLookup)SystemFunction.MakeFunction("function-lookup", GetRetainedStaticContext(), 2);
            IFocusIterator focusIterator = context.GetCurrentIterator();
            if (focusIterator != null)
            {
                IXPathContext c2 = context.NewMinorContext();
                ManualIterator mi = new ManualIterator(context.GetContextItem(), focusIterator.Position());
                c2.SetCurrentIterator(mi);
                bound.boundContext = c2;
            }
            else
            {
                bound.boundContext = context;
            }

            return bound;
        }

        public IFunctionItem Lookup(StructuredQName name, int arity, IXPathContext context)
        {
            Controller controller = context.GetController();
            Executable exec = controller.GetExecutable();
            RetainedStaticContext rsc = GetRetainedStaticContext();
            PackageData pd = rsc.GetPackageData();
            IFunctionLibrary lib = pd is StylesheetPackage sp ? sp.GetFunctionLibrary() : exec.FunctionLibrary;
            SymbolicName.F sn = new SymbolicName.F(name, arity);

            IndependentContext ic = new IndependentContext(controller.GetConfiguration());
            ic.SetDefaultCollationName(rsc.DefaultCollationName);
            ic.SetBaseURI(rsc.StaticBaseUriString);
            ic.SetDecimalFormatManager(rsc.GetDecimalFormatManager());
            ic.SetNamespaceResolver(rsc);
            ic.SetPackageData(pd);
            try
            {
                IFunctionItem fi = lib.GetFunctionItem(sn, ic);
                if (fi is UserFunction uf)
                {
                    Visibility vis = uf.DeclaredVisibility;
                    if (vis == Visibility.ABSTRACT)
                    {
                        return null;
                    }
                }

                if (fi is CallableFunction cf)
                {
                    cf.Callable = new CallableWithBoundFocus(cf.Callable, context);
                }
                else if (fi is ContextItemAccessorFunction ciaf)
                {
                    return ciaf.BindContext(context);
                }
                else if (fi is SystemFunction sysf && sysf.DependsOnContextItem())
                {
                    return new SystemFunctionWithBoundContextItem(sysf, context);
                }

                return fi;
            }
            catch (XPathException e)
            {
                if (e.HasErrorCode("XPST0017"))
                {
                    return null;
                }

                throw;
            }
        }

        public override int GetSpecialProperties(Expression[] arguments)
        {
            // Prevent inlining of stylesheet functions or variables calling function-lookup(), because the
            // dynamic context might be different.
            return base.GetSpecialProperties(arguments) | StaticProperty.HAS_SIDE_EFFECTS;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IXPathContext c = boundContext == null ? context : boundContext;
            QNameValue qname = (QNameValue)arguments[0].Head();
            IntegerValue arity = (IntegerValue)arguments[1].Head();
            IFunctionItem fi = Lookup(qname.GetStructuredQName(), (int)arity.LongValue(), c);
            if (fi == null)
            {
                return EmptySequence.GetInstance();
            }

            if (fi is ContextAccessorFunction caf)
            {
                fi = caf.BindContext(c);
            }

            Component target = fi is UserFunction uf ? uf.DeclaringComponent : null;
            IExportAgent agent = new FunctionLookupExportAgent(this, qname, arity);
            return new UserFunctionReference.BoundUserFunction(fi, (int)arity.LongValue(), target, agent, c.GetController());
        }

        internal sealed class FunctionLookupExportAgent : IExportAgent
        {
            private readonly QNameValue qName;
            private readonly IntegerValue arity;
            private readonly FunctionLookup container;

            public FunctionLookupExportAgent(FunctionLookup container, QNameValue qName, IntegerValue arity)
            {
                this.arity = arity;
                this.qName = qName;
                this.container = container;
            }

            public void Export(ExpressionPresenter @out)
            {
                container.MakeFunctionCall(Literal.MakeLiteral(qName), Literal.MakeLiteral(arity)).Export(@out);
            }
        }
    }
}

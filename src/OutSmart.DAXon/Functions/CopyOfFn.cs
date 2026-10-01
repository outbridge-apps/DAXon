////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Wrappers;

namespace OutSmart.DAXon.Functions
{
    // Faithful port of net.sf.saxon.functions.CopyOfFn (Saxon 12.9). The class was missing from the port,
    // so fn:copy-of() was unregistered (XPST0017).
    // XSLT 3.0 function copy-of(): compiles into an xsl:copy-of instruction, except when called dynamically.
    internal sealed class CopyOfFn : SystemFunction
    {
        public override int GetCardinality(Expression[] arguments)
        {
            return arguments[0].GetCardinality();
        }

        /// <summary>
        /// Evaluate the expression (used only for dynamic calls)
        /// </summary>
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequence @in = arguments.Length == 0 ? (ISequence)context.GetContextItem() : arguments[0];
            ISequenceIterator input = @in.Iterate();
            ISequenceIterator output = ItemMappingIterator.IMap(input, (item) =>
            {
                if (!(item is NodeInfo))
                {
                    return item;
                }
                else
                {
                    VirtualCopy vc = VirtualCopy.MakeVirtualCopy((NodeInfo)item);
                    if (GetRetainedStaticContext().GetPackageData().IsXSLT())
                    {
                        vc.GetTreeInfo().SetCopyAccumulators(true);
                    }

                    return (IItem)vc;
                }
            });
            return new LazySequence(output);
        }

        /// <summary>
        /// Make an expression that either calls this function, or that is equivalent to a call on it
        /// </summary>
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            Expression arg;
            if (arguments.Length == 0)
            {
                arg = new ContextItemExpression();
            }
            else
            {
                arg = arguments[0];
            }

            CopyOf fn = new CopyOf(arg, true, Validation.PRESERVE, null, false);
            fn.SetCopyAccumulators(true);
            fn.SetSchemaAware(false);
            return fn;
        }
    }
}

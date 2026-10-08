////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the function fn:has-children(), which is a standard function in XPath 3.0
    /// </summary>
    internal sealed class Innermost : SystemFunction
    {
        bool presorted = false;
        public override int GetSpecialProperties(Expression[] arguments)
        {
            return StaticProperty.ORDERED_NODESET | StaticProperty.PEER_NODESET;
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            if ((arguments[0].GetSpecialProperties() & StaticProperty.PEER_NODESET) != 0)
            {
                return arguments[0];
            }

            if ((arguments[0].GetSpecialProperties() & StaticProperty.ORDERED_NODESET) != 0)
            {
                presorted = true;
            }

            return base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(InnermostFn(arguments[0].Iterate()));
        }

        public ISequenceIterator InnermostFn(ISequenceIterator @in)
        {
            if (!presorted)
            {
                @in = new DocumentOrderIterator(@in, GlobalOrderComparer.GetInstance());
            }

            return new InnermostIterator(@in);
        }

        public override void ExportAttributes(ExpressionPresenter @out)
        {
            base.ExportAttributes(@out);
            if (presorted)
            {
                @out.EmitAttribute("flags", "p");
            }
        }

        public override void ImportAttributes(Properties attributes)
        {
            base.ImportAttributes(attributes);
            string flags = attributes.GetProperty("flags");
            if (flags != null && flags.Contains("p"))
            {
                presorted = true;
            }
        }

        private sealed class InnermostIterator : ISequenceIterator
        {
            ISequenceIterator @in;
            NodeInfo pending = null;
            public InnermostIterator(ISequenceIterator @in)
            {
                this.@in = @in;
                pending = (NodeInfo)@in.Next();
            }

            public NodeInfo Next()
            {
                if (pending == null)
                {

                    // we're done
                    return null;
                }
                else
                {
                    while (true)
                    {
                        NodeInfo next = (NodeInfo)@in.Next();
                        if (next == null)
                        {
                            NodeInfo current = pending;
                            pending = null;
                            return current;
                        }

                        if (Navigator.IsAncestorOrSelf(pending, next))
                        {

                            // discard the pending node
                            pending = next;
                        }
                        else
                        {

                            // emit the pending node
                            NodeInfo current = pending;
                            pending = next;
                            return current;
                        }
                    }
                }
            }

            public void Dispose()
            {
                @in.Dispose();
            }
            IItem ISequenceIterator.Next() => Next();
        }
    }
}


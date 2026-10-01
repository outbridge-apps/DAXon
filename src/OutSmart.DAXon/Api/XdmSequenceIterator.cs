////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Api
{
    public class XdmSequenceIterator<T> : IEnumerator<T>
    {
        private readonly ILookaheadIterator @base;
        private readonly RunResources scope;   // what the lazy evaluation opens; closed when it ends
        private bool closed = false;
        private T current;

        public T Current => current;
        object System.Collections.IEnumerator.Current => current;

        public XdmSequenceIterator(ISequenceIterator @base) : this(@base, null)
        {
        }

        internal XdmSequenceIterator(ISequenceIterator @base, RunResources scope)
        {
            this.scope = scope;
            RunResources saved = scope?.Activate();
            try
            {
                this.@base = LookaheadIteratorImpl.MakeLookaheadIterator(@base);
            }
            catch (UncheckedXPathException uxe)
            {
                scope?.CloseAll();
                throw new DAXonApiUncheckedException(uxe.GetXPathException());
            }
            catch (XPathException xe)
            {
                scope?.CloseAll();
                throw new DAXonApiUncheckedException(xe);
            }
            catch (RecursionDepthError xe)
            {
                scope?.CloseAll();
                throw new DAXonApiUncheckedException(xe.ToXPathException());
            }
            finally
            {
                if (scope != null)
                {
                    RunResources.Restore(saved);
                }
            }
        }

        public static XdmSequenceIterator<XdmNode> OfNodes(IAxisIterator @base)
        {
            return new XdmSequenceIterator<XdmNode>(@base);
        }

        public static XdmSequenceIterator<XdmAtomicValue> OfAtomicValues(ISequenceIterator @base)
        {
            return new XdmSequenceIterator<XdmAtomicValue>(@base);
        }

        protected static XdmSequenceIterator<XdmNode> OfNode(XdmNode node)
        {
            return new XdmSequenceIterator<XdmNode>(SingletonIterator.MakeIterator(node.UnderlyingNode));
        }

        public virtual bool HasNext()
        {
            if (closed)
            {
                return false;
            }

            RunResources saved = scope?.Activate();
            try
            {
                bool more = @base.HasNext;
                if (!more)
                {
                    scope?.CloseAll();
                }

                return more;
            }
            catch (UncheckedXPathException e)
            {
                scope?.CloseAll();
                throw new DAXonApiUncheckedException(e.GetXPathException());
            }
            finally
            {
                if (scope != null)
                {
                    RunResources.Restore(saved);
                }
            }
        }

        public virtual T Next()
        {
            RunResources saved = scope?.Activate();
            try
            {
                IItem it = @base.Next();
                if (it == null)
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    return (T)(object)XdmItem.WrapItem(it);
                }
            }
            catch (UncheckedXPathException e)
            {
                scope?.CloseAll();
                throw new DAXonApiUncheckedException(e.GetXPathException());
            }
            finally
            {
                if (scope != null)
                {
                    RunResources.Restore(saved);
                }
            }
        }

        public virtual void Remove()
        {
            throw new NotSupportedException();
        }

        public virtual void Dispose()
        {
            closed = true;
            @base.Dispose();
            scope?.CloseAll();
        }

        // IEnumerator over the same items: MoveNext had always answered false, so a plain
        // while (it.MoveNext()) loop saw an empty sequence.
        bool System.Collections.IEnumerator.MoveNext()
        {
            if (!HasNext())
            {
                current = default;
                return false;
            }

            current = Next();
            return true;
        }

        void System.Collections.IEnumerator.Reset()
        {
        }
    }
}

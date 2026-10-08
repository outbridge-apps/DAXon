////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Api
{
    public class XdmSequenceIterator<T> : IEnumerator<T>
    {
        private readonly ILookaheadIterator @base;
        private readonly RunResources scope;   // what the lazy evaluation opens; closed when it ends
        private readonly Core.Controller.DeadlineToken limits;   // of the call that made the lazy result; null for none
        private bool closed;
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
                if (scope != null)
                {
                    // the call that makes the lazy result has armed its limits; its steps run under them
                    limits = Core.Controller.ActiveLimits;
                    limits?.PauseMemory();
                }
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
            Core.Controller.DeadlineToken previous = limits == null ? null : Core.Controller.BeginLazyStep(limits);
            try
            {
                bool more = @base.HasNext;
                if (!more)
                {
                    scope?.CloseAll();
                }

                return more;
            }
            catch (Exception e) when (IsEngineError(e))
            {
                throw Failed(e);
            }
            finally
            {
                EndStep(previous, saved);
            }
        }

        public virtual T Next()
        {
            RunResources saved = scope?.Activate();
            Core.Controller.DeadlineToken previous = limits == null ? null : Core.Controller.BeginLazyStep(limits);
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
            catch (Exception e) when (IsEngineError(e))
            {
                throw Failed(e);
            }
            finally
            {
                EndStep(previous, saved);
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
        // while (it.MoveNext()) loop saw an empty sequence. One step for the test and the item.
        bool System.Collections.IEnumerator.MoveNext()
        {
            if (closed)
            {
                current = default;
                return false;
            }

            RunResources saved = scope?.Activate();
            Core.Controller.DeadlineToken previous = limits == null ? null : Core.Controller.BeginLazyStep(limits);
            try
            {
                IItem it = @base.Next();
                if (it == null)
                {
                    scope?.CloseAll();
                    current = default;
                    return false;
                }

                current = (T)(object)XdmItem.WrapItem(it);
                return true;
            }
            catch (Exception e) when (IsEngineError(e))
            {
                throw Failed(e);
            }
            finally
            {
                EndStep(previous, saved);
            }
        }

        void System.Collections.IEnumerator.Reset()
        {
        }

        // A time or memory limit of the call raises a checked XPathException in a step: it reached the host raw.
        private static bool IsEngineError(Exception e)
        {
            return e is UncheckedXPathException || e is XPathException || e is RecursionDepthError;
        }

        private DAXonApiUncheckedException Failed(Exception e)
        {
            scope?.CloseAll();
            return new DAXonApiUncheckedException(e is UncheckedXPathException u ? u.GetXPathException()
                : e is RecursionDepthError r ? r.ToXPathException() : (XPathException)e);
        }

        private void EndStep(Core.Controller.DeadlineToken previous, RunResources saved)
        {
            if (limits != null)
            {
                Core.Controller.EndLazyStep(limits, previous);
            }

            if (scope != null)
            {
                RunResources.Restore(saved);
            }
        }
    }
}

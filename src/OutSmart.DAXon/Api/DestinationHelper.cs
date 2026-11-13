////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// A helper class for implementing the <see cref="IDestination"/> interface
    /// </summary>
    public class DestinationHelper
    {
        private readonly IDestination helpee;
        private readonly IList<IAction> listeners = new List<IAction>();
        private bool notified;   // the listeners of this run have been told
        private IList<IAction> ofRun;

        public virtual IList<IAction> Listeners => listeners;
        public DestinationHelper(IDestination helpee)
        {
            ArgumentCheck.NotNull(helpee, nameof(helpee));
            this.helpee = helpee;
        }

        public void OnClose(IAction listener)
        {
            listeners.Add(listener);
        }

        /// <summary>
        /// The listeners as the receiver of a run takes them: told when it closes, and once a run however many of the
        /// closes and CloseAndNotify calls reach them (an XQueryEvaluator as a destination told the final one's three
        /// times). A run starts when its receiver is made.
        /// </summary>
        internal IList<IAction> ListenersOfRun()
        {
            notified = false;
            return ofRun ??= new List<IAction> { Notify };
        }

        private void Notify()
        {
            if (notified)
            {
                return;
            }

            notified = true;
            foreach (IAction action in listeners)
            {
                action.Act();
            }
        }

        public virtual void CloseAndNotify()
        {
            helpee.Close();
            Notify();
            notified = false;   // the run ends here: the destination may be given another
        }

        // A run that delivers to a destination: closed and its listeners notified when the run succeeds, released without
        // notice when it fails. A null destination is a run that delivers its result otherwise.
        internal static T RunTo<T>(IDestination destination, Func<T> body)
        {
            bool done = false;
            try
            {
                T result = body();
                destination?.CloseAndNotify();
                done = true;
                return result;
            }
            finally
            {
                if (!done)
                {
                    ReleaseUnclosed(destination);
                }
            }
        }

        internal static void RunTo(IDestination destination, Action body)
        {
            RunTo<object>(destination, () =>
            {
                body();
                return null;
            });
        }

        /// <summary>
        /// Release a destination whose run did not reach CloseAndNotify. Every transform/query entry
        /// point calls CloseAndNotify as the LAST statement of its try block, so any error before it -
        /// including an SXTO0001 timeout - used to leave a Serializer's output file open, locked and
        /// half-written until finalization. Dispose closes it; the OnClose listeners deliberately do
        /// NOT fire, because they signal a completed result and this run has none. A secondary failure
        /// while closing must not displace the error being reported, hence the swallow.
        /// </summary>
        internal static void ReleaseUnclosed(IDestination destination)
        {
            if (destination == null)
            {
                return;
            }

            // Dispose, not Close, down the chain: closing a transformer used as a destination ran it on the partial tree
            // of the failed run, and its own destination was never released.
            for (IDestination d = destination; d != null; d = (d as IChainedDestination)?.Next)
            {
                try
                {
                    d.Dispose();
                }
                catch (Exception)
                {
                    // nothing useful to do while unwinding; the original error is the one that matters
                }
            }
        }
    }

    // A destination that feeds another: a transformer or query run on what it receives.
    internal interface IChainedDestination
    {
        IDestination Next { get; }
    }
}
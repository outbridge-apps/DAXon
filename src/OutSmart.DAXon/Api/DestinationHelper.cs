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
    /// A helper class for implementing the {@link IDestination} interface
    /// </summary>
    public class DestinationHelper
    {
        private readonly IDestination helpee;
        private readonly IList<IAction> listeners = new List<IAction>();

        public virtual IList<IAction> Listeners => listeners;
        public DestinationHelper(IDestination helpee)
        {
            this.helpee = helpee;
        }

        public void OnClose(IAction listener)
        {
            listeners.Add(listener);
        }

        public virtual void CloseAndNotify()
        {
            helpee.Close();
            foreach (IAction action in listeners)
            {
                action.Act();
            }
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

            try
            {
                destination.Close();
            }
            catch (Exception)
            {
                // nothing useful to do while unwinding; the original error is the one that matters
            }
        }
    }
}
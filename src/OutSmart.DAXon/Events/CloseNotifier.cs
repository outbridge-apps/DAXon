////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;

using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Events
{
    /// <summary>
    /// A receiver that performs specified actions when closed
    /// </summary>
    internal sealed class CloseNotifier : ProxyReceiver
    {
        private readonly IList<IAction> actionList;
        public CloseNotifier(IReceiver next, IList<IAction> actionList) : base(next)
        {
            this.actionList = actionList;
        }

        public override void Close()
        {
            base.Close();
            try
            {
                if (actionList != null)
                {
                    foreach (IAction action in actionList)
                    {
                        action.Act();
                    }
                }
            }
            catch (DAXonApiException e)
            {
                throw XPathException.MakeXPathException(e);
            }
        }
    }
}
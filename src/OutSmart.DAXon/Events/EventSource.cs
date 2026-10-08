////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;

namespace OutSmart.DAXon.Events
{
    internal abstract class EventSource : IActiveSource
    {
        private string systemId;
        public virtual void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public virtual string GetSystemId()
        {
            return systemId;
        }

        public abstract void Deliver(IReceiver @out, ParseOptions options);
    }
}
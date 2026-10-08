////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// An abstract class providing reusable code for implementing the {@link IDestination} interface
    /// </summary>
    public abstract class AbstractDestination : IDestination
    {
        protected DestinationHelper helper;
        private URI baseURI;

        public virtual URI DestinationBaseURI
        {
            get => baseURI; set
            {
                this.baseURI = value;
            }
        }
        public AbstractDestination()
        {
            helper = new DestinationHelper(this);
        }

        public void OnClose(IAction listener)
        {
            helper.OnClose(listener);
        }

        public virtual void CloseAndNotify()
        {
            helper.CloseAndNotify();
        }
        public abstract IReceiver GetReceiver(PipelineConfiguration arg0, SerializationProperties arg1);
        public abstract void Close();
        // Abort-path release; resource-holding destinations (Serializer) override.
        public virtual void Dispose()
        {
        }
    }
}

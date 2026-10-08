////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using System;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Api
{
    public class RawDestination : AbstractDestination
    {
        private SequenceCollector sequenceOutputter;
        private bool closed;
        public RawDestination()
        {
        }

        // MUST be an override: a same-signature method WITHOUT `override` hides the base virtual, so a
        // call through IDestination dispatches to AbstractDestination.GetReceiver -> NotImplementedException.
        public override IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
        {

            // The IReceiver returned by this method is a SequenceOutputter. The SequenceOutputter
            // builds a list of all top-level items passed to it. A top-level document or element
            // node can be passed as a sequence of events, in which case a ComplexContentOutputter
            // is created to build the tree represented by these events; the root document or element
            // node in this tree is then added to the same list as a composed items. On completion
            // the sequence represented by the list of items is available by calling the getXmlValue()
            // method.
            sequenceOutputter = new SequenceCollector(pipe);
            closed = false;
            helper.OnClose(() => closed = true);
            return new CloseNotifier(sequenceOutputter, helper.Listeners);
        }

        // method.
        public override void Close()
        {
            try
            {
                sequenceOutputter.Close();
                closed = true;
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }
        }

        public virtual XdmValue GetXdmValue()
        {
            if (!closed)
            {
                throw new InvalidOperationException("The result sequence has not yet been closed");
            }

            return XdmValue.Wrap(sequenceOutputter.Sequence);
        }
    }
}

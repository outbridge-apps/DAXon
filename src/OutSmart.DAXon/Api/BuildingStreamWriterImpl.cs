////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Api
{
    public class BuildingStreamWriterImpl : StreamWriterToReceiver
    {
        Builder builder;

        public XdmNode DocumentNode
        {
            get
            {
                try
                {
                    builder.Close();
                }
                catch (XPathException e)
                {
                    throw new DAXonApiException(e);
                }

                return new XdmNode(builder.CurrentRoot);
            }
        }
        public BuildingStreamWriterImpl(IReceiver receiver, Builder builder) : base(receiver)
        {
            this.builder = builder;
            builder.Open();
        }
    }
}

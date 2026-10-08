////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Events
{
    internal sealed class Valve : ProxyReceiver
    {
        private bool started;
        private readonly NamespaceUri testNamespace;
        private readonly IReceiver alternativeReceiver;
        public Valve(NamespaceUri testNamespace, IReceiver primary, IReceiver secondary) : base(primary)
        {
            this.testNamespace = testNamespace;
            this.alternativeReceiver = secondary;
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (!started)
            {
                started = true;
                if (elemName.GetNamespaceUri().Equals(testNamespace))
                {
                    alternativeReceiver.Open();
                    alternativeReceiver.StartDocument(ReceiverOption.NONE);
                    try
                    {
                        NextReceiver.Close();
                    }
                    catch (XPathException)
                    {
                    }

                    SetUnderlyingReceiver(alternativeReceiver);
                }
            }

            base.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        // ignore the failure
        public bool WasDiverted()
        {
            return NextReceiver == alternativeReceiver;
        }
    }
}
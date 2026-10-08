////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Serialization
{
    public class PrincipalOutputGatekeeper : ProxyReceiver
    {
        private readonly object syncLock = new object();
        private readonly XsltController controller;
        private bool usedAsPrimaryResult;
        private bool usedAsSecondaryResult;
        private bool opened;
        private bool closed;
        public PrincipalOutputGatekeeper(XsltController controller, IReceiver next) : base(next)
        {
            this.controller = controller;
        }

        public override void Open()
        {
            if (closed)
            {
                string uri = GetSystemId().Equals(XsltController.ANONYMOUS_PRINCIPAL_OUTPUT_URI) ? "(no URI supplied)" : GetSystemId();
                throw new XPathException("Cannot write more than one result document to the principal output destination: " + uri, "XTDE1490");
            }

            base.Open();
            opened = true;
        }

        public override void StartDocument(int properties)
        {
            lock (syncLock)
            {
                if (!opened)
                {
                    Open();
                }


                nextReceiver.StartDocument(properties);
            }
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            UseAsPrimary();
            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            lock (syncLock)
            {
                UseAsPrimary();
                nextReceiver.Characters(chars, locationId, properties);
            }
        }

        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            UseAsPrimary();
            nextReceiver.ProcessingInstruction(target, data, locationId, properties);
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            UseAsPrimary();
            nextReceiver.Comment(chars, locationId, properties);
        }

        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            UseAsPrimary();
            nextReceiver.Append(item, locationId, copyNamespaces);
        }

        private void UseAsPrimary()
        {
            lock (syncLock)
            {
                if (closed)
                {
                    throw new XPathException("Cannot write to the principal output destination as it has already been closed: " + IdentifySystemId()).WithErrorCode("XTDE1490");
                }

                if (usedAsSecondaryResult)
                {
                    throw new XPathException("Cannot write to the principal output destination as it has already been used by xsl:result-document: " + IdentifySystemId()).WithErrorCode("XTDE1490");
                }

                usedAsPrimaryResult = true;
            }
        }

        public virtual void UseAsSecondary()
        {
            lock (syncLock)
            {
                if (usedAsPrimaryResult)
                {
                    throw new XPathException("Cannot use xsl:result-document to write to a destination already used for the principal output: " + IdentifySystemId()).WithErrorCode("XTDE1490");
                }

                if (usedAsSecondaryResult)
                {
                    throw new XPathException("Cannot write more than one xsl:result-document to the principal output destination: " + IdentifySystemId()).WithErrorCode("XTDE1490");
                }

                usedAsSecondaryResult = true;
            }
        }

        public virtual IReceiver MakeReceiver(SerializationProperties @params)
        {
            try
            {
                IDestination dest = controller.PrincipalDestination;
                if (dest != null)
                {
                    return dest.GetReceiver(controller.MakePipelineConfiguration(), @params);
                }
            }
            catch (DAXonApiException)
            {
                return null;
            }

            return null;
        }

        private string IdentifySystemId()
        {
            string uri = controller.BaseOutputURI;
            return uri == null ? "(no URI supplied)" : uri;
        }

        public override void Close()
        {
            closed = true;
            if (usedAsPrimaryResult)
            {
                nextReceiver.Close();
            }
        }
    }
}
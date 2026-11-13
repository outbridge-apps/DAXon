////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Events
{
    // The pipeline behind a public XmlWriter (Serializer.GetXmlWriter, DocumentBuilder's writer): its errors - a character
    // the output encoding cannot hold, a limit of the run - reach the caller as the API's own, as from every other call.
    // The engine's XPathException went out of these public methods (SERE0008 for 'é' in us-ascii).
    internal sealed class ApiErrorReceiver : ProxyReceiver
    {
        public ApiErrorReceiver(IReceiver next) : base(next)
        {
        }

        public override void Open()
        {
            try
            {
                base.Open();
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void Close()
        {
            try
            {
                base.Close();
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void StartDocument(int properties)
        {
            try
            {
                base.StartDocument(properties);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void EndDocument()
        {
            try
            {
                base.EndDocument();
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            try
            {
                base.StartElement(elemName, type, attributes, namespaces, location, properties);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void EndElement()
        {
            try
            {
                base.EndElement();
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            try
            {
                base.Characters(chars, locationId, properties);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            try
            {
                base.ProcessingInstruction(target, data, locationId, properties);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            try
            {
                base.Comment(chars, locationId, properties);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }
    }
}

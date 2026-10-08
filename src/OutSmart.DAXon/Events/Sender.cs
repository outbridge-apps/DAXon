////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Events
{
    /// <summary>
    /// Sender is a helper class that sends events to a IReceiver from any kind of Source object
    /// </summary>
    internal abstract class Sender
    {

        // P5: deliver a resolved resource (the native Stream/TextReader/NodeInfo carrier that replaced the JAXP
        // Source hierarchy). Its parse-time filters are folded into the ParseOptions (so WrapForParse applies
        // them in the same order the old AugmentedSource path did), then it is delivered via its IActiveSource.
        public static void Send(ResolvedResource resource, IReceiver receiver, ParseOptions options)
        {
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            if (options == null)
            {
                options = pipe.GetParseOptions();
            }

            IList<IFilterFactory> filters = resource.Filters;
            if (filters != null)
            {
                foreach (IFilterFactory f in filters)
                {
                    options = options.WithFilter(f);
                }
            }

            Send(resource.ToActiveSource(), receiver, options);
        }

        // P5: deliver directly from an already-active source (a node source, ActiveStreamSource, EventSource, …),
        // with the same filter/validator/stripper wrapping as the Source path. Active sources are never an
        // AugmentedSource and never need ResolveSource, so this is the Source path minus that ceremony.
        public static void Send(IActiveSource source, IReceiver receiver, ParseOptions options)
        {
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            if (options == null)
            {
                options = pipe.GetParseOptions();
            }

            options = options.ApplyDefaults(pipe.GetConfiguration());
            string systemId = source.GetSystemId();
            receiver.SetSystemId(systemId);
            IReceiver next = WrapForParse(receiver, systemId, options);
            source.Deliver(next, options);
        }

        // Wrap a destination receiver with the parse-time filter chain, schema validator, and space stripper
        // defined by the parse options. Shared by the Source path and the direct System.Xml.XmlReader path so
        // both produce byte-identical trees (the space stripper in particular must be applied identically).
        private static IReceiver WrapForParse(IReceiver receiver, string systemId, ParseOptions options)
        {
            IReceiver next = receiver;
            IList<IFilterFactory> filters = options.Filters;
            if (filters != null)
            {
                for (int i = filters.Count - 1; i >= 0; i--)
                {
                    IFilterFactory ff = filters[i]; // Variable needed for C# type inference
                    IReceiver filter = ff.MakeFilter(next);
                    filter.SetSystemId(systemId);
                    next = filter;
                }
            }

            next = MakeValidator(next, systemId, options);
            ISpaceStrippingRule strippingRule = options.SpaceStrippingRule;
            if (strippingRule != null && !(strippingRule is NoElementsSpaceStrippingRule))
            {
                next = strippingRule.MakeStripper(next);
            }

            return next;
        }

        // Source-free parse entry: drive the Receiver pipeline directly from a System.Xml.XmlReader, applying
        // the same filter/validator/stripper wrapping as the Source path. Used by the .NET-native s9api input
        // so the common document-build path never constructs a JAXP Source.
        public static void Send(System.Xml.XmlReader reader, string systemId, IReceiver receiver, ParseOptions options)
        {
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            if (options == null)
            {
                options = pipe.GetParseOptions();
            }

            options = options.ApplyDefaults(pipe.GetConfiguration());
            receiver.SetSystemId(systemId);
            IReceiver next = WrapForParse(receiver, systemId, options);
            XmlReaderToReceiver.Send(reader, next);
        }

        public static void SendDocumentInfo(NodeInfo top, IReceiver receiver, ILocation location)
        {
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            NamePool targetNamePool = pipe.GetConfiguration().GetNamePool();
            if (top.GetConfiguration().GetNamePool() != targetNamePool)
            {

                // This code allows a document in one Configuration to be copied to another, changing
                // namecodes as necessary
                receiver = new NamePoolConverter(receiver, top.GetConfiguration().GetNamePool(), targetNamePool);
            }

            LocationCopier copier = new LocationCopier(top.GetNodeKind() == Types.Type.DOCUMENT, location.GetSystemId());
            pipe.SetComponent(typeof(ICopyInformee).FullName, copier);
            pipe.CopyInformee = (NodeInfo node) => (object)copier.NotifyElementNode(node);

            // start event stream
            receiver.Open();

            // copy the contents of the document
            switch (top.GetNodeKind())
            {
                case Types.Type.DOCUMENT:
                    top.Copy(receiver, CopyOptions.ALL_NAMESPACES | CopyOptions.TYPE_ANNOTATIONS, location);
                    break;
                case Types.Type.ELEMENT:
                    receiver.StartDocument(ReceiverOption.NONE);
                    top.Copy(receiver, CopyOptions.ALL_NAMESPACES | CopyOptions.TYPE_ANNOTATIONS, location);
                    receiver.EndDocument();
                    break;
                default:
                    throw new ArgumentException("Expected document or element node");
            }


            // end event stream
            receiver.Close();
        }

        public static IReceiver MakeValidator(IReceiver receiver, string systemId, ParseOptions options)
        {
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            int sv = options.GetSchemaValidationMode();
            if (sv != Validation.PRESERVE && sv != Validation.DEFAULT)
            {
                Controller controller = pipe.GetController();
                if (controller != null && !controller.GetExecutable().IsSchemaAware() && sv != Validation.STRIP)
                {
                    throw new XPathException("Cannot use schema-validated input documents when the query/stylesheet is not schema-aware");
                }
            }

            return receiver;
        }
    }
}
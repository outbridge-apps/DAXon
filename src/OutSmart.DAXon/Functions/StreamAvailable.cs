////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;

namespace OutSmart.DAXon.Functions
{
    // Faithful port of net/sf/saxon/functions/StreamAvailable.java (Saxon 12.9). Was a hollow stub
    // (no Call override -> AbstractFunction.Call NIE on every fn:stream-available()).
    // Probes whether a source document can be opened: parses until the first startElement, then
    // aborts via QuitParsingException — arrival there means the stream is available.
    internal sealed class StreamAvailable : SystemFunction
    {
        public StreamAvailable() { }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            bool result = IsAvailable(arguments[0].Head().GetStringValue(), context);
            return BooleanValue.Get(result);
        }

        private bool IsAvailable(string uri, IXPathContext context)
        {
            try
            {
                IReceiver tester = new StreamTester(context.GetConfiguration().MakePipelineConfiguration());
                RetainedStaticContext env = GetRetainedStaticContext();
                DocumentFn.SendDoc(uri, env.StaticBaseUriString, env.GetPackageData(), context, null, tester, new ParseOptions());
            }
            catch (QuitParsingException)
            {
                // Indicates that the first element was reported and the parse was then aborted
                return true;
            }
            catch (Exception)
            {
                // Any failure to open/parse (FODC0002 not-found as XPathException, or a raw
                // System.Xml.XmlException for non-well-formed input that .NET does not wrap) means the
                // document is not available for streaming: fn:stream-available returns false, never throws.
                return false;
            }
            return false;
        }

        private sealed class StreamTester : ProxyReceiver
        {
            public StreamTester(PipelineConfiguration pipe) : base(new Sink(pipe))
            {
            }

            public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes,
                NamespaceMap namespaces, ILocation location, int properties)
            {
                throw new QuitParsingException(false);
            }
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Resources;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Functions
{
    internal sealed class ParseXmlFragment : SystemFunction, ICallable
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue input = (StringValue)arguments[0].Head();
            if (input == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                return EvalParseXml(input, context);
            }
        }

        private NodeInfo EvalParseXml(StringValue inputArg, IXPathContext context)
        {
            NodeInfo node = null;
            string baseURI = StaticBaseUriString;
            string inputXml = inputArg.GetStringValue();
            if (!(inputXml.Length == 0) && inputXml[0] == 0xFEFF)
            {

                // Strip a leading BOM
                inputXml = inputXml.Substring(1);
            }

            try
            {
                Controller controller = context.GetController();
                if (controller == null)
                {
                    throw new XPathException("parse-xml-fragment() function is not available in this environment");
                }

                Configuration configuration = controller.GetConfiguration();
                string skeleton = "<!DOCTYPE z [<!ENTITY e SYSTEM \"http://www.saxonica.com/parse-xml-fragment/actual.xml\">]>\n<z>&e;</z>";

                // A fragment labelled XML 1.1 is an entity only a document so labelled may include: the wrapper is
                // one then, and both labels are read as a 1.1 document's is on every other path (see Xml11Label).
                bool labelled = XmlReaderToReceiver.IsLabelledXml11(inputXml);
                if (labelled)
                {
                    skeleton = "<?xml version=\"1.1\"?>" + skeleton;
                }

                StringReader skeletonReader = new StringReader(skeleton);
                Builder b = controller.MakeBuilder();
                b.SetDurability(Durability.TEMPORARY);
                if (b is TinyBuilder)
                {
                    ((TinyBuilder)b).SetStatistics(controller.GetConfiguration().GetTreeStatistics().FN_PARSE_STATISTICS);
                }

                IReceiver s = b;
                ParseOptions options = new ParseOptions().WithSchemaValidationMode(Validation.SKIP).WithDTDValidationMode(Validation.SKIP);
                PackageData pd = GetRetainedStaticContext().GetPackageData();
                if (pd is StylesheetPackage)
                {
                    options = options.WithSpaceStrippingRule(((StylesheetPackage)pd).SpaceStrippingRule);
                    if (((StylesheetPackage)pd).IsStripsTypeAnnotations())
                    {
                        s = configuration.GetAnnotationStripper(s);
                    }
                }
                else
                {
                    options = options.WithSpaceStrippingRule(IgnorableSpaceStrippingRule.GetInstance());
                }

                s.SetPipelineConfiguration(b.GetPipelineConfiguration());
                options = options.WithFilter((next) => new OuterElementStripper(next));

                // Native fragment parse: the DTD skeleton references the fragment as an external parsed entity;
                // a System.Xml.XmlResolver hands back the fragment content, so XmlReaderToReceiver expands it
                // inline as children of the wrapper element, which OuterElementStripper then removes.
                using (System.Xml.XmlReader xr = XmlReaderToReceiver.CreateXmlReader(skeletonReader, null, baseURI, new FragmentEntityResolver(inputXml), XmlReaderToReceiver.DtdUse.None, inputInEntity: inputXml.Length, mayBeXml11: labelled))
                {
                    Sender.Send(xr, baseURI, s, options);
                }

                node = b.CurrentRoot;
                b.Reset();
            }
            // A limit of the run keeps its code (SXTO0001, SXLM0003): the input was not malformed.
            catch (XPathException err) when (!err.IsRunLimit())
            {
                XPathException xe = new XPathException("First argument to parse-xml-fragment() is not a well-formed and namespace-well-formed XML fragment. XML parser reported: " + err.Message, "FODC0006");
                xe.MaybeSetContext(context);
                throw xe;
            }
            catch (System.Xml.XmlException xmlErr)
            {
                // The direct XmlReader path reports a malformed fragment as a raw XmlException (the SAX
                // error handler is not consulted here); map to FODC0006 like ParseXml does, instead of
                // letting it escape as a code-less internal error.
                XPathException xe = new XPathException("First argument to parse-xml-fragment() is not a well-formed and namespace-well-formed XML fragment. XML parser reported: " + xmlErr.Message, "FODC0006");
                xe.MaybeSetContext(context);
                throw xe;
            }

            return node;
        }

        // we don't want to overwrite the existing EntityResolver; try again
        // with a clean parser
        // this might be because the EntityResolver wasn't called - see bug 4127
        // This means our entity resolver wasn't called. Make one more try, using the
        // built-in platform default parser; then give up.
        /// <summary>
        /// Filter to remove the element wrapper added to the document to satisfy the XML parser
        /// </summary>
        // Returns the fn:parse-xml-fragment input as the content of the external parsed entity referenced by
        // the DTD skeleton, so System.Xml.XmlReader expands the fragment inline. Replaces the SAX EntityResolver.
        private sealed class FragmentEntityResolver : System.Xml.XmlResolver
        {
            private readonly string fragment;

            public override System.Net.ICredentials Credentials { set { } }
            public FragmentEntityResolver(string fragment) { this.fragment = fragment; }
            // The fragment is characters, and is served as characters: as bytes its text declaration decided how
            // they were read - one naming iso-8859-1 turned every non-ASCII character into two.
            public override bool SupportsType(Uri absoluteUri, System.Type type)
            {
                return type == typeof(TextReader) || base.SupportsType(absoluteUri, type);
            }

            public override object GetEntity(Uri absoluteUri, string role, System.Type ofObjectToReturn)
            {
                if (ofObjectToReturn == typeof(TextReader))
                {
                    return new StringReader(fragment);
                }

                return new MemoryStream(Encoding.UTF8.GetBytes(fragment));
            }
        }

        private sealed class OuterElementStripper : ProxyReceiver
        {

            private int level = 0;
            public OuterElementStripper(IReceiver next) : base(next)
            {
            }
            /// <summary>
            /// Notify the start of an element
            /// </summary>
            public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
            {
                if (level++ > 0)
                {
                    base.StartElement(elemName, type, attributes, namespaces, location, properties);
                }
            }

            /// <summary>
            /// End of element
            /// </summary>
            public override void EndElement()
            {
                if (--level > 0)
                {
                    base.EndElement();
                }
            }
        }
    }
}

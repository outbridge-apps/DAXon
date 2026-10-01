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
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Functions
{
    internal sealed class ParseXml : SystemFunction, ICallable
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue input = (StringValue)arguments[0].Head();
            if (input == null)
            {
                return EmptySequence.GetInstance();
            }
            else if (UsePushParser())
            {
                return ParseXmlPush(input, context);
            }
            else
            {
                return ParseXmlPull(input, context);
            }
        }

        private static bool UsePushParser()
        {
            return true;
        }

        private NodeInfo ParseXmlPush(StringValue inputArg, IXPathContext context)
        {
            string baseURI = GetRetainedStaticContext().StaticBaseUriString;
            try
            {
                Controller controller = context.GetController();
                if (controller == null)
                {
                    throw new XPathException("parse-xml() function is not available in this environment");
                }

                Configuration config = controller.GetConfiguration();
                string inputXml = inputArg.GetStringValue();
                if (!(inputXml.Length == 0) && inputXml[0] == 0xFEFF)
                {

                    // Strip a leading BOM
                    inputXml = inputXml.Substring(1);
                }

                StringReader sr = new StringReader(inputXml);
                Builder b = TreeModel.TINY_TREE.MakeBuilder(controller.MakePipelineConfiguration());
                IReceiver s = b;
                ParseOptions options = config.GetParseOptions();
                options = options.WithDTDValidationMode(Validation.SKIP);
                options = options.WithSchemaValidationMode(Validation.SKIP);
                PackageData pd = GetRetainedStaticContext().GetPackageData();
                if (pd is StylesheetPackage)
                {
                    options = options.WithSpaceStrippingRule(((StylesheetPackage)pd).SpaceStrippingRule);
                    if (((StylesheetPackage)pd).IsStripsTypeAnnotations())
                    {
                        s = config.GetAnnotationStripper(s);
                    }
                }
                else
                {
                    options = options.WithSpaceStrippingRule(IgnorableSpaceStrippingRule.GetInstance());
                }

                s.SetPipelineConfiguration(b.GetPipelineConfiguration());

                // P5: parse the literal XML string via the direct System.Xml.XmlReader path (no JAXP Source).
                using (System.Xml.XmlReader reader = global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(sr, null, baseURI, null, false, false, config))
                {
                    Sender.Send(reader, baseURI, s, options);
                }

                TinyDocumentImpl node = (TinyDocumentImpl)b.CurrentRoot;
                node.SetBaseURI(baseURI);
                b.Reset();
                return node;
            }
            catch (XPathException err)
            {
                string msg = MakeParsingErrorMessage(err);
                XPathException xe = new XPathException(msg, "FODC0006");
                xe.MaybeSetContext(context);
                throw xe;
            }
            catch (System.Xml.XmlException xmlErr)
            {
                // The direct XmlReader path reports malformed input as XmlException. Map to FODC0006.
                XPathException xe = new XPathException("Failure parsing XML: " + xmlErr.Message, "FODC0006");
                xe.MaybeSetContext(context);
                throw xe;
            }
        }

        private NodeInfo ParseXmlPull(StringValue inputArg, IXPathContext context)
        {
            string baseURI = GetRetainedStaticContext().StaticBaseUriString;
            try
            {
                Controller controller = context.GetController();
                if (controller == null)
                {
                    throw new XPathException("parse-xml() function is not available in this environment");
                }

                Configuration config = context.GetConfiguration();
                string inputXml = inputArg.GetStringValue();
                if (!(inputXml.Length == 0) && inputXml[0] == 0xFEFF)
                {

                    // Strip a leading BOM
                    inputXml = inputXml.Substring(1);
                }

                StringReader sr = new StringReader(inputXml);
                IActiveSource pullSource = new OutSmart.DAXon.Resources.ActiveStreamSource(null, sr, baseURI);
                Builder b = TreeModel.TINY_TREE.MakeBuilder(controller.MakePipelineConfiguration());
                IReceiver s = b;
                ParseOptions options = config.GetParseOptions();
                options = options.WithDTDValidationMode(Validation.SKIP);
                options = options.WithSchemaValidationMode(Validation.SKIP);
                PackageData pd = GetRetainedStaticContext().GetPackageData();
                if (pd is StylesheetPackage)
                {
                    options = options.WithSpaceStrippingRule(((StylesheetPackage)pd).SpaceStrippingRule);
                    if (((StylesheetPackage)pd).IsStripsTypeAnnotations())
                    {
                        s = config.GetAnnotationStripper(s);
                    }
                }
                else
                {
                    options = options.WithSpaceStrippingRule(IgnorableSpaceStrippingRule.GetInstance());
                }

                s.SetPipelineConfiguration(b.GetPipelineConfiguration());
                Sender.Send(pullSource, s, options);
                NodeInfo root = b.CurrentRoot;
                if (root is TinyDocumentImpl)
                {
                    TinyDocumentImpl node = (TinyDocumentImpl)root;
                    node.SetBaseURI(baseURI);
                    node.GetTreeInfo().SetUserData("saxon:document-uri", "");
                }
                else if (root is DocumentImpl)
                {
                    DocumentImpl node = (DocumentImpl)root;
                    node.SetBaseURI(baseURI);
                    node.GetTreeInfo().SetUserData("saxon:document-uri", "");
                }

                b.Reset();
                return root;
            }
            catch (XPathException err)
            {
                string msg = MakeParsingErrorMessage(err);
                XPathException xe = new XPathException(msg, "FODC0006");
                xe.MaybeSetContext(context);
                throw xe;
            }
        }

        // no action
        private string MakeParsingErrorMessage(XPathException err)
        {
            string msg = "First argument to parse-xml() is not a well-formed and namespace-well-formed XML document. ";
            msg += err.Message;
            var cause = err.InnerException;
            if (cause != null)
            {
                msg += cause is Exception __ct ? __ct.Message : cause.Message;
            }

            return msg;
        }
    }
}

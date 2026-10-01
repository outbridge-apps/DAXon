////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Serialization
{
    public class MetaTagAdjuster : ProxyReceiver
    {
        private bool seekingHead = true;
        private int droppingMetaTags = -1;
        private bool inMetaTag = false;
        string encoding;
        private string mediaType;
        private int level = 0;
        private bool isXHTML = false;
        private int htmlVersion = 4;
        public MetaTagAdjuster(IReceiver next) : base(next)
        {
        }

        public virtual void SetOutputProperties(Properties details)
        {
            encoding = details.GetProperty(DAXonOutputKeys.ENCODING);
            if (encoding == null)
            {
                encoding = "UTF-8";
            }

            mediaType = details.GetProperty(DAXonOutputKeys.MEDIA_TYPE);
            if (mediaType == null)
            {
                mediaType = "text/html";
            }

            string htmlVn = details.GetProperty(DAXonOutputKeys.HTML_VERSION);
            if (htmlVn == null && !isXHTML)
            {
                htmlVn = details.GetProperty(DAXonOutputKeys.VERSION);
            }

            if (htmlVn != null && htmlVn.StartsWith("5", StringComparison.Ordinal))
            {
                htmlVersion = 5;
            }
        }

        public virtual void SetIsXHTML(bool xhtml)
        {
            isXHTML = xhtml;
        }

        /// <summary>
        /// Compare a name: case-blindly in the case of HTML, case-sensitive for XHTML
        /// </summary>
        private bool ComparesEqual(string name1, string name2)
        {
            if (isXHTML)
            {
                return name1.Equals(name2);
            }
            else
            {
                return name1.Equals(name2, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Compare a name: case-blindly in the case of HTML, case-sensitive for XHTML
        /// </summary>
        private bool MatchesName(INodeName name, string local)
        {
            if (isXHTML)
            {
                if (!name.GetLocalPart().Equals(local))
                {
                    return false;
                }

                if (htmlVersion == 5)
                {
                    return name.HasURI(NamespaceUri.NULL) || name.HasURI(NamespaceUri.XHTML);
                }
                else
                {
                    return name.HasURI(NamespaceUri.XHTML);
                }
            }
            else
            {
                return name.GetLocalPart().Equals(local, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (droppingMetaTags == level)
            {
                if (MatchesName(elemName, "meta"))
                {

                    // if there was an http-equiv="ContentType" attribute, discard the meta element entirely
                    bool found = false;
                    foreach (AttributeInfo att in attributes)
                    {
                        string name = att.GetNodeName().GetLocalPart();
                        if (ComparesEqual(name, "http-equiv"))
                        {
                            string value = Whitespace.Trim(att.Value);
                            if (value.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            {

                                // case-blind comparison even for XHTML
                                found = true;
                                break;
                            }
                        }
                        else if (ComparesEqual(name, "charset"))
                        {

                            // See QT4 issue 318, Saxon bug 5852
                            found = true;
                            break;
                        }
                    }

                    inMetaTag = found;
                    if (found)
                    {
                        return;
                    }
                }
            }

            level++;
            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
            if (seekingHead && MatchesName(elemName, "head"))
            {
                string headPrefix = elemName.GetPrefix();
                NamespaceUri headURI = elemName.GetNamespaceUri();
                FingerprintedQName metaCode = new FingerprintedQName(headPrefix, headURI, "meta");
                IAttributeMap atts = EmptyAttributeMap.GetInstance();
                atts = atts.Put(new AttributeInfo(new NoNamespaceName("http-equiv"), BuiltInAtomicType.UNTYPED_ATOMIC, "Content-Type", Loc.NONE, ReceiverOption.NONE));
                atts = atts.Put(new AttributeInfo(new NoNamespaceName("content"), BuiltInAtomicType.UNTYPED_ATOMIC, mediaType + "; charset=" + encoding, Loc.NONE, ReceiverOption.NONE));
                nextReceiver.StartElement(metaCode, Untyped.INSTANCE, atts, namespaces, location, ReceiverOption.NONE);
                droppingMetaTags = level;
                seekingHead = false;
                nextReceiver.EndElement();
            }
        }

        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            if (inMetaTag)
            {
                inMetaTag = false;
            }
            else
            {
                level--;
                if (droppingMetaTags == level + 1)
                {
                    droppingMetaTags = -1;
                }

                nextReceiver.EndElement();
            }
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Serialization
{
    internal sealed class SerializationParamsHandler
    {
        public static readonly NamespaceUri NAMESPACE = NamespaceUri.OUTPUT;
        Properties properties;
        CharacterMap characterMap;
        ILocation locator;
        public SerializationParamsHandler()
        {
        }

        public SerializationParamsHandler(Properties props)
        {
            this.properties = props;
        }

        public void SetSerializationParams(NodeInfo node)
        {
            if (properties == null)
            {
                properties = new Properties();
            }

            if (node.GetNodeKind() == Types.Type.DOCUMENT)
            {
                node = Navigator.GetOutermostElement(node.GetTreeInfo());
            }

            if (node.GetNodeKind() != Types.Type.ELEMENT)
            {
                throw new XPathException("Serialization params: node must be a document or element node");
            }

            if (!node.GetLocalPart().Equals("serialization-parameters"))
            {
                throw new XPathException("Serialization params: element name must be 'serialization-parameters'");
            }

            if (!node.GetNamespaceUri().Equals(NAMESPACE))
            {
                throw new XPathException("Serialization params: element namespace must be " + NAMESPACE);
            }

            RestrictAttributes(node);
            HashSet<INodeName> nodeNames = new HashSet<INodeName>();
            IAxisIterator kids = node.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
            NodeInfo child;
            while ((child = kids.Next()) != null)
            {
                if (!nodeNames.Add(NameOfNode.MakeName(child)))
                {
                    throw new XPathException("Duplicated serialization parameter " + child.DisplayName, "SEPM0019");
                }

                string lname = child.GetLocalPart();
                NamespaceUri uri = child.GetNamespaceUri();
                if (uri.IsEmpty())
                {
                    throw new XPathException("Serialization parameter " + lname + " is in no namespace", "SEPM0017");
                }

                if (NamespaceUri.OUTPUT.Equals(uri))
                {
                    uri = NamespaceUri.NULL;
                }

                if (uri.IsEmpty() && lname.Equals("use-character-maps"))
                {
                    RestrictAttributes(child);
                    IAxisIterator gKids = child.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT);
                    NodeInfo gChild;
                    IntHashMap<string> map = new IntHashMap<string>();
                    while ((gChild = gKids.Next()) != null)
                    {
                        RestrictAttributes(gChild, "character", "map-string");
                        if (!(gChild.GetNamespaceUri().Equals(NAMESPACE) && gChild.GetLocalPart().Equals("character-map")))
                        {
                            if (gChild.GetNamespaceUri().Equals(NAMESPACE) || gChild.GetNamespaceUri().IsEmpty())
                            {
                                throw new XPathException("Invalid child of use-character-maps: " + gChild.DisplayName, "SEPM0017");
                            }
                        }

                        string ch = GetAttribute(gChild, "character");
                        string str = GetAttribute(gChild, "map-string");
                        StringValue chValue = new StringValue(ch);
                        if (chValue.Length() != 1)
                        {
                            throw new XPathException("In the serialization parameters, the value of @character in the character map " + "must be a single Unicode character", "SEPM0017");
                        }

                        int code = chValue.Content.CodePointAt(0);
                        string prev = map.Put(code, str);
                        if (prev != null)
                        {
                            throw new XPathException("In the serialization parameters, the character map contains two entries for the character \\u" + (65536 + code).ToString("x", CultureInfo.InvariantCulture).Substring(1), "SEPM0018");
                        }
                    }

                    characterMap = new CharacterMap(NameOfNode.MakeName(node).GetStructuredQName(), map);
                }
                else
                {
                    RestrictAttributes(child, "value");
                    string value = GetAttribute(child, "value");
                    try
                    {
                        ResultDocument.SetSerializationProperty(properties, uri, lname, value, child.AllNamespaces, false, node.GetConfiguration());
                    }
                    catch (XPathException err)
                    {
                        if (err.HasErrorCode("XQST0109", "SEPM0016"))
                        {
                            if (uri.IsEmpty())
                            {
                                throw err.WithErrorCode("SEPM0017").MaybeWithLocation(locator);
                            } // Unknown serialization parameter in a namespace - no action, ignore the error
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
        }

        private static void RestrictAttributes(NodeInfo element, params string[] allowedNames)
        {
            foreach (AttributeInfo att in element.Attributes())
            {
                INodeName name = att.GetNodeName();
                if (name.HasURI(NamespaceUri.NULL) && Array.BinarySearch(allowedNames, name.GetLocalPart(), StringComparer.Ordinal) < 0)
                {
                    throw new XPathException("In serialization parameters, attribute @" + name.GetLocalPart() + " must not appear on element " + element.DisplayName, "SEPM0017");
                }
            }
        }

        private static string GetAttribute(NodeInfo element, string localName)
        {
            string value = element.GetAttributeValue(NamespaceUri.NULL, localName);
            if (value == null)
            {
                throw new XPathException("In serialization parameters, attribute @" + localName + " is missing on element " + element.DisplayName);
            }

            return value;
        }

        public SerializationProperties GetSerializationProperties()
        {
            CharacterMapIndex index = new CharacterMapIndex();
            if (characterMap != null)
            {
                index.PutCharacterMap(NamespaceUri.NULL.QName("charMap"), characterMap);
                properties.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, "charMap");
            }

            return new SerializationProperties(properties, index);
        }

        public CharacterMap GetCharacterMap()
        {
            return characterMap;
        }
    }
}
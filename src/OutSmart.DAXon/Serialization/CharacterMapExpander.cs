////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Serialization
{
    public class CharacterMapExpander : ProxyReceiver
    {
        private CharacterMap charMap;
        private bool useNullMarkers = true;
        public CharacterMapExpander(IReceiver next) : base(next)
        {
        }

        public virtual void SetCharacterMap(CharacterMap map)
        {
            charMap = map;
        }

        public virtual CharacterMap GetCharacterMap()
        {
            return charMap;
        }

        public virtual void SetUseNullMarkers(bool use)
        {
            useNullMarkers = use;
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            IList<AttributeInfo> atts2 = new List<AttributeInfo>(attributes.Size());
            foreach (AttributeInfo att in attributes)
            {
                UnicodeString oldValue = StringView.Of(att.Value).Tidy();
                if (!ReceiverOption.Contains(att.GetProperties(), ReceiverOption.DISABLE_CHARACTER_MAPS))
                {
                    UnicodeString mapped = charMap.IMap(oldValue, useNullMarkers);
                    if (mapped != oldValue)
                    {

                        // mapping was done
                        int p2 = (att.GetProperties() | ReceiverOption.USE_NULL_MARKERS) & ~ReceiverOption.NO_SPECIAL_CHARS;
                        atts2.Add(new AttributeInfo(att.GetNodeName(), att.GetType(), mapped.ToString(), att.GetLocation(), p2));
                    }
                    else
                    {
                        atts2.Add(att);
                    }
                }
                else
                {
                    atts2.Add(att);
                }
            }

            nextReceiver.StartElement(elemName, type, SequenceTool.AttributeMapFromList(atts2), namespaces, location, properties);
        }

        /// <summary>
        /// Output character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!ReceiverOption.Contains(properties, ReceiverOption.DISABLE_CHARACTER_MAPS))
            {
                UnicodeString mapped = charMap.IMap(chars, useNullMarkers);
                if (mapped != chars)
                {
                    properties = (properties | ReceiverOption.USE_NULL_MARKERS) & ~ReceiverOption.NO_SPECIAL_CHARS;
                }

                nextReceiver.Characters(mapped, locationId, properties);
            }
            else
            {

                // if the user requests disable-output-escaping, this overrides the character
                // mapping
                nextReceiver.Characters(chars, locationId, properties);
            }
        }
    }
}
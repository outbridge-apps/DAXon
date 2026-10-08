////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal.Collections;
using System.Text;
using OutSmart.DAXon.Events;
namespace OutSmart.DAXon.Serialization
{
    public class SerializationProperties
    {
        Properties properties;
        CharacterMapIndex charMapIndex;
        IFilterFactory validationFactory;

        public virtual IFilterFactory ValidationFactory
        {
            get => validationFactory; set
            {
                this.validationFactory = value;
            }
        }
        /// <summary>
        /// Create a set of defaulted serialization parameters
        /// </summary>
        public SerializationProperties()
        {
            this.properties = new Properties();
        }

        public SerializationProperties(Properties props)
        {
            this.properties = props;
        }

        public SerializationProperties(Properties props, CharacterMapIndex charMapIndex)
        {
            this.properties = props;
            this.charMapIndex = charMapIndex;
        }

        public virtual void SetProperty(string name, string value)
        {
            properties.SetProperty(name, value);
        }

        public virtual string GetProperty(string name)
        {
            return GetProperties().GetProperty(name);
        }

        public virtual Properties GetProperties()
        {
            return properties;
        }

        public virtual CharacterMapIndex GetCharacterMapIndex()
        {
            return charMapIndex;
        }

        public virtual SequenceNormalizer MakeSequenceNormalizer(IReceiver next)
        {
            if (ValidationFactory != null)
            {
                next = ValidationFactory.MakeFilter(next);
            }

            string itemSeparator = properties.GetProperty(DAXonOutputKeys.ITEM_SEPARATOR);
            if (itemSeparator == null || itemSeparator == "#absent")
            {
                return new SequenceNormalizerWithSpaceSeparator(next);
            }
            else
            {
                return new SequenceNormalizerWithItemSeparator(next, StringView.Of(itemSeparator));
            }
        }

        public virtual SerializationProperties CombineWith(SerializationProperties defaults)
        {
            CharacterMapIndex charMap = this.charMapIndex;
            if (charMap == null || charMap.IsEmpty())
            {
                charMap = defaults.GetCharacterMapIndex();
            }

            IFilterFactory validationFactory = this.validationFactory;
            if (validationFactory == null)
            {
                validationFactory = defaults.validationFactory;
            }

            Properties props = new Properties(defaults.GetProperties());
            foreach (string prop in this.GetProperties().StringPropertyNames())
            {
                string value = this.GetProperties().GetProperty(prop);
                if (prop.Equals(DAXonOutputKeys.CDATA_SECTION_ELEMENTS) || prop.Equals(DAXonOutputKeys.SUPPRESS_INDENTATION) || prop.Equals(DAXonOutputKeys.USE_CHARACTER_MAPS))
                {
                    string existing = defaults.GetProperty(prop);
                    if (existing == null || existing.Equals(value))
                    {
                        props.SetProperty(prop, value);
                    }
                    else
                    {
                        props.SetProperty(prop, existing + " " + value);
                        if (prop.Equals(DAXonOutputKeys.USE_CHARACTER_MAPS))
                        {
                            CharacterMapIndex charMapIndex2 = charMap.Copy();
                            foreach (CharacterMap map in defaults.GetCharacterMapIndex())
                            {
                                charMapIndex2.PutCharacterMap(map.Name, map);
                            }

                            charMap = charMapIndex2;
                        }
                    }
                }
                else
                {
                    props.SetProperty(prop, value);
                }
            }

            SerializationProperties newParams = new SerializationProperties(props, charMap);
            newParams.ValidationFactory = validationFactory;
            return newParams;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string k in properties.StringPropertyNames())
            {
                sb.Append(k).Append('=').Append(properties.GetProperty(k)).Append(' ');
            }

            if (charMapIndex != null)
            {
                foreach (CharacterMap cm in charMapIndex)
                {
                    sb.Append(cm.Name.EQName).Append("={").Append(cm.ToString()).Append("} ");
                }
            }

            return sb.ToString();
        }
    }
}
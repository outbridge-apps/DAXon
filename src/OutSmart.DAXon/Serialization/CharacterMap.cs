////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Serialization
{
    public class CharacterMap
    {
        private StructuredQName name;
        private readonly IntHashMap<string> charMap;
        private int min = int.MaxValue; // the lowest mapped character
        private int max = 0; // the highest mapped character
        private bool mapsWhitespace = false;

        public virtual StructuredQName Name => name;

        public virtual IntHashMap<string> Map => charMap;
        public CharacterMap(StructuredQName name, IntHashMap<string> map)
        {
            this.name = name;
            this.charMap = map;
            Init();
        }

        public CharacterMap(IEnumerable<CharacterMap> list, StructuredQName combinedName)
        {
            name = combinedName;
            charMap = new IntHashMap<string>(64);
            foreach (CharacterMap map in list)
            {
                IIntIterator keys = map.charMap.KeyIterator();
                while (keys.MoveNext())
                {
                    int next = keys.Current;
                    charMap.Put(next, map.charMap[next]);
                }
            }

            Init();
        }

        private void Init()
        {
            IIntIterator keys = charMap.KeyIterator();
            while (keys.MoveNext())
            {
                int next = keys.Current;
                if (next < min)
                {
                    min = next;
                }

                if (next > max)
                {
                    max = next;
                }

                if (!mapsWhitespace && Whitespace.IsWhite(next))
                {
                    mapsWhitespace = true;
                }
            }

            if (min > 0xD800)
            {

                // if all the mapped characters are above the BMP, we need to check
                // surrogates
                min = 0xD800;
            }
        }

        // insertNulls: each replacement goes between two U+0000, the marks that tell the serializer not to escape it.
        // The result then holds no other U+0000 - not from the data, not from a replacement.
        public virtual UnicodeString IMap(UnicodeString @in, bool insertNulls)
        {
            if (!mapsWhitespace && @in is WhitespaceString)
            {
                return @in;
            }


            // First scan the string to see if there are any possible mapped
            // characters; if not, don't bother creating the new buffer
            bool move = @in.IndexWhere((c) => (c >= min && c <= max), 0) >= 0;
            if (!move)
            {
                return @in;
            }

            UnicodeBuilder buffer = new UnicodeBuilder();
            IIntIterator iter = @in.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                if (c >= min && c <= max)
                {
                    string rep = charMap[c];
                    if (rep == null)
                    {
                        buffer.Append(c);
                    }
                    else
                    {
                        if (insertNulls)
                        {
                            buffer.Append((char)0);
                            buffer.Append(rep.IndexOf((char)0) < 0 ? rep : rep.Replace("\0", ""));
                            buffer.Append((char)0);
                        }
                        else
                        {
                            buffer.Append(rep);
                        }
                    }
                }
                else if (c != 0 || !insertNulls)
                {
                    buffer.Append(c);
                }
            }

            return buffer.ToUnicodeString();
        }

        public virtual void Export(ExpressionPresenter @out)
        {
            @out.StartElement("charMap");
            @out.EmitAttribute("name", name);
            for (IIntIterator iter = charMap.KeyIterator(); iter.MoveNext();)
            {
                int c = iter.Current;
                string s = charMap[c];
                @out.StartElement("m");
                @out.EmitAttribute("c", c + "");
                @out.EmitAttribute("s", s);
                @out.EndElement();
            }

            @out.EndElement();
        }
    }
}
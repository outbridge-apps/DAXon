////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Charsets;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Core;
using System.IO;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Serialization.CharCodes
{
    public class CharacterSetFactory
    {
        private readonly Dictionary<string, ICharacterSet> characterSets = new Dictionary<string, ICharacterSet>(10);
        /// <summary>
        /// Class has a single instance per Configuration
        /// </summary>
        public CharacterSetFactory()
        {
            Dictionary<string, ICharacterSet> c = characterSets;
            UTF8CharacterSet utf8 = UTF8CharacterSet.GetInstance();
            c["utf8"] = utf8;
            UTF16CharacterSet utf16 = UTF16CharacterSet.GetInstance();
            c["utf16"] = utf16;
            ASCIICharacterSet acs = ASCIICharacterSet.GetInstance();
            c["ascii"] = acs;
            c["iso646"] = acs;
            c["usascii"] = acs;
            ISO88591CharacterSet lcs = ISO88591CharacterSet.GetInstance();
            c["iso88591"] = lcs;
        }

        public virtual void SetCharacterSetImplementation(string encoding, ICharacterSet charSet)
        {
            characterSets[NormalizeCharsetName(encoding)] = charSet;
        }

        private static string NormalizeCharsetName(string name)
        {
            return name.Replace("-", "").Replace("_", "").ToLowerInvariant();
        }

        public virtual ICharacterSet GetCharacterSet(Properties details)
        {
            string encoding = details.GetProperty(DAXonOutputKeys.ENCODING);
            if (encoding == null)
            {
                return UTF8CharacterSet.GetInstance();
            }

            return GetCharacterSet(encoding);
        }

        public virtual ICharacterSet GetCharacterSet(string encoding)
        {
            if (encoding == null)
            {
                return UTF8CharacterSet.GetInstance();
            }
            else
            {
                string encodingKey = NormalizeCharsetName(encoding);
                ICharacterSet cs = characterSets.GetOrDefault(encodingKey);
                if (cs != null)
                {
                    return cs;
                }


                // Not one of the built-in sets: consult the platform. An encoding the platform doesn't
                // recognise must raise SESU0007 — mirrors java.nio.Charset.forName throwing
                // IllegalCharsetNameException / UnsupportedCharsetException.
                Encoding platformEncoding;
                try
                {
                    platformEncoding = Encoding.GetEncoding(encoding);
                }
                catch (ArgumentException)
                {
                    throw new XPathException("Unknown encoding requested: " + encoding, "SESU0007");
                }

                ICharacterSet res = JavaCharacterSet.MakeCharSet(platformEncoding);
                characterSets[encodingKey] = res;
                return res;
            }
        }

        public static void Main(string[] args)
        {
            Console.Error.WriteLine("Available platform encodings:");
            foreach (string s in Encoding.GetEncodings().Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal))
            {
                Console.Error.WriteLine("    " + s);
            }

            Console.Error.WriteLine("Registered Character Sets in Saxon:");
            CharacterSetFactory factory = new CharacterSetFactory();
            foreach (KeyValuePair<string, ICharacterSet> e in factory.characterSets)
            {
                Console.Error.WriteLine("    " + e.Key + " = " + e.Value.GetType().FullName);
            }
        }
    }
}

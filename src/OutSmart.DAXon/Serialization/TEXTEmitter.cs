////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Regex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    internal sealed class TEXTEmitter : XMLEmitter
    {
        private OutSmart.DAXon.Internal.Regex.Pattern newlineMatcher = null;
        private string newlineRepresentation = null;
        /// <summary>
        /// Start of the document.
        /// </summary>
        public override void Open()
        {
        }

        /// <summary>
        /// Start of the document.
        /// </summary>
        protected override void OpenDocument()
        {
            if (characterSet == null)
            {
                characterSet = UTF8CharacterSet.GetInstance();
                allCharactersEncodable = true;
            }


            // Write a BOM if requested
            string encoding = outputProperties.GetProperty(DAXonOutputKeys.ENCODING);
            if (encoding == null || encoding.Equals("utf8", StringComparison.OrdinalIgnoreCase))
            {
                encoding = "UTF-8";
            }

            string byteOrderMark = outputProperties.GetProperty(DAXonOutputKeys.BYTE_ORDER_MARK);
            string nl = outputProperties.GetProperty(DAXonOutputKeys.NEWLINE);
            if (nl != null && !nl.Equals("\n"))
            {
                newlineRepresentation = nl;
                newlineMatcher = OutSmart.DAXon.Internal.Regex.Pattern.Compile("\\n");
            }

            if ("yes".Equals(byteOrderMark) && ("UTF-8".Equals(encoding, StringComparison.OrdinalIgnoreCase) || "UTF-16LE".Equals(encoding, StringComparison.OrdinalIgnoreCase) || "UTF-16BE".Equals(encoding, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    writer.WriteCodePoint(0xFEFF);
                }
                catch (IOException err)
                {
                }
            }

            started = true;
        }

        public override void WriteDeclaration()
        {
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!started)
            {
                OpenDocument();
            }

            if (!ReceiverOption.Contains(properties, ReceiverOption.NO_SPECIAL_CHARS))
            {
                int badchar = TestCharacters(chars);
                if (badchar != 0)
                {
                    throw new XPathException("Output character not available in this encoding (x" + (badchar).ToString("x") + ")", "SERE0008");
                }
            }

            if (newlineMatcher != null)
            {
                chars = StringView.Of(newlineMatcher.Matcher(chars.ToString()).ReplaceAll(newlineRepresentation));
            }

            try
            {
                writer.Write(chars);
            }
            catch (IOException err)
            {
                throw new XPathException(err?.Message);
            }
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            previousAtomic = false;
        }

        public override void EndElement()
        {
        }

        // no-op
        public override void ProcessingInstruction(string name, UnicodeString value, ILocation locationId, int properties)
        {
        }

        // no-op
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
        }
    }
}

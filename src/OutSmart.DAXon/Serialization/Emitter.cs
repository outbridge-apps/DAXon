////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    public abstract class Emitter : SequenceReceiver, IReceiverWithOutputProperties
    {
        protected IUnicodeWriter writer;
        protected Properties outputProperties;
        protected ICharacterSet characterSet;
        protected bool allCharactersEncodable = false;
        private bool mustClose = false;
        public Emitter() : base(null)
        {
        }

        public virtual void SetOutputProperties(Properties details)
        {
            if (characterSet == null)
            {
                characterSet = GetConfiguration().GetCharacterSetFactory().GetCharacterSet(details);
                allCharactersEncodable = (characterSet is UTF8CharacterSet || characterSet is UTF16CharacterSet);
            }

            outputProperties = details;
        }

        public Properties GetOutputProperties()
        {
            return outputProperties;
        }

        public virtual void SetUnicodeWriter(IUnicodeWriter unicodeWriter)
        {
            this.writer = unicodeWriter;
        }

        // A destination that stopped taking the output - a full disk, a closed pipe, a stream of the host's that
        // threw: SXRD0004, naming the destination when it has a name and giving the system's own reason.
        internal static XPathException WriteFailure(IOException err, string systemId)
        {
            return new XPathException("Failure writing to " + (string.IsNullOrEmpty(systemId) ? "the output" : systemId) + ": " + err.Message, err)
                .WithErrorCode(DAXonErrorCode.SXRD0004);
        }

        private protected XPathException WriteFailure(IOException err)
        {
            return WriteFailure(err, GetSystemId());
        }

        // No character of XML, 1.0 or 1.1: U+0000, half a surrogate pair, U+FFFE, U+FFFF.
        internal static bool IsNoCharacter(int c)
        {
            return c == 0 || (c >= 0xD800 && (c <= 0xDFFF || c == 0xFFFE || c == 0xFFFF));
        }

        // What XML cannot hold where no character reference can stand for it - a comment, a processing instruction, a
        // CDATA section: no character of XML, the control characters XML 1.0 does not have, and those 1.1 restricts.
        internal static bool IsNoLiteral(int c, bool xml11)
        {
            if (c < 0x20)
            {
                return c != 0x9 && c != 0xA && c != 0xD;
            }

            if (c < 0xA0)
            {
                return xml11 && c >= 0x7F && c != 0x85;
            }

            return c >= 0xD800 && (c <= 0xDFFF || c == 0xFFFE || c == 0xFFFF);
        }

        public virtual void SetMustClose(bool mustClose)
        {
            this.mustClose = mustClose;
        }

        public override void SetUnparsedEntity(string name, string uri, string publicId)
        {
        }

        /// <summary>
        /// Notify the end of the event stream
        /// </summary>
        private bool released;
        public override void Close()
        {
            if (mustClose && writer != null && !released)
            {
                released = true;
                try
                {
                    writer.Dispose();
                }
                catch (IOException e)
                {
                    throw WriteFailure(e);
                }
            }
        }

        // Abort-path release: same writer release as Close, but silent (no XPathException
        // on the unwind path) and idempotent with it.
        public override void Dispose()
        {
            if (mustClose && writer != null && !released)
            {
                released = true;
                try
                {
                    writer.Dispose();
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>
        /// Notify the end of the event stream
        /// </summary>
        public override bool UsesTypeAnnotations()
        {
            return false;
        }

        /// <summary>
        /// Append an arbitrary item (node or atomic value) to the output
        /// </summary>
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item is NodeInfo)
            {
                Decompose(item, locationId, copyNamespaces);
            }
            else
            {
                Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
            }
        }
    }
}

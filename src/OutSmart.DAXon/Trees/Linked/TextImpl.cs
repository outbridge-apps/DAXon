////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Api;
namespace OutSmart.DAXon.Trees.Linked
{
    public class TextImpl : NodeImpl
    {
        private UnicodeString content;

        public override UnicodeString UnicodeStringValue => content;
        public TextImpl(UnicodeString content)
        {
            this.content = content;
        }

        public virtual void AppendStringValue(UnicodeString content)
        {
            this.content = this.content.Concat(content);
        }

        public override int GetNodeKind()
        {
            return Types.Type.TEXT;
        }

        /// <summary>
        /// Copy this node to a given outputter
        /// </summary>
        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.Characters(content, locationId, ReceiverOption.NONE);
        }

        /// <summary>
        /// Copy this node to a given outputter
        /// </summary>
        public override void ReplaceStringValue(UnicodeString stringValue)
        {
            if (stringValue.IsEmpty())
            {
                Delete();
            }
            else
            {
                content = stringValue;
            }
        }
    }
}

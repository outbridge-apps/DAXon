////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using System;

namespace OutSmart.DAXon.Events
{
    internal sealed class CommentStripper : ProxyReceiver
    {
        private UnicodeString currentTextNode = null;
        private Func<INodeName, bool> skippedElementTest = (INodeName name) => false;
        private int depthOfHole = 0;
        public CommentStripper(IReceiver next) : base(next)
        {
        }

        public void SetSkippedElementTest(Func<INodeName, bool> test)
        {
            this.skippedElementTest = test;
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (depthOfHole == 0)
            {
                if (skippedElementTest(elemName))
                {
                    depthOfHole++;
                }
                else
                {
                    Flush();
                    nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
                }
            }
            else
            {
                depthOfHole++;
            }
        }

        /// <summary>
        /// Callback interface for SAX: not for application use
        /// </summary>
        public override void EndElement()
        {
            if (depthOfHole > 0)
            {
                depthOfHole--;
            }
            else
            {
                Flush();
                nextReceiver.EndElement();
            }
        }

        /// <summary>
        /// Callback interface for SAX: not for application use
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (depthOfHole == 0)
            {
                if (currentTextNode == null)
                {
                    currentTextNode = chars;
                }
                else
                {
                    currentTextNode = currentTextNode.Concat(chars);
                }
            }
        }

        /// <summary>
        /// Remove comments
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
        }

        /// <summary>
        /// Remove processing instructions
        /// </summary>
        public override void ProcessingInstruction(string name, UnicodeString data, ILocation locationId, int properties)
        {
        }

        /// <summary>
        /// Remove processing instructions
        /// </summary>
        private void Flush()
        {
            if (currentTextNode != null)
            {
                nextReceiver.Characters(currentTextNode, Loc.NONE, ReceiverOption.NONE);
            }

            currentTextNode = null;
        }
    }
}

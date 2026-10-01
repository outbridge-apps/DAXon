////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;

namespace OutSmart.DAXon.Events
{
    internal sealed class IgnorableWhitespaceStripper : ProxyReceiver
    {
        private bool[] stripStack = new bool[100];
        private int top = 0;
        public IgnorableWhitespaceStripper(IReceiver next) : base(next)
        {
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
            bool strip = false;
            if (type != Untyped.INSTANCE)
            {

                // if the element has element-only content, whitespace stripping is enabled
                if (type.IsComplexType() && !((IComplexType)type).IsSimpleContent() && !((IComplexType)type).IsMixedContent())
                {
                    strip = true;
                }
            }


            // put "strip" value on top of stack
            top++;
            if (top >= stripStack.Length)
            {
                Array.Resize(ref stripStack, top * 2);
            }

            stripStack[top] = strip;
        }

        /// <summary>
        /// Handle an end-of-element event
        /// </summary>
        public override void EndElement()
        {
            nextReceiver.EndElement();
            top--;
        }

        /// <summary>
        /// Handle a text node
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!chars.IsEmpty() && (!stripStack[top] || !Whitespace.IsAllWhite(chars)))
            {
                nextReceiver.Characters(chars, locationId, properties);
            }
        }

        /// <summary>
        /// Handle a text node
        /// </summary>
        public override bool UsesTypeAnnotations()
        {
            return true;
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Events
{
    public class Stripper : ProxyReceiver
    {

        public const int ALWAYS_PRESERVE = 0x01; // whitespace always preserved (e.g. xsl:text)
        public const int ALWAYS_STRIP = 0x02; // whitespace always stripped (e.g. xsl:choose)
        public const int STRIP_DEFAULT = 0x00; // no special action
        public const int PRESERVE_PARENT = 0x04; // parent element specifies xml:space="preserve"
        public const int SIMPLE_CONTENT = 0x08; // type annotation indicates simple typed content
        public const int ASSERTIONS_EXIST = 0x10; // XSD 1.1 assertions are in scope
        public static readonly StripRuleTarget STRIP = new StripRuleTarget();
        public static readonly StripRuleTarget PRESERVE = new StripRuleTarget();
        protected ISpaceStrippingRule rule;

        private int[] stripStack = new int[100];
        private int top = 0;
        public Stripper(ISpaceStrippingRule rule, IReceiver next) : base(next)
        {
            this.rule = rule;
        }
        private int IsSpacePreserving(INodeName name, ISchemaType type)
        {
            return rule.IsSpacePreserving(name, type);
        }
        /// <summary>
        /// Callback interface for SAX: not for application use
        /// </summary>
        public override void Open()
        {

            top = 0;
            stripStack[top] = ALWAYS_PRESERVE; // {xml:space = default, preserve this element = true}
            base.Open();
        }

        /// <summary>
        /// Callback interface for SAX: not for application use
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {

            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
            int preserveParent = stripStack[top];
            int preserve = preserveParent & (PRESERVE_PARENT | ASSERTIONS_EXIST);
            int elementStrip = IsSpacePreserving(elemName, type);
            if (elementStrip == ALWAYS_PRESERVE)
            {
                preserve |= ALWAYS_PRESERVE;
            }
            else if (elementStrip == ALWAYS_STRIP)
            {
                preserve |= ALWAYS_STRIP;
            }

            if (type != Untyped.INSTANCE)
            {
                if (preserve == 0)
                {

                    // if the element has simple content, whitespace stripping is disabled
                    if (type.IsSimpleType() || ((IComplexType)type).IsSimpleContent())
                    {
                        preserve |= SIMPLE_CONTENT;
                    }
                }

                if (type is IComplexType && ((IComplexType)type).HasAssertions())
                {
                    preserve |= ASSERTIONS_EXIST;
                }
            }


            // put "preserve" value on top of stack
            top++;
            if (top >= stripStack.Length)
            {
                Array.Resize(ref stripStack, top * 2);
            }

            stripStack[top] = preserve;
            string xmlSpace = attributes.GetValue(NamespaceUri.XML, "space");
            if (xmlSpace != null)
            {
                if (Whitespace.Trim(xmlSpace).Equals("preserve"))
                {
                    stripStack[top] |= PRESERVE_PARENT;
                }
                else
                {
                    stripStack[top] &= ~PRESERVE_PARENT;
                }
            }
        }

        /// <summary>
        /// Handle an end-of-element event
        /// </summary>
        public override void EndElement()
        {
            nextReceiver.EndElement();
            top--;
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {

            // assume adjacent chunks of text are already concatenated
            if (((((stripStack[top] & (ALWAYS_PRESERVE | PRESERVE_PARENT | SIMPLE_CONTENT | ASSERTIONS_EXIST)) != 0) && (stripStack[top] & ALWAYS_STRIP) == 0) || !Whitespace.IsAllWhite(chars)) && !chars.IsEmpty())
            {
                nextReceiver.Characters(chars, locationId, properties);
            }
        }

        public override bool UsesTypeAnnotations()
        {
            return true;
        }

        public class StripRuleTarget : IRuleTarget
        {
            public virtual void Export(ExpressionPresenter presenter)
            {
            }

            // no-op
            public virtual void RegisterRule(Rule rule)
            {
            }
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Api
{
    public class WhitespaceStrippingPolicy
    {
        public static readonly WhitespaceStrippingPolicy NONE = new WhitespaceStrippingPolicy(Whitespace.NONE);
        public static readonly WhitespaceStrippingPolicy IGNORABLE = new WhitespaceStrippingPolicy(Whitespace.IGNORABLE);
        /// <summary>
        /// The value ALL indicates that all whitespace-only text nodes are discarded.
        /// </summary>
        public static readonly WhitespaceStrippingPolicy ALL = new WhitespaceStrippingPolicy(Whitespace.ALL);
        /// <summary>
        /// UNSPECIFIED means that no other value has been specifically requested.
        /// </summary>
        public static readonly WhitespaceStrippingPolicy UNSPECIFIED = new WhitespaceStrippingPolicy(Whitespace.UNSPECIFIED);
        private readonly int policy;
        private ISpaceStrippingRule stripperRules;

        public virtual ISpaceStrippingRule SpaceStrippingRule => stripperRules;

        private WhitespaceStrippingPolicy(int policy)
        {
            this.policy = policy;
            switch (policy)
            {
                case Whitespace.ALL:
                    stripperRules = AllElementsSpaceStrippingRule.GetInstance();
                    break;
                case Whitespace.NONE:
                    stripperRules = NoElementsSpaceStrippingRule.GetInstance();
                    break;
                case Whitespace.IGNORABLE:
                    stripperRules = IgnorableSpaceStrippingRule.GetInstance();
                    break;
                default:
                    break;
            }
        }

        public WhitespaceStrippingPolicy(StylesheetPackage pack)
        {
            policy = Whitespace.XSLT;
            stripperRules = pack.StripperRules;
        }
        public static WhitespaceStrippingPolicy MakeCustomPolicy(Func<QName, bool> elementTest)
        {
            ISpaceStrippingRule rule = new AnonymousISpaceStrippingRule(elementTest);
            WhitespaceStrippingPolicy wsp = new WhitespaceStrippingPolicy(Whitespace.XSLT);
            wsp.stripperRules = rule;
            return wsp;
        }

        public virtual int Ordinal()
        {
            return policy;
        }

        public virtual IFilterFactory MakeStripper()
        {
            return (next) => new Stripper(stripperRules, next);
        }

        private sealed class AnonymousISpaceStrippingRule : ISpaceStrippingRule
        {

            private readonly Func<QName, bool> elementTest;
            public AnonymousISpaceStrippingRule(Func<QName, bool> elementTest)
            {
                this.elementTest = elementTest;
            }
            public int IsSpacePreserving(INodeName nodeName, ISchemaType schemaType)
            {
                return elementTest(new QName(nodeName.GetStructuredQName())) ? Stripper.ALWAYS_STRIP : Stripper.ALWAYS_PRESERVE;
            }

            public ProxyReceiver MakeStripper(IReceiver next)
            {
                return new Stripper(this, next);
            }

            public void Export(ExpressionPresenter presenter)
            {
                throw new NotSupportedException();
            }
        }
    }
}
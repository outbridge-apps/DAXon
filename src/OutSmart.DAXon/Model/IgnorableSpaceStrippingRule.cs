////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Model
{
    internal sealed class IgnorableSpaceStrippingRule : ISpaceStrippingRule
    {
        private static readonly IgnorableSpaceStrippingRule THE_INSTANCE = new IgnorableSpaceStrippingRule();
        public static IgnorableSpaceStrippingRule GetInstance()
        {
            return THE_INSTANCE;
        }

        public int IsSpacePreserving(INodeName name, ISchemaType schemaType)
        {
            if (schemaType != Untyped.INSTANCE && schemaType.IsComplexType() && !((IComplexType)schemaType).IsSimpleContent() && !((IComplexType)schemaType).IsMixedContent())
            {
                return Stripper.ALWAYS_STRIP;
            }
            else
            {
                return Stripper.ALWAYS_PRESERVE;
            }
        }

        public ProxyReceiver MakeStripper(IReceiver next)
        {
            return new IgnorableWhitespaceStripper(next);
        }

        public void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("strip.ignorable");
            presenter.EndElement();
        }
    }
}
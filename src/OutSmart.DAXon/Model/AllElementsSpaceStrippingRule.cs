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
    internal sealed class AllElementsSpaceStrippingRule : ISpaceStrippingRule
    {
        private static readonly AllElementsSpaceStrippingRule THE_INSTANCE = new AllElementsSpaceStrippingRule();
        public static AllElementsSpaceStrippingRule GetInstance()
        {
            return THE_INSTANCE;
        }

        public int IsSpacePreserving(INodeName fingerprint, ISchemaType schemaType)
        {
            return Stripper.STRIP_DEFAULT;
        }

        public ProxyReceiver MakeStripper(IReceiver next)
        {
            return new Stripper(this, next);
        }

        public void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("strip.all");
            presenter.EndElement();
        }
    }
}
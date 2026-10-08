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
    /// <summary>
    /// Interface for deciding whether a particular element is to have whitespace text nodes stripped
    /// </summary>
    public interface ISpaceStrippingRule
    {
        int IsSpacePreserving(INodeName nodeName, ISchemaType schemaType);
        ProxyReceiver MakeStripper(IReceiver next);
        void Export(ExpressionPresenter presenter);
    }
}
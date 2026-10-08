////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;namespace OutSmart.DAXon.Lib
{
    /// <summary>
    /// An IActiveSource is capable of delivering an XML document to a IReceiver. P5: this is a native Saxon
    /// abstraction, no longer rooted in the JAXP <c>Source</c> shim; it declares its own systemId accessors.
    /// Concrete active sources that must also flow through the remaining <c>Source</c>-typed plumbing (NodeSource,
    /// EventSource, ActiveStreamSource:StreamSource, ActiveSAXSource:SAXSource) additionally implement <c>Source</c>.
    /// </summary>
    public interface IActiveSource
    {
        void SetSystemId(string systemId);
        string GetSystemId();
        void Deliver(IReceiver receiver, ParseOptions options);
    }
}
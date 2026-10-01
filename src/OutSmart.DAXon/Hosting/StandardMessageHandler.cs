////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;

namespace OutSmart.DAXon.Lib
{
    // StandardMessageHandler stub — paulirwin emitted as plain class but should
    // be Action<Message>. Use implicit operator (Action<T> is a delegate, so we wrap).
    internal sealed class StandardMessageHandler
    {
        public StandardMessageHandler(object factory) { }
        public void Accept(Message msg) { }
        public static implicit operator Action<Message>(StandardMessageHandler h) => h == null ? null : h.Accept;
    }
}

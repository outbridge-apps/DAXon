////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions
{
    // CallableDelegate is a class implementing ICallable so that
    //   `ICallable callable = new CallableDelegate((ctx, args) => ...);`
    // compiles. Wraps a lambda that returns ISequence.
    internal sealed class CallableDelegate : ICallable
    {
        private readonly Func<IXPathContext, ISequence[], ISequence> _impl;
        public CallableDelegate(Func<IXPathContext, ISequence[], ISequence> impl) { _impl = impl; }
        public ISequence Call(IXPathContext context, ISequence[] arguments)
            => _impl != null ? _impl(context, arguments) : null;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/functions/EscapeHtmlUri.java (the class was missing, so
// fn:escape-html-uri was unregistered/unresolved).

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    /// <summary>Implements fn:escape-html-uri — percent-escapes only non-ASCII/control chars for HTML URI attributes.</summary>
    internal sealed class EscapeHtmlUri : ScalarSystemFunction
    {
        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            return new StringValue(HTMLURIEscaper.EscapeURL(arg.GetStringValue(), false, GetRetainedStaticContext().GetConfiguration()));
        }

        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }
    }
}

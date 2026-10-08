////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Instructions;

namespace OutSmart.DAXon.Expressions.Parsing
{
    // Real ScopedBindingElement.cs (excluded) is a marker interface with one method;
    // the hollow stub lacked it, so ExpressionTool's ((IScopedBindingElement)exp).AllocateSlots(..)
    // failed CS1061. No compiled class implements this interface, so adding the method cannot cascade.
    internal interface IScopedBindingElement { int AllocateSlots(SlotManager slotManager, int nextFree); }
}

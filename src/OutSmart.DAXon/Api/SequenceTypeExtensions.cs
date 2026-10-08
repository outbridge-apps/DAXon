////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Api
{

    public static class SequenceTypeExtensions
    {
        // The engine has no more to say about a mismatch than the error itself says.
        public static string ExplainMismatch(this SequenceType st, object item, object th) => "";

        // The occurrence indicator as the engine's cardinality bits (StaticProperty.ALLOWS_...).
        public static int GetCardinality(this SequenceType st) => st.GetOccurrenceIndicator().GetCardinality();
    }
}

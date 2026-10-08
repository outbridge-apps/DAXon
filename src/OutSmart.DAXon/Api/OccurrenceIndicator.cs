////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Api
{
    public enum OccurrenceIndicator
    {
        ZERO,
        ZERO_OR_ONE,
        ZERO_OR_MORE,
        ONE,
        ONE_OR_MORE

        // --------------------
        // protected int getCardinality() {
        //             return StaticProperty.EMPTY;
        //         case ZERO_OR_ONE:
        //             return StaticProperty.ALLOWS_ZERO_OR_ONE;
        //         case ZERO_OR_MORE:
        //             return StaticProperty.ALLOWS_ZERO_OR_MORE;
        //         case ONE:
        //             return StaticProperty.ALLOWS_ONE;
        //         case ONE_OR_MORE:
        //             return StaticProperty.ALLOWS_ONE_OR_MORE;
        //         default:
        //             return StaticProperty.EMPTY;
        // protected static OccurrenceIndicator getOccurrenceIndicator(int cardinality) {
        //             return ZERO;
        //         case StaticProperty.ALLOWS_ZERO_OR_ONE:
        //             return ZERO_OR_ONE;
        //         case StaticProperty.ALLOWS_ZERO_OR_MORE:
        //             return ZERO_OR_MORE;
        //         case StaticProperty.ALLOWS_ONE:
        //             return ONE;
        //         case StaticProperty.ALLOWS_ONE_OR_MORE:
        //             return ONE_OR_MORE;
        //         default:
        //             return ZERO_OR_MORE;
    }
}
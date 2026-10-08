////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// Enumeration class giving the different streamability categories defined for stylesheet functions in XSLT 3.0
    /// </summary>
    public enum FunctionStreamability
    {
        // UNCLASSIFIED("unclassified")
        UNCLASSIFIED,
        // ABSORBING("absorbing")
        ABSORBING,
        // INSPECTION("inspection")
        INSPECTION,
        // FILTER("filter")
        FILTER,
        // SHALLOW_DESCENT("shallow-descent")
        SHALLOW_DESCENT,
        // DEEP_DESCENT("deep-descent")
        DEEP_DESCENT,
        // ASCENT("ascent")
        ASCENT

        // --------------------
        // public String streamabilityStr;
        // public boolean isConsuming() {
        // public boolean isStreaming() {
        //     streamabilityStr = v;
        // }
        // public static FunctionStreamability of(String v) {
        //             return UNCLASSIFIED;
        //         case "absorbing":
        //             return ABSORBING;
        //         case "inspection":
        //             return INSPECTION;
        //         case "filter":
        //             return FILTER;
        //         case "shallow-descent":
        //             return SHALLOW_DESCENT;
        //         case "deep-descent":
        //             return DEEP_DESCENT;
        //         case "ascent":
        //             return ASCENT;
        //         default:
        //             throw new ArgumentException();
        // --------------------
    }
}
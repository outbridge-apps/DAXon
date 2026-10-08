////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Lib
{
    internal sealed class Validation
    {
        /// <summary>
        /// Code indicating that the value of a validation request was invalid
        /// </summary>
        public const int INVALID = -1;
        /// <summary>
        /// Code for strict validation
        /// </summary>
        public const int STRICT = 1;
        /// <summary>
        /// Code for lax validation
        /// </summary>
        public const int LAX = 2;
        public const int PRESERVE = 3;
        public const int STRIP = 4;
        /// <summary>
        /// Synonym for {@link #STRIP}, corresponding to XQuery usage
        /// </summary>
        public const int SKIP = 4; // synonym provided for the XQuery API
        /// <summary>
        /// Code indicating that no specific validation options were requested
        /// </summary>
        public const int DEFAULT = 0;
        /// <summary>
        /// Code indicating that validation against a named type was requested
        /// </summary>
        public const int BY_TYPE = 8;

        public static int GetCode(string value)
        {
            if (value.Equals("strict"))
            {
                return STRICT;
            }
            else if (value.Equals("lax"))
            {
                return LAX;
            }
            else if (value.Equals("preserve"))
            {
                return PRESERVE;
            }
            else if (value.Equals("strip"))
            {
                return STRIP;
            }
            else
            {
                return INVALID;
            }
        }

        public static string Describe(int value)
        {
            switch (value)
            {
                case STRICT:
                    return "strict";
                case LAX:
                    return "lax";
                case PRESERVE:
                    return "preserve";
                case STRIP:
                    return "skip"; // for XQuery
                case BY_TYPE:
                    return "by type";
                default:
                    return "invalid";
            }
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Events
{
    internal sealed class ReceiverOption
    {
        public const int NONE = 0;
        /// <summary>
        /// Flag set on a characters() event to disable output escaping
        /// </summary>
        public const int DISABLE_ESCAPING = 0x01;
        /// <summary>
        /// Flag to disable use of character maps
        /// </summary>
        public const int DISABLE_CHARACTER_MAPS = 0x02;
        public const int NO_SPECIAL_CHARS = 0x04;
        public const int DEFAULTED_VALUE = 0x08;
        public const int NILLED_ELEMENT = 0x10;
        /// <summary>
        /// Flag indicating that duplicate values should be rejected
        /// </summary>
        public const int REJECT_DUPLICATES = 0x20;
        public const int NAMESPACE_OK = 0x40;
        public const int DISINHERIT_NAMESPACES = 0x80;
        public const int USE_NULL_MARKERS = 0x100;
        public const int WHOLE_TEXT_NODE = 0x400;
        /// <summary>
        /// Flag indicating an element or attribute that has the is-id property
        /// </summary>
        public const int IS_ID = 0x800;
        public const int IS_IDREF = 0x1000;
        /// <summary>
        /// Flag set on startDocument() in relation to an xsl:message call with terminate="yes"
        /// </summary>
        public const int TERMINATE = 0x4000;
        /// <summary>
        /// Flag set on startDocument() to indicate that the constructed document must be updatable
        /// </summary>
        public const int MUTABLE_TREE = 0x8000;
        public const int REFUSE_NAMESPACES = 0x10000;
        public const int BEQUEATH_INHERITED_NAMESPACES_ONLY = 0x20000;
        /// <summary>
        /// Flag set on append() to indicate that all in-scope namespaces should be copied
        /// </summary>
        public const int ALL_NAMESPACES = 0x80000;
        /// <summary>
        /// Flag set on attribute() to indicate that there is no need to check for duplicate attributes
        /// </summary>
        public const int NOT_A_DUPLICATE = 0x100000;
        public static bool Contains(int options, int option)
        {
            return (options & option) != 0;
        }
    }
}
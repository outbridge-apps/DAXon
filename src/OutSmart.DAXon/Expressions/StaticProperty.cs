////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Expressions
{
    internal abstract class StaticProperty
    {
        /// <summary>
        /// Bit setting: Expression depends on current() item
        /// </summary>
        public const int DEPENDS_ON_CURRENT_ITEM = 1;
        public const int DEPENDS_ON_CONTEXT_ITEM = 1 << 1;
        /// <summary>
        /// Bit setting: Expression depends on position()
        /// </summary>
        public const int DEPENDS_ON_POSITION = 1 << 2;
        /// <summary>
        /// Bit setting: Expression depends on last()
        /// </summary>
        public const int DEPENDS_ON_LAST = 1 << 3;
        /// <summary>
        /// Bit setting: Expression depends on the document containing the context node
        /// </summary>
        public const int DEPENDS_ON_CONTEXT_DOCUMENT = 1 << 4;
        /// <summary>
        /// Bit setting: Expression depends on current-group() and/or current-grouping-key()
        /// </summary>
        public const int DEPENDS_ON_CURRENT_GROUP = 1 << 5;
        /// <summary>
        /// Bit setting: Expression depends on regex-group()
        /// </summary>
        public const int DEPENDS_ON_REGEX_GROUP = 1 << 6;
        public const int DEPENDS_ON_LOCAL_VARIABLES = 1 << 7;
        /// <summary>
        /// Bit setting: Expression depends on user-defined functions
        /// </summary>
        public const int DEPENDS_ON_USER_FUNCTIONS = 1 << 8;
        /// <summary>
        /// Bit setting: Expression depends on assignable global variables
        /// </summary>
        public const int DEPENDS_ON_ASSIGNABLE_GLOBALS = 1 << 9;
        /// <summary>
        /// Bit setting: Expression can't be evaluated at compile time for reasons other than the above
        /// </summary>
        public const int DEPENDS_ON_RUNTIME_ENVIRONMENT = 1 << 10;
        public const int DEPENDS_ON_STATIC_CONTEXT = 1 << 11;
        /// <summary>
        /// Bit setting: Expression binds (and typically references) its own range variables
        /// </summary>
        public const int DEPENDS_ON_OWN_RANGE_VARIABLES = 1 << 12;
        /// <summary>
        /// Combination of bits representing dependencies on the XSLT context
        /// </summary>
        public const int DEPENDS_ON_XSLT_CONTEXT = DEPENDS_ON_CURRENT_ITEM | DEPENDS_ON_CURRENT_GROUP | DEPENDS_ON_REGEX_GROUP | DEPENDS_ON_ASSIGNABLE_GLOBALS;
        /// <summary>
        /// Combination of bits representing dependencies on the focus
        /// </summary>
        public const int DEPENDS_ON_FOCUS = DEPENDS_ON_CONTEXT_ITEM | DEPENDS_ON_POSITION | DEPENDS_ON_LAST | DEPENDS_ON_CONTEXT_DOCUMENT;
        public const int DEPENDS_ON_NON_DOCUMENT_FOCUS = DEPENDS_ON_CONTEXT_ITEM | DEPENDS_ON_POSITION | DEPENDS_ON_LAST;
        /*
    * Bit set if an empty sequence is allowed
    */
        public const int ALLOWS_ZERO = 1 << 13;
        /// <summary>
        /// Bit set if a single value is allowed
        /// </summary>
        public const int ALLOWS_ONE = 1 << 14;
        /// <summary>
        /// Bit set if multiple values are allowed
        /// </summary>
        public const int ALLOWS_MANY = 1 << 15;
        /// <summary>
        /// Mask for all cardinality bits
        /// </summary>
        public const int CARDINALITY_MASK = ALLOWS_ZERO | ALLOWS_ONE | ALLOWS_MANY;
        /// <summary>
        /// Occurence indicator for "one or more" (+)
        /// </summary>
        public const int ALLOWS_ONE_OR_MORE = ALLOWS_ONE | ALLOWS_MANY;
        /// <summary>
        /// Occurrence indicator for "zero or more" (*)
        /// </summary>
        public const int ALLOWS_ZERO_OR_MORE = ALLOWS_ZERO | ALLOWS_ONE | ALLOWS_MANY;
        /// <summary>
        /// Occurence indicator for "zero or one" (?)
        /// </summary>
        public const int ALLOWS_ZERO_OR_ONE = ALLOWS_ZERO | ALLOWS_ONE;
        /// <summary>
        /// Occurence indicator for "exactly one" (default occurrence indicator)
        /// </summary>
        public const int EXACTLY_ONE = ALLOWS_ONE;
        /// <summary>
        /// Occurence indicator when an empty sequence is required
        /// </summary>
        public const int EMPTY = ALLOWS_ZERO;

        public const int CONTEXT_DOCUMENT_NODESET = 1 << 16;
        public const int ORDERED_NODESET = 1 << 17;
        public const int REVERSE_DOCUMENT_ORDER = 1 << 18;
        public const int PEER_NODESET = 1 << 19;
        public const int SUBTREE_NODESET = 1 << 20;
        public const int ATTRIBUTE_NS_NODESET = 1 << 21;
        public const int ALL_NODES_NEWLY_CREATED = 1 << 22;
        public const int NO_NODES_NEWLY_CREATED = 1 << 23;
        public const int SINGLE_DOCUMENT_NODESET = 1 << 24;
        public const int HAS_SIDE_EFFECTS = 1 << 25;
        public const int NOT_UNTYPED_ATOMIC = 1 << 26;
        public const int ALL_NODES_UNTYPED = 1 << 27;
        public const int COMPUTED_FUNCTION = 1 << 28;
        /// <summary>
        /// Mask to select all the dependency bits
        /// </summary>
        public const int DEPENDENCY_MASK = DEPENDS_ON_CONTEXT_DOCUMENT | DEPENDS_ON_CONTEXT_ITEM | DEPENDS_ON_CURRENT_GROUP | DEPENDS_ON_REGEX_GROUP | DEPENDS_ON_CURRENT_ITEM | DEPENDS_ON_FOCUS | DEPENDS_ON_LOCAL_VARIABLES | DEPENDS_ON_USER_FUNCTIONS | DEPENDS_ON_ASSIGNABLE_GLOBALS | DEPENDS_ON_RUNTIME_ENVIRONMENT | DEPENDS_ON_STATIC_CONTEXT | DEPENDS_ON_OWN_RANGE_VARIABLES | HAS_SIDE_EFFECTS;
        public const int SPECIAL_PROPERTY_MASK = CONTEXT_DOCUMENT_NODESET | ORDERED_NODESET | REVERSE_DOCUMENT_ORDER | PEER_NODESET | SUBTREE_NODESET | ATTRIBUTE_NS_NODESET | SINGLE_DOCUMENT_NODESET | NO_NODES_NEWLY_CREATED | HAS_SIDE_EFFECTS | NOT_UNTYPED_ATOMIC | ALL_NODES_UNTYPED | ALL_NODES_NEWLY_CREATED | COMPUTED_FUNCTION;
        /// <summary>
        /// Mask for nodeset-related properties
        /// </summary>
        public const int NODESET_PROPERTIES = CONTEXT_DOCUMENT_NODESET | ORDERED_NODESET | REVERSE_DOCUMENT_ORDER | PEER_NODESET | SUBTREE_NODESET | ATTRIBUTE_NS_NODESET | SINGLE_DOCUMENT_NODESET | ALL_NODES_UNTYPED;
    }
}
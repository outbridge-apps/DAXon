////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Trees.Tiny
{
    public class TreeStatistics
    {
        public readonly Statistics SOURCE_DOCUMENT_STATISTICS = new Statistics();
        public readonly Statistics TEMPORARY_TREE_STATISTICS = new Statistics();
        public readonly Statistics RESULT_TREE_STATISTICS = new Statistics();
        public readonly Statistics ASSERTION_TREE_STATISTICS = new Statistics();
        public readonly Statistics FN_PARSE_STATISTICS = new Statistics();
    }
}
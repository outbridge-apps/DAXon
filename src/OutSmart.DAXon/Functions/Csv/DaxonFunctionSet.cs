////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values.Maps;

namespace OutSmart.DAXon.Functions.Csv
{
    // fn:parse-csv, fn:csv-to-xml and fn:csv-doc of the XPath 4.0 draft (2026-10), in DAXon's own namespace. Not registered
    // until XPath 4.0 settles them.
    internal sealed class DaxonFunctionSet : BuiltInFunctionSet
    {
        private static readonly DaxonFunctionSet THE_INSTANCE = new DaxonFunctionSet();

        public override string ConventionalPrefix => "daxon";

        private DaxonFunctionSet()
        {
            Register("parse-csv", 1, e => e.Populate(() => new ParseCsvFn(), MapType.ANY_MAP_TYPE, OPT, LATE)
                .Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            Register("parse-csv", 2, e => e.Populate(() => new ParseCsvFn(), MapType.ANY_MAP_TYPE, OPT, LATE)
                .Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, MapType.ANY_MAP_TYPE, OPT, null).SetOptionDetails(CsvOptions.Details));
            Register("csv-to-xml", 1, e => e.Populate(() => new CsvToXmlFn(), new DocumentNodeTest(NodeKindTest.ELEMENT), OPT, LATE | NEW)
                .Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            Register("csv-to-xml", 2, e => e.Populate(() => new CsvToXmlFn(), new DocumentNodeTest(NodeKindTest.ELEMENT), OPT, LATE | NEW)
                .Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, MapType.ANY_MAP_TYPE, OPT, null).SetOptionDetails(CsvOptions.Details));
            Register("csv-doc", 1, e => e.Populate(() => new CsvDocFn(), MapType.ANY_MAP_TYPE, OPT, BASE | LATE)
                .Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY));
            Register("csv-doc", 2, e => e.Populate(() => new CsvDocFn(), MapType.ANY_MAP_TYPE, OPT, BASE | LATE)
                .Arg(0, BuiltInAtomicType.STRING, OPT, EMPTY).Arg(1, MapType.ANY_MAP_TYPE, OPT, null).SetOptionDetails(CsvOptions.Details));
        }

        public static DaxonFunctionSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public override NamespaceUri GetNamespace()
        {
            return NamespaceUri.DAXON;
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Patterns
{
    /// <summary>
    /// A QNameTest that matches if the name matches any one of a supplied list of QNameTests. Used for the
    /// union name tests in a try/catch clause (<c>catch err:A | err:B { ... }</c>) and xsl:catch/@errors.
    /// Ported from upstream Saxon (was a hollow excluded stub — the (IQNameTest) cast in
    /// XQueryParser.ParseTryCatchExpression / XSLCatch threw InvalidCastException at runtime).
    /// </summary>
    internal sealed class UnionQNameTest : IQNameTest
    {
        private readonly IList<IQNameTest> tests;

        public UnionQNameTest(IList<IQNameTest> tests)
        {
            // Copy — callers (XQueryParser.ParseTryCatchExpression) reuse a single list across catch clauses,
            // Clear()ing and refilling it per clause; aliasing it here would let a later `catch *` mutate an
            // earlier clause's union into an always-match.
            this.tests = new List<IQNameTest>(tests);
        }

        public bool Matches(StructuredQName qname)
        {
            foreach (IQNameTest test in tests)
            {
                if (test.Matches(qname))
                {
                    return true;
                }
            }

            return false;
        }

        public bool MatchesFingerprint(NamePool namePool, int fp)
        {
            foreach (IQNameTest test in tests)
            {
                if (test.MatchesFingerprint(namePool, fp))
                {
                    return true;
                }
            }

            return false;
        }

        public string ExportQNameTest()
        {
            StringBuilder fsb = new StringBuilder();
            for (int i = 0; i < tests.Count; i++)
            {
                if (i != 0)
                {
                    fsb.Append(' ');
                }

                fsb.Append(tests[i].ExportQNameTest());
            }

            return fsb.ToString();
        }

        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder();
            for (int i = 0; i < tests.Count; i++)
            {
                if (i != 0)
                {
                    fsb.Append(" | ");
                }

                fsb.Append(tests[i].ToString());
            }

            return fsb.ToString();
        }
    }
}

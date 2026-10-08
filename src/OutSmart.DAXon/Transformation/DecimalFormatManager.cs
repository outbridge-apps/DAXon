////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Transformation
{
    public class DecimalFormatManager
    {
        private readonly DecimalSymbols defaultDFS;
        private readonly Dictionary<StructuredQName, DecimalSymbols> formatTable; // table for named decimal formats
        private readonly HostLanguage language;
        private readonly int languageLevel;

        public virtual DecimalSymbols DefaultDecimalFormat => defaultDFS;

        public virtual IEnumerable<StructuredQName> DecimalFormatNames => formatTable.Keys;
        /// <summary>
        /// create a DecimalFormatManager and initialise variables
        /// </summary>
        public DecimalFormatManager(HostLanguage language, int languageLevel)
        {
            formatTable = new Dictionary<StructuredQName, DecimalSymbols>(10);
            defaultDFS = new DecimalSymbols(language, languageLevel);
            this.language = language;
            this.languageLevel = languageLevel;
        }

        public virtual DecimalSymbols GetNamedDecimalFormat(StructuredQName qName)
        {
            DecimalSymbols ds = formatTable.GetOrDefault(qName);
            if (ds == null)
            {
                return null; // following two lines had been added to the code since 9.4, but they break XSLT test error089
                //            ds = new DecimalSymbols();
                //            formatTable.put(qName, ds);
            }

            return ds;
        }

        public virtual DecimalSymbols ObtainNamedDecimalFormat(StructuredQName qName)
        {
            DecimalSymbols ds = formatTable.GetOrDefault(qName);
            if (ds == null)
            {
                ds = new DecimalSymbols(language, languageLevel);
                formatTable[qName] = ds;
            }

            return ds;
        }

        public virtual void CheckConsistency()
        {
            defaultDFS.CheckConsistency(null);
            foreach (KeyValuePair<StructuredQName, DecimalSymbols> entry in formatTable)
            {
                entry.Value.CheckConsistency(entry.Key);
            }
        }
    }
}

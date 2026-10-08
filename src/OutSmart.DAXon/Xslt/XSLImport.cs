////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// xsl:import element in the stylesheet. <br>
    /// </summary>
    internal sealed class XSLImport : XSLGeneralIncorporate
    {
        /// <summary>
        /// isImport() returns true if this is an xsl:import statement rather than an xsl:include
        /// </summary>
        public override bool IsImport()
        {
            return true;
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// What a stylesheet or query asks the engine to read; passed to
    /// <see cref="ResourceAccessPolicy.PermitsRead"/>.
    /// </summary>
    public enum ResourceKind
    {
        /// <summary>doc(), document(), xsl:source-document, xsl:merge-source.</summary>
        Document,

        /// <summary>unparsed-text(), unparsed-text-lines(), unparsed-text-available(), json-doc().</summary>
        Text,

        /// <summary>collection(), uri-collection() and each member they read.</summary>
        Collection,

        /// <summary>xsl:include, xsl:import, the stylesheet-location of fn:transform.</summary>
        StylesheetModule,

        /// <summary>load-xquery-module(), XQuery import module.</summary>
        QueryModule,

        /// <summary>An external DTD subset or external entity met while parsing XML.</summary>
        ExternalEntity,
    }
}

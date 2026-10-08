////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Lib;

namespace OutSmart.DAXon.Core
{
    internal interface IPlatform
    {
        void Initialize(Configuration config);
        bool IsDotNet();
        string PlatformSuffix { get; }
        IIDynamicLoader DefaultDynamicLoader { get; }
        string GetDefaultLanguage();
        string DefaultCountry { get; }
        System.IO.Stream LocateResource(string filename, IList<string> messages);


        IStringCollator MakeCollation(Configuration config, Properties props, string uri);
        bool CanReturnCollationKeys(IStringCollator collation);
        IAtomicMatchKey GetCollationKey(SimpleCollation namedCollation, string value);
        IStringCollator MakeUcaCollator(string uri, Configuration config);
        IRegularExpression CompileRegularExpression(Configuration config, UnicodeString regex, string flags, string hostLanguage, IList<string> warnings);
        ExternalObjectType GetExternalObjectType(Configuration config, NamespaceUri uri, string localName);
        bool JAXPStaticContextCheck(RetainedStaticContext retainedStaticContext, IStaticContext sc);
        IModuleURIResolver MakeStandardModuleURIResolver(Configuration config);
    }
}

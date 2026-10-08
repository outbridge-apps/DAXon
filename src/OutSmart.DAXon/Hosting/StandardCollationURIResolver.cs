////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardCollationURIResolver : ICollationURIResolver
    {
        private static readonly StandardCollationURIResolver theInstance = new StandardCollationURIResolver();
        public StandardCollationURIResolver()
        {
        }

        public static StandardCollationURIResolver GetInstance()
        {
            return theInstance;
        }

        private static URI ParseCollationUri(string uri)
        {
            try
            {
                return new URI(uri);
            }
            catch (URISyntaxException err)
            {
                throw new XPathException(err?.Message);
            }
        }

        // kw=val pairs of a collation-URI query: strips a leading '?' if present, splits on
        // ';'/'&', skips malformed params. A null query (URI with a bare trailing '?': RawQuery
        // is null then) yields nothing — Java's getRawQuery() returns "" there, and the empty
        // props must produce the default collation, not a crash.
        private static IEnumerable<KeyValuePair<string, string>> QueryParams(string query)
        {
            if (query == null)
            {
                yield break;
            }

            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                query = query.Substring(1);
            }

            foreach (string param in query.Split(new[] { ';', '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = param.IndexOf('=');
                if (eq > 0 && eq < param.Length - 1)
                {
                    yield return new KeyValuePair<string, string>(param.Substring(0, eq), param.Substring(eq + 1));
                }
            }
        }

        public IStringCollator Resolve(string uri, Configuration config)
        {
            if (uri.Equals("http://saxon.sf.net/collation"))
            {
                return Core.Version.platform.MakeCollation(config, new Properties(), uri);
            }
            else if (uri.StartsWith("http://saxon.sf.net/collation?", StringComparison.Ordinal))
            {
                URI uuri = ParseCollationUri(uri);
                Properties props = new Properties();
                foreach (KeyValuePair<string, string> p in QueryParams(uuri.RawQuery))
                {
                    props.SetProperty(p.Key, AnyURIValue.Decode(p.Value));
                }

                return Core.Version.platform.MakeCollation(config, props, uri);
            }
            else if (uri.StartsWith("http://www.w3.org/2013/collation/UCA", StringComparison.Ordinal))
            {
                IStringCollator uca = Core.Version.platform.MakeUcaCollator(uri, config);
                if (uca != null)
                {
                    return uca;
                }

                // No exact UCA collator on this platform (MakeUcaCollator is a stub). `fallback=no` demands the
                // exact UCA collation with no substitution, which we cannot guarantee — signal FOCH0002 (the
                // spec's "collation not supported" error, which fn:compare/collation-key surface and the
                // fallback=no tests expect). Without fallback=no we may substitute the closest CompareInfo
                // locale/strength approximation below.
                if (uri.Contains("fallback=no"))
                {
                    throw new XPathException("The UCA collation with fallback=no is not supported (no exact Unicode Collation Algorithm implementation)", "FOCH0002");
                }

                URI uuri = ParseCollationUri(uri);
                Properties props = new Properties();

                // Unlike the saxon: branch (per-value decode), the whole query is decoded up
                // front — UCA keyword VALUES are compared against plain literals below.
                foreach (KeyValuePair<string, string> p in QueryParams(AnyURIValue.Decode(uuri.RawQuery)))
                {
                    string kw = p.Key;
                    string val = p.Value;
                    if (kw.Equals("fallback")) // always satisfied: we provide the CompareInfo collation
                    {
                        continue;
                    }
                    // Fallback is LAX (F&O §5.3.3): an unrecognized value for a known keyword is ignored,
                    // not an error — the strict validation in MakeCollation/CaseFirstCollator is for
                    // saxon:-URI collations only (UCA-collation-012/017/019).
                    switch (kw)
                    {
                        case "strength":
                            switch (val)
                            {
                                case "1": val = "primary"; break;
                                case "2": val = "secondary"; break;
                                case "3": val = "tertiary"; break;
                                case "quaternary":
                                case "4":
                                case "5": val = "identical"; break;
                            }

                            if (val != "primary" && val != "secondary" && val != "tertiary" && val != "identical")
                            {
                                continue;
                            }
                            break;
                        case "caseFirst":
                            if (val != "upper" && val != "lower")
                            {
                                continue;
                            }
                            kw = "case-order";
                            val += "-first";
                            break;
                        case "numeric":
                            if (val != "yes" && val != "no")
                            {
                                continue;
                            }
                            kw = "alphanumeric";
                            break;
                    }

                    props.SetProperty(kw, val);
                }

                return Core.Version.platform.MakeCollation(config, props, uri);
            }
            else
            {
                return null;
            }
        }
    }
}
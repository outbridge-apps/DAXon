////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// A set of query parameters on a URI passed to the collection() or document() function
    /// </summary>
    public class URIQueryParameters
    {
        public const int ON_ERROR_FAIL = 1;
        public const int ON_ERROR_WARNING = 2;
        public const int ON_ERROR_IGNORE = 3;
        Func<string, string, bool> filter;
        bool? recurse;
        int? validation;
        ISpaceStrippingRule strippingRule;
        int? onError;
        bool? xinclude;
        bool? stable;
        bool? metadata;
        string contentType;

        public virtual ISpaceStrippingRule SpaceStrippingRule => strippingRule;

        /// <summary>
        /// Get the file name filter (select=pattern), or absent if unspecified
        /// </summary>
        public virtual Func<string, string, bool> FilenameFilter => filter;

        /// <summary>
        /// Get the value of the recurse=yes|no parameter, or absent if unspecified
        /// </summary>
        public virtual bool? Recurse => recurse;

        /// <summary>
        /// Get the value of the on-error=fail|warning|ignore parameter, or absent if unspecified
        /// </summary>
        public virtual int? OnError => onError;

        /// <summary>
        /// Get the value of xinclude=yes|no, or absent if unspecified
        /// </summary>
        public virtual bool? XInclude => xinclude;

        /// <summary>
        /// Get the value of metadata=yes|no, or absent if unspecified
        /// </summary>
        public virtual bool? MetaData => metadata;

        /// <summary>
        /// Get the value of media-type, or absent if absent
        /// </summary>
        public virtual string ContentType => contentType;

        /// <summary>
        /// Get the value of stable=yes|no, or absent if unspecified
        /// </summary>
        public virtual bool? Stable => stable;
        public URIQueryParameters(string query, Configuration config)
        {
            if (query != null)
            {
                query = Decoded(query);
                foreach (string tok in query.Split(new[] { ';', '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = tok.IndexOf('=');
                    if (eq > 0 && eq < (tok.Length - 1))
                    {
                        string keyword = tok.Substring(0, eq);
                        string value = tok.Substring(eq + 1);
                        ProcessParameter(config, keyword, value);
                    }
                }
            }
        }

        // The query with its %-escapes decoded (UTF-8), as Java's URI.getQuery() gives it: select=*.x%6Dl is *.xml. A %
        // not followed by two hex digits is FODC0004, as Java refuses the URI.
        private static string Decoded(string query)
        {
            if (query.IndexOf('%') < 0)
            {
                return query;
            }

            var text = new StringBuilder(query.Length);
            var bytes = new List<byte>();
            for (int i = 0; i < query.Length; i++)
            {
                if (query[i] == '%')
                {
                    if (i + 2 >= query.Length || !Uri.IsHexDigit(query[i + 1]) || !Uri.IsHexDigit(query[i + 2]))
                    {
                        throw new XPathException("Invalid %-escape in the query of a collection URI: " + Err.Wrap(query), "FODC0004");
                    }

                    bytes.Add((byte)(Uri.FromHex(query[i + 1]) * 16 + Uri.FromHex(query[i + 2])));
                    i += 2;
                    continue;
                }

                if (bytes.Count > 0)
                {
                    text.Append(Encoding.UTF8.GetString(bytes.ToArray()));
                    bytes.Clear();
                }

                text.Append(query[i]);
            }

            return text.Append(Encoding.UTF8.GetString(bytes.ToArray())).ToString();
        }

        private void ProcessParameter(Configuration config, string keyword, string value)
        {
            if (keyword.Equals("select"))
            {
                filter = (MakeGlobFilter(value));
            }
            else if (keyword.Equals("match"))
            {
                ARegularExpression regex = new ARegularExpression(StringView.Of(value), "", "XP", new List<string>(), config);
                filter = ((Func<string, string, bool>)(new RegexFilter(regex.Matches).Accept));
            }
            else if (keyword.Equals("recurse"))
            {
                recurse = (value == "yes");
            }
            else if (keyword.Equals("validation"))
            {
                int v = Validation.GetCode(value);
                if (v != Validation.INVALID)
                {
                    validation = (v);
                }
            }
            else if (keyword.Equals("strip-space"))
            {
                switch (value)
                {
                    case "yes":
                        strippingRule = AllElementsSpaceStrippingRule.GetInstance();
                        break;
                    case "ignorable":
                        strippingRule = IgnorableSpaceStrippingRule.GetInstance();
                        break;
                    case "no":
                        strippingRule = NoElementsSpaceStrippingRule.GetInstance();
                        break;
                }
            }
            else if (keyword.Equals("stable"))
            {
                if (value.Equals("yes"))
                {
                    stable = (true);
                }
                else if (value.Equals("no"))
                {
                    stable = (false);
                }
            }
            else if (keyword.Equals("metadata"))
            {
                if (value.Equals("yes"))
                {
                    metadata = (true);
                }
                else if (value.Equals("no"))
                {
                    metadata = (false);
                }
            }
            else if (keyword.Equals("xinclude"))
            {
                if (value.Equals("yes"))
                {
                    CheckXIncludeIsSupported();
                    xinclude = (true);
                }
                else if (value.Equals("no"))
                {
                    xinclude = (false);
                }
            }
            else if (keyword.Equals("content-type"))
            {
                contentType = (value);
            }
            else if (keyword.Equals("on-error"))
            {
                switch (value)
                {
                    case "warning":
                        onError = (ON_ERROR_WARNING);
                        break;
                    case "ignore":
                        onError = (ON_ERROR_IGNORE);
                        break;
                    case "fail":
                        onError = (ON_ERROR_FAIL);
                        break;
                }
            }
        }

        // Whether the query of a URI says xinclude=yes.
        internal static bool AsksForXInclude(string uri)
        {
            int mark = uri.IndexOf('?');
            if (mark < 0)
            {
                return false;
            }

            int end = uri.IndexOf('#', mark);
            string query = end < 0 ? uri.Substring(mark + 1) : uri.Substring(mark + 1, end - mark - 1);
            return System.Array.IndexOf(query.Split(';', '&'), "xinclude=yes") >= 0;
        }

        private static void CheckXIncludeIsSupported()
        {
            // FODC0002: the resource the URI asks for - the document with its inclusions made - cannot be had
            throw new XPathException("XInclude is not supported on this platform", "FODC0002");
        }

        public static Func<string, string, bool> MakeGlobFilter(string value)
        {
            UnicodeBuilder sb = new UnicodeBuilder();
            sb.Append('^');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '.')
                {

                    // replace "." with "\."
                    sb.AppendLatin("\\.");
                }
                else if (c == '*')
                {

                    // replace "*" with ".*"
                    sb.AppendLatin(".*");
                }
                else if (c == '?')
                {

                    // replace "?" with ".?"
                    sb.AppendLatin(".?");
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append('$');
            try
            {
                return new RegexFilter(new JavaRegularExpression(sb.ToUnicodeString(), "").Matches).Accept;
            }
            catch (XPathException)
            {
                throw new XPathException("Invalid glob " + value + " in collection URI", "FODC0004");
            }
        }

        /// <summary>
        /// Get the value of the validation=strict|lax|preserve|strip parameter, or absent if unspecified
        /// </summary>
        public virtual int? GetValidationMode()
        {
            return validation;
        }

        /// <summary>
        /// Create ParseOptions based on these query parameters
        /// </summary>
        public virtual ParseOptions MakeParseOptions(Configuration config)
        {
            ParseOptions options = new ParseOptions();
            ISpaceStrippingRule stripSpace = SpaceStrippingRule;
            if (stripSpace != null)
            {
                options = options.WithSpaceStrippingRule(stripSpace);
            }

            int? validation = GetValidationMode();
            if (validation.HasValue)
            {
                options = options.WithSchemaValidationMode(validation.Value);
            }

            bool? xinclude = XInclude;
            if (xinclude.HasValue)
            {
                options = options.WithXIncludeAware(xinclude.Value);
            }

            return options;
        }

        /// <summary>
        /// A FilenameFilter that tests file names against a regular expression
        /// </summary>
        internal sealed class RegexFilter
        {
            private readonly Func<UnicodeString, bool> matches;
            public RegexFilter(Func<UnicodeString, bool> matches)
            {
                this.matches = matches;
            }

            // dir is null for an archive entry: there is no directory on disk to look at
            public bool Accept(string dir, string name)
            {
                return (dir != null && Directory.Exists(Path.Combine(dir, name))) || matches(StringView.Of(name));
            }
        }
    }
}

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
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Internal;
using System.IO;
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
        Func<string, string, bool> filter = null;
        bool? recurse = null;
        int? validation = null;
        ISpaceStrippingRule strippingRule = null;
        int? onError = null;
        bool? xinclude = null;
        bool? stable = null;
        bool? metadata = null;
        string contentType = null;

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

        private void ProcessParameter(Configuration config, string keyword, string value)
        {
            if (keyword.Equals("select"))
            {
                filter = (MakeGlobFilter(value));
            }
            else if (keyword.Equals("match"))
            {
                ARegularExpression regex = new ARegularExpression(StringView.Of(value).Tidy(), "", "XP", new List<string>(), config);
                filter = ((Func<string, string, bool>)(new RegexFilter(regex).Accept));
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

        private static void CheckXIncludeIsSupported()
        {
            throw new XPathException("XInclude is not supported on this platform");
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
                return new RegexFilter(new JavaRegularExpression(sb.ToUnicodeString(), "")).Accept;
            }
            catch (XPathException e)
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
            private readonly IRegularExpression pattern;
            public RegexFilter(IRegularExpression regex)
            {
                this.pattern = regex;
            }

            // dir is null for an archive entry: there is no directory on disk to look at
            public bool Accept(string dir, string name)
            {
                return (dir != null && Directory.Exists(Path.Combine(dir, name))) || pattern.Matches(StringView.Of(name).Tidy());
            }
        }
    }
}

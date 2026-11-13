////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.Numerics;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Values.Maps;

namespace OutSmart.DAXon.Functions.Csv
{
    // The options of parse-csv, csv-to-xml and csv-doc (XPath 4.0 draft, 2026-10).
    internal sealed class CsvOptions
    {
        internal static readonly OptionsParameter Details = MakeDetails();

        internal string Separator = ",";
        internal string Quote = "\"";
        internal string CommentMarker;
        internal bool TrimWhitespace;
        internal bool HeaderFromData;
        internal List<string> HeaderNames;      // the names the caller supplied, or null
        internal int[] SelectColumns;           // 1-based; an index past int is 0, which selects nothing; null for all
        internal bool TrimRows;

        private static OptionsParameter MakeDetails()
        {
            var details = new OptionsParameter();
            details.AddAllowedOption("separator", SequenceType.SINGLE_STRING, StringValue.Bmp(","));
            details.AddAllowedOption("quote-character", SequenceType.SINGLE_STRING, StringValue.Bmp("\""));
            details.AddAllowedOption("comment-marker", SequenceType.OPTIONAL_STRING, null);
            details.AddAllowedOption("trim-whitespace", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            details.AddAllowedOption("header", SequenceType.ATOMIC_SEQUENCE, BooleanValue.FALSE);
            details.AddAllowedOption("select-columns", SequenceType.INTEGER_SEQUENCE, null);
            details.AddAllowedOption("trim-rows", SequenceType.SINGLE_BOOLEAN, BooleanValue.FALSE);
            return details;
        }

        // The options argument when there is one (an empty one takes the defaults), checked.
        internal static CsvOptions Of(OptionsParameter details, ISequence[] arguments, IXPathContext context)
        {
            var options = new CsvOptions();
            MapItem supplied = arguments.Length > 1 ? (MapItem)arguments[1].Head() : null;
            if (supplied == null)
            {
                return options;
            }

            Dictionary<string, IGroundedValue> values = details.ProcessSuppliedOptions(supplied, context);
            options.Separator = Character(values, "separator");
            options.Quote = Character(values, "quote-character");
            options.CommentMarker = values.TryGetValue("comment-marker", out IGroundedValue marker) && marker.Head() != null ? Character(values, "comment-marker") : null;
            if (options.Separator == options.Quote || options.Separator == options.CommentMarker || options.Quote == options.CommentMarker)
            {
                throw new XPathException("The separator, the quote character and the comment marker must differ", "FOCV0002");
            }

            options.TrimWhitespace = ((BooleanValue)values["trim-whitespace"]).GetBooleanValue();
            options.TrimRows = ((BooleanValue)values["trim-rows"]).GetBooleanValue();
            ReadHeader(values["header"], options);
            if (values.TryGetValue("select-columns", out IGroundedValue select) && select.GetLength() > 0)
            {
                var columns = new List<int>();
                ISequenceIterator it = select.Iterate();
                for (IItem item; (item = it.Next()) != null;)
                {
                    BigInteger index = ((IntegerValue)item).AsBigInteger();
                    columns.Add(index >= 1 && index <= int.MaxValue ? (int)index : 0);
                    OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(columns.Count);
                }

                options.SelectColumns = columns.ToArray();
            }

            return options;
        }

        // One character: one code point, and not a newline (a CR reads as one).
        private static string Character(Dictionary<string, IGroundedValue> values, string name)
        {
            string c = values[name].Head().GetStringValue();
            bool one = c.Length == 1 ? !char.IsSurrogate(c[0]) : c.Length == 2 && char.IsSurrogatePair(c[0], c[1]);
            if (!one || c == "\n" || c == "\r")
            {
                throw new XPathException("The CSV option " + name + " must be one character other than a newline, not " + Err.Wrap(c), "FOCV0002");
            }

            return c;
        }

        // true, false, or the column names: one boolean, or strings.
        private static void ReadHeader(IGroundedValue header, CsvOptions options)
        {
            IItem first = header.Head();
            if (first is BooleanValue b && header.GetLength() == 1)
            {
                options.HeaderFromData = b.GetBooleanValue();
                return;
            }

            options.HeaderNames = new List<string>();
            ISequenceIterator it = header.Iterate();
            for (IItem item; (item = it.Next()) != null;)
            {
                if (!(item is StringValue))
                {
                    throw new XPathException("The CSV option header must be one boolean or a sequence of strings", "XPTY0004");
                }

                options.HeaderNames.Add(item.GetStringValue());
            }
        }
    }
}

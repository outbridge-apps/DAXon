////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Numerics;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;

namespace OutSmart.DAXon.Functions.Csv
{
    // The rows of a CSV text as parse-csv shapes them: the header taken out, select-columns and trim-rows applied.
    internal static class CsvRows
    {
        // onColumns gets the column names once, before the first row; onRow each data row (the list is reused).
        internal static void Read(string text, CsvOptions options, Action<List<string>> onColumns, Action<List<string>> onRow)
        {
            var reader = new CsvReader(text, options);
            var fields = new List<string>();
            var shaped = new List<string>();
            bool first = true;
            int width = 0;
            if (!options.HeaderFromData)
            {
                onColumns(options.HeaderNames ?? new List<string>());
            }

            while (reader.Next(fields))
            {
                if (first)
                {
                    first = false;
                    width = fields.Count;
                    if (options.HeaderFromData)
                    {
                        for (int i = 0; i < fields.Count; i++)
                        {
                            fields[i] = Whitespace.Trim(fields[i]);
                        }

                        onColumns(Shape(fields, options, width, shaped));
                        continue;
                    }
                }

                onRow(Shape(fields, options, width, shaped));
            }

            if (first && options.HeaderFromData)
            {
                onColumns(new List<string>());
            }
        }

        private static List<string> Shape(List<string> fields, CsvOptions options, int width, List<string> shaped)
        {
            if (options.SelectColumns != null)
            {
                shaped.Clear();
                foreach (int c in options.SelectColumns)
                {
                    shaped.Add(c >= 1 && c <= fields.Count ? fields[c - 1] : "");
                }

                return shaped;
            }

            if (options.TrimRows)
            {
                if (fields.Count > width)
                {
                    fields.RemoveRange(width, fields.Count - width);
                }

                while (fields.Count < width)
                {
                    fields.Add("");
                }
            }

            return fields;
        }

        // The first position of each non-empty name.
        internal static Dictionary<string, int> Index(List<string> columns)
        {
            var index = new Dictionary<string, int>();
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].Length > 0 && !index.ContainsKey(columns[i]))
                {
                    index[columns[i]] = i + 1;
                }
            }

            return index;
        }
    }

    // daxon:parse-csv: a record of the column names, their index, the rows as arrays of strings and a get function.
    internal sealed class ParseCsvFn : SystemFunction
    {
        private static readonly IFunctionItemType GetType2 = new SpecificFunctionType(
            new Values.SequenceType[] { Values.SequenceType.SINGLE_INTEGER, Values.SequenceType.SINGLE_ATOMIC }, Values.SequenceType.SINGLE_STRING);

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem value = arguments[0].Head();
            return value == null ? EmptySequence.GetInstance() : Parse(value.GetStringValue(), CsvOptions.Of(Details.optionDetails, arguments, context));
        }

        internal static ISequence Parse(string text, CsvOptions options)
        {
            List<string> columns = null;
            var rows = new List<IItem>();
            CsvRows.Read(text, options, names => columns = new List<string>(names), fields =>
            {
                var members = new List<IGroundedValue>(fields.Count);
                foreach (string field in fields)
                {
                    members.Add(StringValue.MakeStringValue(field));
                }

                rows.Add(new SimpleArrayItem(members));
                if (rows.Count == MemoryLedger.BigCount)
                {
                    MemoryLedger.Hold(rows, MemoryLedger.ItemListSizer);
                }
            });
            Dictionary<string, int> index = CsvRows.Index(columns);
            var names = new List<IItem>(columns.Count);
            foreach (string name in columns)
            {
                names.Add(StringValue.MakeStringValue(name));
            }

            var columnIndex = new DictionaryMap();
            foreach (KeyValuePair<string, int> entry in index)
            {
                columnIndex.InitialPut(entry.Key, Int64Value.MakeIntegerValue(entry.Value));
            }

            var record = new DictionaryMap();
            record.InitialPut("columns", SequenceExtent.MakeSequenceExtent(names));
            record.InitialPut("column-index", columnIndex);
            record.InitialPut("rows", SequenceExtent.MakeSequenceExtent(rows));
            record.InitialPut("get", new CallableFunction(2, new Get(rows, index), GetType2));
            return record;
        }

        // get($row, $column): a field by row number and column number or name; "" outside the table.
        private sealed class Get : ICallable
        {
            private readonly List<IItem> rows;
            private readonly Dictionary<string, int> index;

            internal Get(List<IItem> rows, Dictionary<string, int> index)
            {
                this.rows = rows;
                this.index = index;
            }

            public ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                int r = Position(((IntegerValue)arguments[0].Head()).AsBigInteger());
                IItem c = arguments[1].Head();
                int column;
                if (c is IntegerValue ci)
                {
                    column = Position(ci.AsBigInteger());
                }
                else if (c is StringValue name)
                {
                    if (!index.TryGetValue(name.GetStringValue(), out column))
                    {
                        throw new XPathException("No CSV column is named " + Err.Wrap(name.GetStringValue()), "FOCV0004");
                    }
                }
                else
                {
                    throw new XPathException("A CSV column is a number or a name, not " + Types.Type.DisplayTypeName(c), "XPTY0004").AsTypeError();
                }

                if (r < 1 || r > rows.Count)
                {
                    return StringValue.EMPTY_STRING;
                }

                var row = (ArrayItem)rows[r - 1];
                return column >= 1 && column <= row.ArrayLength() ? row.Get(column - 1) : StringValue.EMPTY_STRING;
            }

            private static int Position(BigInteger n)
            {
                return n >= 1 && n <= int.MaxValue ? (int)n : 0;
            }
        }
    }

    // daxon:csv-to-xml: the rows as fn:csv/fn:rows/fn:row/fn:field elements, written as they are read.
    internal sealed class CsvToXmlFn : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem value = arguments[0].Head();
            if (value == null)
            {
                return EmptySequence.GetInstance();
            }

            CsvOptions options = CsvOptions.Of(Details.optionDetails, arguments, context);
            Builder builder = context.GetController().MakeBuilder();
            builder.SetSystemId(StaticBaseUriString);
            builder.SetTiming(false);
            builder.SetDurability(Durability.TEMPORARY);
            var @out = new ComplexContentOutputter(builder);
            NamePool pool = context.GetConfiguration().GetNamePool();
            FingerprintedQName fieldName = Name("field", pool);
            FingerprintedQName rowName = Name("row", pool);
            var columnAttribute = new FingerprintedQName("", NamespaceUri.NULL, "column");
            columnAttribute.ObtainFingerprint(pool);
            List<string> names = null;
            @out.Open();
            @out.StartDocument(ReceiverOption.NONE);
            Start(@out, Name("csv", pool));
            CsvRows.Read(value.GetStringValue(), options, columns =>
            {
                names = new List<string>(columns);
                int last = names.FindLastIndex(n => n.Length > 0);
                if (last >= 0)
                {
                    Start(@out, Name("columns", pool));
                    FingerprintedQName columnName = Name("column", pool);
                    for (int i = 0; i <= last; i++)
                    {
                        Start(@out, columnName);
                        Text(@out, names[i]);
                        @out.EndElement();
                    }

                    @out.EndElement();
                }

                Start(@out, Name("rows", pool));
            }, fields =>
            {
                Start(@out, rowName);
                for (int i = 0; i < fields.Count; i++)
                {
                    @out.StartElement(fieldName, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
                    if (i < names.Count && names[i].Length > 0)
                    {
                        @out.Attribute(columnAttribute, BuiltInAtomicType.UNTYPED_ATOMIC, names[i], Loc.NONE, ReceiverOption.NONE);
                    }

                    @out.StartContent();
                    Text(@out, fields[i]);
                    @out.EndElement();
                }

                @out.EndElement();
            });
            @out.EndElement();
            @out.EndElement();
            @out.EndDocument();
            @out.Close();
            return builder.CurrentRoot;
        }

        private static FingerprintedQName Name(string local, NamePool pool)
        {
            var name = new FingerprintedQName("", NamespaceUri.FN, local);
            name.ObtainFingerprint(pool);
            return name;
        }

        private static void Start(Outputter @out, FingerprintedQName name)
        {
            @out.StartElement(name, Untyped.INSTANCE, Loc.NONE, ReceiverOption.NONE);
            @out.StartContent();
        }

        private static void Text(Outputter @out, string text)
        {
            if (text.Length > 0)
            {
                @out.Characters(StringView.Of(text), Loc.NONE, ReceiverOption.NONE);
            }
        }
    }

    // daxon:csv-doc: unparsed-text($href) => daxon:parse-csv($options).
    internal sealed class CsvDocFn : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            var href = (StringValue)arguments[0].Head();
            if (href == null)
            {
                return EmptySequence.GetInstance();
            }

            CsvOptions options = CsvOptions.Of(Details.optionDetails, arguments, context);
            StringValue text;
            try
            {
                text = UnparsedText.EvalUnparsedText(href, StaticBaseUriString, null, context);
            }
            catch (XPathException e)
            {
                e.MaybeSetErrorCode("FOUT1170");
                throw;
            }

            return ParseCsvFn.Parse(text.GetStringValue(), options);
        }
    }
}

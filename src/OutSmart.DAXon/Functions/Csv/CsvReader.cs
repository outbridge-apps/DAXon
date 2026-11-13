////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions.Csv
{
    // Rows of a CSV text by the rules of fn:parse-csv (XPath 4.0 draft, 2026-10): RFC 4180 with CR and CRLF read as LF
    // (in quoted fields too), any one character as separator, quote and comment marker, a quote only opening a field.
    internal sealed class CsvReader
    {
        private readonly string text;
        private readonly int length;
        private readonly string separator;
        private readonly string quote;
        private readonly string commentMarker;
        private readonly bool trimWhitespace;
        private readonly StringBuilder quoted = new StringBuilder();
        private int pos;
        private int row;
        private bool heldFields;

        internal CsvReader(string text, CsvOptions options)
        {
            this.text = text;
            length = text.Length;
            separator = options.Separator;
            quote = options.Quote;
            commentMarker = options.CommentMarker;
            trimWhitespace = options.TrimWhitespace;
        }

        // The next row into fields, false at the end of the text. A blank row leaves fields empty.
        internal bool Next(List<string> fields)
        {
            fields.Clear();
            while (pos < length && commentMarker != null && At(pos, commentMarker))
            {
                SkipLine();
            }

            if (pos >= length)
            {
                return false;
            }

            row++;
            bool anyQuoted = false;
            while (true)
            {
                bool wasQuoted = pos < length && At(pos, quote);
                string field = wasQuoted ? ReadQuoted() : ReadPlain();
                anyQuoted |= wasQuoted;
                fields.Add(field);
                if (fields.Count >= MemoryLedger.BigCount)
                {
                    HoldWide(fields);
                }

                bool blank = !anyQuoted && fields.Count == 1 && field.Length == 0;
                if (pos >= length)
                {
                    // An empty last row with no newline after it is no row.
                    if (blank)
                    {
                        fields.Clear();
                        return false;
                    }

                    return true;
                }

                if (At(pos, separator))
                {
                    pos += separator.Length;
                    continue;
                }

                SkipNewline();
                if (blank)
                {
                    fields.Clear();
                }

                return true;
            }
        }

        // A wide row is held in the ledger as it grows and checked once in 64K fields: empty ones read no characters.
        private void HoldWide(List<string> fields)
        {
            if (!heldFields)
            {
                heldFields = true;
                MemoryLedger.Hold(fields, FieldsSizer);
            }

            if ((fields.Count & 0xFFFF) == 0)
            {
                Controller.CheckActiveTimeoutNow();
            }
        }

        private static readonly System.Func<object, long> FieldsSizer = o => 24 + 40L * ((List<string>)o).Count;

        // An unquoted field: up to a separator or a newline, the quote character taken as it stands.
        private string ReadPlain()
        {
            int start = pos;
            char first = separator[0];
            while (pos < length)
            {
                char c = text[pos];
                if (c == '\n' || c == '\r' || (c == first && At(pos, separator)))
                {
                    break;
                }

                Controller.CheckActiveTimeoutEvery64K(pos);
                pos++;
            }

            string field = text.Substring(start, pos - start);
            return trimWhitespace ? Whitespace.Trim(field) : field;
        }

        // A quoted field: a doubled quote stands for one, and the closing quote ends the field.
        private string ReadQuoted()
        {
            pos += quote.Length;
            quoted.Clear();
            int run = pos;
            while (true)
            {
                if (pos >= length)
                {
                    throw Error("a quoted field has no closing quote");
                }

                char c = text[pos];
                if (c == quote[0] && At(pos, quote))
                {
                    quoted.Append(text, run, pos - run);
                    pos += quote.Length;
                    if (pos < length && At(pos, quote))
                    {
                        quoted.Append(quote);
                        pos += quote.Length;
                        run = pos;
                        continue;
                    }

                    break;
                }

                if (c == '\r')
                {
                    quoted.Append(text, run, pos - run).Append('\n');
                    pos += pos + 1 < length && text[pos + 1] == '\n' ? 2 : 1;
                    run = pos;
                    continue;
                }

                Controller.CheckActiveTimeoutEvery64K(pos);
                pos++;
            }

            if (pos < length && text[pos] != '\n' && text[pos] != '\r' && !At(pos, separator))
            {
                throw Error("a quoted field is followed by " + Err.Wrap(text.Substring(pos, char.IsHighSurrogate(text[pos]) && pos + 1 < length ? 2 : 1)) + ", not a separator or a newline");
            }

            return quoted.ToString();
        }

        // A comment row: through the next newline, quoted or not.
        private void SkipLine()
        {
            while (pos < length && text[pos] != '\n' && text[pos] != '\r')
            {
                Controller.CheckActiveTimeoutEvery64K(pos);
                pos++;
            }

            if (pos < length)
            {
                SkipNewline();
            }
        }

        private void SkipNewline()
        {
            pos += text[pos] == '\r' && pos + 1 < length && text[pos + 1] == '\n' ? 2 : 1;
        }

        private bool At(int at, string token)
        {
            return text[at] == token[0] && (token.Length == 1 || (at + 1 < length && text[at + 1] == token[1]));
        }

        private XPathException Error(string what)
        {
            return new XPathException("Invalid CSV in row " + row + ": " + what, "FOCV0001");
        }
    }
}

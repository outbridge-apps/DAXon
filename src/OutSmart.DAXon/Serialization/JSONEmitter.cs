////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Json;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    public class JSONEmitter
    {
        //private final ExpandedStreamResult result;
        private Configuration config;
        private IUnicodeWriter writer;
        private bool normalize;
        private NormalizationForm normalizationForm;
        private CharacterMap characterMap;
        private Properties outputProperties;
        private ICharacterSet characterSet;
        private bool isIndenting;
        private int indentSpaces = 3;
        private int maxLineLength;
        private bool first = true;
        private bool afterKey = false;
        private int level;
        private readonly Stack<bool> oneLinerStack = new Stack<bool>();
        private bool mustClose = true;
        private bool escapeSolidus = true;
        private bool unfailing = false;
        // ASCII escape decisions folded into one table (structural JSON escapes + the hex-escape
        // predicate of SimpleEscape): one bounds-checked lookup per char on the clean scan instead
        // of a delegate plus an ICharacterSet call. Chars >= 128 keep the generic path.
        private bool[] asciiDirty;

        public virtual Properties OutputProperties
        {
            get => outputProperties; set
            {
                this.outputProperties = value;
                if (value.GetProperty(DAXonOutputKeys.INDENT) == "yes")
                {
                    isIndenting = true;
                }

                if (value.GetProperty(DAXonOutputKeys.UNFAILING) == "yes")
                {
                    unfailing = true;
                }

                if (value.GetProperty(DAXonOutputKeys.ESCAPE_SOLIDUS) == "no")
                {
                    escapeSolidus = false;
                }

                string max = value.GetProperty(DAXonOutputKeys.LINE_LENGTH);
                if (max != null)
                {
                    try
                    {
                        maxLineLength = int.Parse(max, CultureInfo.InvariantCulture);
                    }
                    catch (Exception err) when (err is FormatException || err is OverflowException)
                    {
                    }
                }

                string spaces = value.GetProperty(DAXonOutputKeys.INDENT_SPACES);
                if (spaces != null)
                {
                    try
                    {
                        indentSpaces = int.Parse(spaces, CultureInfo.InvariantCulture);
                    }
                    catch (Exception err) when (err is FormatException || err is OverflowException)
                    {
                    }
                }

                string encoding = value.GetProperty(DAXonOutputKeys.ENCODING);
                try
                {
                    characterSet = config.GetCharacterSetFactory().GetCharacterSet(encoding);
                }
                catch (XPathException e)
                {
                    characterSet = UTF8CharacterSet.GetInstance();
                }

                asciiDirty = null;   // charset/solidus may have changed; rebuild on next use
            }
        }
        public JSONEmitter(PipelineConfiguration pipe, IUnicodeWriter writer, Properties outputProperties)
        {
            config = pipe.GetConfiguration();
            OutputProperties = outputProperties;
            this.writer = writer;
        }

        public virtual void SetMustClose(bool mustClose)
        {
            this.mustClose = mustClose;
        }

        public virtual void SetNormalizationForm(NormalizationForm form)
        {
            this.normalize = true;
            this.normalizationForm = form;
        }

        public virtual void SetCharacterMap(CharacterMap map)
        {
            this.characterMap = map;
        }

        public virtual void WriteKey(string key)
        {
            bool oneLiner = oneLinerStack.Peek();
            ConditionalComma(false);
            Emit('"');
            Emit(Escape(key));
            Emit("\":");
            if (isIndenting && !oneLiner)
            {
                Emit(" ");
            }

            afterKey = true;
        }

        public virtual void WriteAtomicValue(AtomicValue item)
        {
            ConditionalComma(false);
            if (item == null)
            {
                Emit("null");
            }
            else if (item is NumericValue)
            {
                NumericValue num = (NumericValue)item;
                if (item is DecimalValue)
                {

                    // Avoid exponential notation
                    Emit(num.UnicodeStringValue);
                }
                else if (num.IsNaN())
                {
                    if (unfailing)
                    {
                        Emit("NaN");
                    }
                    else
                    {
                        throw new XPathException("JSON has no way of representing NaN", "SERE0020");
                    }
                }
                else if (double.IsInfinity(num.GetDoubleValue()))
                {
                    if (unfailing)
                    {
                        Emit(num.GetDoubleValue() < 0 ? "-INF" : "INF");
                    }
                    else
                    {
                        throw new XPathException("JSON has no way of representing Infinity", "SERE0020");
                    }
                }
                else if (num.IsNegativeZero())
                {
                    Emit("-0");
                }
                else
                {
                    double val = num.GetDoubleValue();
                    double abs = Math.Abs(val);

                    // Avoid exponential notation except in extremis
                    Emit(FloatingPointConverter.ConvertDouble(val, abs >= 1E+18 || abs < 1E-18)); //                if (num.isWholeNumber() && abs < 1e18) {
                    //                    emit(num.longValue() + "");
                    //                } else if (abs < 1e18 && abs > 1e-18) {
                    //                    // Avoid exponential notation except in extremis
                    //                } else {
                    //                }
                }
            }
            else if (item is BooleanValue)
            {
                Emit(item.GetStringValue());
            }
            else
            {
                Emit('"');
                if (!TryEmitClean(item.UnicodeStringValue))
                {
                    Emit(Escape(item.GetStringValue()));
                }

                Emit('"');
            }
        }

        // Emit 8-bit string content (Slice8/Twine8) in place when every byte is escape-clean:
        // no System.String materialization, and the writer takes the bytes as-is.
        private bool TryEmitClean(UnicodeString us)
        {
            if (characterMap != null || normalize)
            {
                return false;
            }

            byte[] b;
            int s, e;
            if (us is Slice8 s8)
            {
                b = s8.ByteArray;
                s = s8.Start;
                e = s8.End;
            }
            else if (us is Twine8 t8)
            {
                b = t8.ByteArray;
                s = 0;
                e = b.Length;
            }
            else if (us is BMPSlice sl)
            {
                // zero-copy token view over its line: scan the char window in place
                string str = sl.Backing;
                int se = sl.End;
                bool[] d = DirtyTable();
                for (int i = sl.Start; i < se; i++)
                {
                    char c = str[i];
                    if (c >= 128 || d[c])
                    {
                        return false;
                    }
                }

                Emit(us);
                return true;
            }
            else
            {
                return false;
            }

            bool[] dirty = DirtyTable();
            for (int i = s; i < e; i++)
            {
                byte c = b[i];
                if (c >= 128 || dirty[c])
                {
                    return false;
                }
            }

            Emit(us);
            return true;
        }

        private bool[] DirtyTable()
        {
            bool[] t = asciiDirty;
            if (t == null)
            {
                t = new bool[128];
                for (int c = 0; c < 128; c++)
                {
                    // Exactly the chars the generic path touches: named escapes and quote/backslash
                    // are < 31 or listed; the hex predicate adds c < 31, DEL, and out-of-charset.
                    t[c] = c < 31 || c == 127 || c == '"' || c == '\\' || (c == '/' && escapeSolidus) || !characterSet.InCharset(c);
                }

                asciiDirty = t;
            }

            return t;
        }

        public virtual void WriteStringValue(string str)
        {
            ConditionalComma(false);
            Emit('"');
            Emit(Escape(str));
            Emit('"');
        }

        public virtual void StartArray(bool oneLiner)
        {
            EmitOpen('[', oneLiner);
            level++;
        }

        public virtual void EndArray()
        {
            EmitClose(']', level--);
        }

        public virtual void StartMap(bool oneLiner)
        {
            EmitOpen('{', oneLiner);
            level++;
        }

        public virtual void EndMap()
        {
            EmitClose('}', level--);
        }

        private void EmitOpen(char bracket, bool oneLiner)
        {
            ConditionalComma(true);
            oneLinerStack.Push(oneLiner);
            Emit(bracket);
            first = true;
            if (isIndenting && oneLiner)
            {
                Emit(' ');
            }
        }

        private void EmitClose(char bracket, int level)
        {
            bool oneLiner = oneLinerStack.Pop();
            if (isIndenting)
            {
                if (oneLiner)
                {
                    Emit(' ');
                }
                else
                {
                    Indent(level - 1);
                }
            }

            Emit(bracket);
            first = false;
        }

        private void ConditionalComma(bool opening)
        {
            bool wasFirst = first;
            bool oneLiner = oneLinerStack.Count > 0 && oneLinerStack.Peek();
            bool actuallyIndenting = isIndenting && level != 0 && !oneLiner;
            if (first)
            {
                first = false;
            }
            else if (!afterKey)
            {
                Emit(',');
                if (oneLiner && isIndenting)
                {
                    Emit(' ');
                }
            }

            if ((wasFirst && afterKey))
            {
                Emit(' ');
            }
            else if (actuallyIndenting && !afterKey)
            {
                Emit('\n');
                for (int i = 0; i < indentSpaces * level; i++)
                {
                    Emit(' ');
                }
            }

            afterKey = false;
        }

        private void Indent(int level)
        {
            Emit('\n');
            for (int i = 0; i < indentSpaces * level; i++)
            {
                Emit(' ');
            }
        }

        private string Escape(string cs)
        {
            if (characterMap == null)
            {
                return SimpleEscape(cs);
            }

            // U+0000 of the data is escaped apart, unless the map has a replacement for it: in what the map
            // returns, U+0000 marks the replacements.
            int nul = characterMap.Map[0] == null ? cs.IndexOf((char)0) : -1;
            if (nul < 0)
            {
                return EscapeMapped(cs);
            }

            StringBuilder @out = new StringBuilder(cs.Length + 16);
            int from = 0;
            while (nul >= 0)
            {
                @out.Append(EscapeMapped(cs.Substring(from, nul - from))).Append(SimpleEscape("\0"));
                from = nul + 1;
                nul = cs.IndexOf((char)0, from);
            }

            return @out.Append(EscapeMapped(cs.Substring(from))).ToString();
        }

        // The character map applied: its replacements as they are, the rest escaped.
        private string EscapeMapped(string cs)
        {
            UnicodeString text = StringView.Of(cs).Tidy();
            UnicodeString mapped = characterMap.IMap(text, true);
            if (mapped == text)
            {
                return SimpleEscape(cs);
            }

            string s = mapped.ToString();
            StringBuilder @out = new StringBuilder(s.Length);
            int prev = 0;
            while (true)
            {
                int start = s.IndexOf((char)0, prev);
                if (start < 0)
                {
                    @out.Append(SimpleEscape(s.Substring(prev)));
                    return @out.ToString();
                }

                @out.Append(SimpleEscape(s.Substring(prev, start - prev)));
                int end = s.IndexOf((char)0, start + 1);
                @out.Append(s, start + 1, end - start - 1);
                prev = end + 1;
            }
        }

        private string SimpleEscape(string cs)
        {
            if (normalize)
            {
                cs = cs.Normalize(normalizationForm);
            }

            // Table-driven clean scan; only dirty strings pay the delegate-per-char path. Past ASCII a
            // charset that encodes everything (UTF-8/16) hex-escapes nothing but the C1 controls.
            bool[] dirty = DirtyTable();
            bool allEncodable = characterSet is UTF8CharacterSet || characterSet is UTF16CharacterSet;
            int n = cs.Length;
            int i = 0;
            while (i < n)
            {
                char c = cs[i];
                if (c < 128 ? dirty[c] : (!allEncodable || c <= 159))
                {
                    break;
                }

                i++;
            }

            if (i == n)
            {
                return cs;
            }

            return JsonReceiver.Escape(cs, false, !escapeSolidus, (c) => c < 31 || (c >= 127 && c <= 159) || !characterSet.InCharset(c));
        }

        private void Emit(string s)
        {
            try
            {
                writer.Write(s);
            }
            catch (IOException e)
            {
                throw new XPathException(e?.Message);
            }
        }

        private void Emit(UnicodeString s)
        {
            try
            {
                writer.Write(s);
            }
            catch (IOException e)
            {
                throw new XPathException(e?.Message);
            }
        }

        private void Emit(char c)
        {
            try
            {
                writer.WriteCodePoint(c);
            }
            catch (IOException e)
            {
                throw new XPathException(e?.Message);
            }
        }

        public virtual void Dispose()
        {
            if (first)
            {
                Emit("null");
            }

            if (writer != null)
            {
                try
                {
                    if (mustClose)
                    {
                        writer.Dispose();
                    }
                    else
                    {
                        writer.Flush();
                    }
                }
                catch (IOException e)
                {
                    throw new XPathException(e?.Message);
                }
            }
        }
    }
}
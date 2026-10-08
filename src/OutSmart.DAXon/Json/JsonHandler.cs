////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Json
{
    /// <summary>
    /// Default handler class for accepting the result from parsing JSON strings
    /// </summary>
    internal class JsonHandler
    {
        private static readonly string REPLACEMENT = "�";
        public bool escape;
        protected IIntPredicateProxy charChecker;
        private IXPathContext context;
        private IFunctionItem fallbackFunction = null;
        // Set by ReEscape: true when the returned string is provably BMP with no substitutions
        // (every char in [0x20..0xD7FF] plus TAB/LF/CR), so a downstream consumer can skip a
        // second surrogate scan. False on the escape path and whenever a char needed handling.
        protected bool reEscapeClean;

        public virtual IXPathContext Context
        {
            get => context; set
            {
                this.context = value;
            }
        }

        public virtual ISequence GetResult()
        {
            return null;
        }

        public virtual bool SetKey(string unEscaped, string reEscaped)
        {
            return false;
        }

        public virtual void StartArray()
        {
        }

        public virtual void EndArray()
        {
        }

        public virtual void StartMap()
        {
        }

        public virtual void EndMap()
        {
        }

        public virtual void WriteNumeric(string asString, AtomicValue parsedValue)
        {
        }

        public virtual void WriteString(string val)
        {
        }

        public virtual string ReEscape(string val)
        {
            string escaped;
            reEscapeClean = false;
            if (escape)
            {
                escaped = JsonReceiver.Escape(val, true, true, (value) => (value >= 0 && value <= 0x1F) || (value >= 0x7F && value <= 0x9F) || !charChecker.Test(value) || (value == 0x5C));
            }
            else
            {
                // Fast path: only chars outside 0x20..0xD7FF (plus TAB/LF/CR) can ever need
                // substitution -- that range is valid in both XML 1.0 and 1.1, so no checker
                // call and no copies for the overwhelmingly common clean string.
                int n = val.Length;
                int i = 0;
                while (i < n)
                {
                    char c = val[i];
                    if ((c >= 0x20 && c < 0xD800) || c == '\t' || c == '\n' || c == '\r')
                    {
                        i++;
                        continue;
                    }

                    break;
                }

                if (i == n)
                {
                    reEscapeClean = true;   // provably BMP, no substitution needed
                    return val;
                }

                StringBuilder buffer = new StringBuilder(val);
                HandleInvalidCharacters(buffer);
                escaped = buffer.ToString();
            }

            return escaped;
        }

        public virtual void WriteBoolean(bool value)
        {
        }

        public virtual void WriteNull()
        {
        }

        protected virtual void HandleInvalidCharacters(StringBuilder buffer)
        {

            //if (checkSurrogates && !liberal) {
            IIntPredicateProxy charChecker = context.GetConfiguration().ValidCharacterChecker;
            for (int i = 0; i < buffer.Length; i++)
            {
                char ch = buffer[i];
                if (UTF16CharacterSet.IsHighSurrogate(ch))
                {
                    if (i + 1 >= buffer.Length || !UTF16CharacterSet.IsLowSurrogate(buffer[i + 1]))
                    {
                        Substitute(buffer, i, 1, context);
                    }
                }
                else if (UTF16CharacterSet.IsLowSurrogate(ch))
                {
                    if (i == 0 || !UTF16CharacterSet.IsHighSurrogate(buffer[i - 1]))
                    {
                        Substitute(buffer, i, 1, context);
                    }
                    else
                    {
                        int pair = UTF16CharacterSet.CombinePair(buffer[i - 1], ch);
                        if (!charChecker.Test(pair))
                        {
                            Substitute(buffer, i - 1, 2, context);
                        }
                    }
                }
                else
                {
                    if (!charChecker.Test(ch))
                    {
                        Substitute(buffer, i, 1, context);
                    }
                }
            } //}
        }

        protected virtual void MarkAsEscaped(string escaped, bool isKey)
        {
        }

        private void Substitute(StringBuilder buffer, int offset, int count, IXPathContext context)
        {
            StringBuilder escaped = new StringBuilder(count * 6);
            for (int j = 0; j < count; j++)
            {
                escaped.Append("\\u");
                StringBuilder hex = new StringBuilder(((int)(buffer[offset + j])).ToString("x", CultureInfo.InvariantCulture));
                while (hex.Length < 4)
                {
                    hex.Insert(0, "0");
                }

                escaped.Append(hex.ToString().ToUpperInvariant());
            }

            string replacement = Replace(escaped.ToString(), context);
            if (replacement.Length == count)
            {
                for (int j = 0; j < count; j++)
                {
                    buffer[offset + j] = replacement[j];
                }
            }
            else
            {
                for (int j = 0; j < count; j++)
                {
                    buffer.Remove(offset + j, 1);
                }

                for (int j = 0; j < replacement.Length; j++)
                {
                    buffer.Insert(offset + j, replacement[j]);
                }
            }
        }

        private string Replace(string s, IXPathContext context)
        {
            if (fallbackFunction != null)
            {
                ISequence[] args = new ISequence[1];
                args[0] = new StringValue(s);
                ISequence result = SystemFunction.DynamicCall(fallbackFunction, context, args).Head();
                IItem first = result.Head();
                return first == null ? "" : first.GetStringValue();
            }
            else
            {
                return REPLACEMENT;
            }
        }

        public virtual void SetFallbackFunction(Dictionary<string, IGroundedValue> options, IXPathContext context)
        {
            IGroundedValue val = options.ContainsKey("fallback") ? options.GetOrDefault("fallback") : null;
            if (val != null)
            {
                IItem fn = val.Head();
                if (fn is IFunctionItem)
                {
                    fallbackFunction = (IFunctionItem)fn;
                    // A 'fallback' option value that is not a function of the required type
                    // function(xs:string) as xs:string is a TYPE error (XPTY0004), not an invalid-option
                    // error (FOJS0005): the options-parameter conventions type-check each option value, and
                    // upstream reports XPTY0004 for this from that earlier layer. (FOJS0005 stays for genuine
                    // option-semantics errors like the escape+fallback conflict.)
                    if (fallbackFunction.GetArity() != 1)
                    {
                        throw new XPathException("Fallback function must have arity=1", "XPTY0004");
                    }

                    SpecificFunctionType required = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_STRING }, SequenceType.ANY_SEQUENCE);
                    if (!required.Matches(fallbackFunction, context.GetConfiguration().GetTypeHierarchy()))
                    {
                        throw new XPathException("Fallback function does not match the required type", "XPTY0004");
                    }
                }
                else
                {
                    throw new XPathException("Value of option 'fallback' is not a function", "XPTY0004");
                }
            }
        }
    }
}
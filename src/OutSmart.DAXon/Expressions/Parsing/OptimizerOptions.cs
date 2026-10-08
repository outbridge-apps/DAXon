////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class OptimizerOptions
    {
        public const int LOOP_LIFTING = 1;
        public const int EXTRACT_GLOBALS = 2;
        public const int INLINE_VARIABLES = 4;
        public const int INLINE_FUNCTIONS = 8;
        public const int INDEX_VARIABLES = 16;
        public const int CREATE_KEYS = 32;
        public const int BYTE_CODE_NOT_USED = 64;
        public const int COMMON_SUBEXPRESSIONS = 128;
        public const int MISCELLANEOUS = 256;
        public const int SWITCH = 512;
        public const int JIT = 1024;
        public const int RULE_SET = 2048;
        public const int REGEX_CACHE = 4096;
        public const int VOID_EXPRESSIONS = 8192;
        public const int TAIL_CALLS = 16384;
        public const int CONSTANT_FOLDING = 32768;
        public const int REORDER_PREDICATES = 65536;
        public static readonly OptimizerOptions FULL_HE_OPTIMIZATION = new OptimizerOptions("lvmt");
        public static readonly OptimizerOptions FULL_EE_OPTIMIZATION = new OptimizerOptions(-1);
        private readonly int options;
        public OptimizerOptions(int options)
        {
            this.options = options;
        }

        public OptimizerOptions(string flags)
        {
            int opt = 0;
            if (flags.StartsWith("-", StringComparison.Ordinal))
            {
                opt = -1;
                for (int i = 1; i < flags.Length; i++)
                {
                    char c = flags[i];
                    opt &= ~DecodeFlag(c);
                }
            }
            else
            {
                for (int i = 0; i < flags.Length; i++)
                {
                    char c = flags[i];
                    opt |= DecodeFlag(c);
                }
            }

            this.options = opt;
        }

        private int DecodeFlag(char flag)
        {
            switch (flag)
            {
                case 'c':
                    return BYTE_CODE_NOT_USED;
                case 'd':
                    return VOID_EXPRESSIONS;
                case 'e':
                    return REGEX_CACHE;
                case 'f':
                    return INLINE_FUNCTIONS;
                case 'g':
                    return EXTRACT_GLOBALS;
                case 'j':
                    return JIT;
                case 'k':
                    return CREATE_KEYS;
                case 'l':
                    return LOOP_LIFTING;
                case 'm':
                    return MISCELLANEOUS;
                case 'n':
                    return CONSTANT_FOLDING;
                case 'p':
                    return REORDER_PREDICATES;
                case 'r':
                    return RULE_SET;
                case 's':
                    return COMMON_SUBEXPRESSIONS;
                case 't':
                    return TAIL_CALLS;
                case 'v':
                    return INLINE_VARIABLES;
                case 'w':
                    return SWITCH;
                case 'x':
                    return INDEX_VARIABLES;
                default:
                    return 0;
            }
        }

        public virtual OptimizerOptions Intersect(OptimizerOptions other)
        {
            return new OptimizerOptions(options & other.options);
        }

        public virtual OptimizerOptions Union(OptimizerOptions other)
        {
            return new OptimizerOptions(options | other.options);
        }

        public virtual OptimizerOptions Except(OptimizerOptions other)
        {
            return new OptimizerOptions(options & ~other.options);
        }

        public override string ToString()
        {
            string result = "";
            if (IsSet(VOID_EXPRESSIONS))
            {
                result += "d";
            }

            if (IsSet(REGEX_CACHE))
            {
                result += "e";
            }

            if (IsSet(INLINE_FUNCTIONS))
            {
                result += "f";
            }

            if (IsSet(EXTRACT_GLOBALS))
            {
                result += "g";
            }

            if (IsSet(JIT))
            {
                result += "j";
            }

            if (IsSet(CREATE_KEYS))
            {
                result += "k";
            }

            if (IsSet(LOOP_LIFTING))
            {
                result += "l";
            }

            if (IsSet(MISCELLANEOUS))
            {
                result += "m";
            }

            if (IsSet(CONSTANT_FOLDING))
            {
                result += "n";
            }

            if (IsSet(REORDER_PREDICATES))
            {
                result += "p";
            }

            if (IsSet(RULE_SET))
            {
                result += "r";
            }

            if (IsSet(COMMON_SUBEXPRESSIONS))
            {
                result += "s";
            }

            if (IsSet(TAIL_CALLS))
            {
                result += "t";
            }

            if (IsSet(INLINE_VARIABLES))
            {
                result += "v";
            }

            if (IsSet(SWITCH))
            {
                result += "w";
            }

            if (IsSet(INDEX_VARIABLES))
            {
                result += "x";
            }

            return result;
        }

        public virtual bool IsSet(int option)
        {
            return (options & option) != 0;
        }

        public virtual int GetOptions()
        {
            return options;
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;

using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A match against a fixed string of any length, within a regular expression
    /// </summary>
    internal sealed class OpAtom : Operation
    {
        private readonly UnicodeString atom;
        private readonly int len;

        public UnicodeString Atom => atom;

        public override int MatchLength => len;
        public OpAtom(UnicodeString atom)
        {
            this.atom = atom;
            this.len = atom.Length32();
        }

        public override int MatchesEmptyString()
        {
            return len == 0 ? MATCHES_ZLS_ANYWHERE : MATCHES_ZLS_NEVER;
        }

        public override ICharacterClass GetInitialCharacterClass(bool caseBlind)
        {
            if (len == 0)
            {
                return EmptyCharacterClass.GetInstance();
            }
            else if (caseBlind)
            {
                IntSet set;
                int ch = atom.CodePointAt(0);
                int[] variants = CaseVariants.GetCaseVariants(ch);
                if (variants.Length > 0)
                {
                    set = new IntHashSet(variants.Length);
                    set.Add(ch);
                    foreach (int v in variants)
                    {
                        set.Add(v);
                    }

                    return new IntSetCharacterClass(set);
                }
            }

            return new SingletonCharacterClass(atom.CodePointAt(0));
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            UnicodeString @in = matcher.search;
            if (position + len > @in.Length())
            {
                return EmptyIntIterator.GetInstance();
            }

            if (matcher.program.flags.IsCaseIndependent())
            {
                for (int i = 0; i < len; i++)
                {
                    if (!matcher.EqualCaseBlind(@in.CodePointAt(position + i), atom.CodePointAt(i)))
                    {
                        return EmptyIntIterator.GetInstance();
                    }
                }
            }
            else
            {
                for (int i = 0; i < len; i++)
                {
                    if (@in.CodePointAt(position + i) != atom.CodePointAt(i))
                    {
                        return EmptyIntIterator.GetInstance();
                    }
                }
            }

            return new IntSingletonIterator(position + len);
        }

        public override string Display()
        {
            return atom.ToString();
        }
    }
}
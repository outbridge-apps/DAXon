////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// Operation that wraps a base operation and traces its execution
    /// </summary>
    internal sealed class OpTrace : Operation
    {
        private static int counter = 0;
        private Operation @base;

        public override int MatchLength => @base.MatchLength;

        public override int MaxLoopingDepth => @base.MaxLoopingDepth;
        public OpTrace(Operation @base)
        {
            this.@base = @base;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            IIntIterator baseIter = @base.IterateMatches(matcher, position);
            int iterNr = counter++;
            string clName = baseIter.GetType().FullName;
            int lastDot = clName.LastIndexOf('.');
            string iterName = clName.Substring(lastDot + 1);
            Console.Error.WriteLine("Iterating over " + @base.GetType().Name + " " + @base.Display() + " at position " + position + " returning " + iterName + " " + iterNr);
            return new AnonymousIntIterator(this, baseIter, iterNr);
        }

        public override int MatchesEmptyString()
        {
            return @base.MatchesEmptyString();
        }

        public override Operation Optimize(REProgram program, REFlags flags)
        {
            @base = @base.Optimize(program, flags);
            return this;
        }

        public override string Display()
        {
            return @base.Display();
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly OpTrace parent;
            private readonly IIntIterator baseIter;
            private readonly int iterNr;
            public AnonymousIntIterator(OpTrace parent, IIntIterator baseIter, int iterNr)
            {
                this.parent = parent; this.baseIter = baseIter; this.iterNr = iterNr;
            }
            public override bool HasNext()
            {
                bool b = baseIter.MoveNext();
                Console.Error.WriteLine("IIntIterator " + iterNr + " hasNext() = " + b);
                return b;
            }

            public override int Next()
            {
                int n = baseIter.Current;
                Console.Error.WriteLine("IIntIterator " + iterNr + " next() = " + n);
                return n;
            }
        }
    }
}
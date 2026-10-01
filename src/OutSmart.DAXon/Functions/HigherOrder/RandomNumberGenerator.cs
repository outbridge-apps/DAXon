////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// Implements fn:random-number-generator (XPath 3.1). Returns a map with entries 'number' (an
    /// xs:double in [0,1)), 'next' (a zero-arg function producing the next generator), and 'permute'
    /// (a function that randomly permutes its argument sequence). Deterministic for a given seed.
    /// </summary>
    internal sealed class RandomNumberGenerator : SystemFunction, ICallable
    {
        public static readonly MapType RETURN_TYPE = new MapType(BuiltInAtomicType.STRING, SequenceType.SINGLE_ITEM);

        private static readonly IFunctionItemType NEXT_FN_TYPE = new SpecificFunctionType(
            new SequenceType[] { },
            SequenceType.MakeSequenceType(RETURN_TYPE, StaticProperty.ALLOWS_ONE));

        private static readonly IFunctionItemType PERMUTE_FN_TYPE = new SpecificFunctionType(
            new SequenceType[] { SequenceType.ANY_SEQUENCE },
            SequenceType.ANY_SEQUENCE);

        private static long NextLong(Random r)
        {
            byte[] b = new byte[8];
            r.NextBytes(b);
            return BitConverter.ToInt64(b, 0);
        }

        private static MapItem Generator(long seed, IXPathContext context)
        {
            Random random = new Random(unchecked((int)seed));
            double number = random.NextDouble();
            long nextSeed = NextLong(random);
            DictionaryMap map = new DictionaryMap();
            map.InitialPut("number", new DoubleValue(number));
            map.InitialPut("next", new CallableFunction(0, new NextGenerator(nextSeed), NEXT_FN_TYPE));
            map.InitialPut("permute", new CallableFunction(1, new Permutation(nextSeed), PERMUTE_FN_TYPE));
            return map;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            long seed;
            if (arguments.Length == 0)
            {
                seed = context.GetCurrentDateTime().RandomSeed();
            }
            else
            {
                AtomicValue val = (AtomicValue)arguments[0].Head();
                seed = val == null ? context.GetCurrentDateTime().RandomSeed() : val.GetHashCode();
            }

            return Generator(seed, context);
        }

        private sealed class Permutation : ICallable
        {
            private readonly long nextSeed;
            public Permutation(long nextSeed) { this.nextSeed = nextSeed; }

            public ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ISequenceIterator iterator = arguments[0].Iterate();
                List<IItem> output = new List<IItem>();
                Random random = new Random(unchecked((int)nextSeed));
                IItem item;
                while ((item = iterator.Next()) != null)
                {
                    int p = random.Next(output.Count + 1);
                    output.Insert(p, item);
                }

                return SequenceExtent.MakeSequenceExtent(output);
            }
        }

        private sealed class NextGenerator : ICallable
        {
            private readonly long nextSeed;
            public NextGenerator(long nextSeed) { this.nextSeed = nextSeed; }

            public ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                return Generator(nextSeed, context);
            }
        }
    }
}

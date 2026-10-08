////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath translate() function
    /// </summary>
    internal sealed class Translate : SystemFunction, ICallable, IStatefulSystemFunction
    {
        private IIntToIntMap staticMap = null;

        public IIntToIntMap StaticMap => staticMap;
        public override Expression FixArguments(params Expression[] arguments)
        {
            if (arguments[1] is StringLiteral && arguments[2] is StringLiteral)
            {
                staticMap = BuildMap(((StringLiteral)arguments[1]).GroundedValue, ((StringLiteral)arguments[2]).GroundedValue);
            }

            return null;
        }

        public static StringValue TranslateFn(StringValue sv0, StringValue sv1, StringValue sv2)
        {

            // if the size of the strings is above some threshold, use a hash map to avoid O(n*m) performance
            if (sv0.Length() * sv1.Length() > 1000)
            {

                // Cut-off point for building the map based on some simple measurements
                return TranslateUsingMap(sv0, BuildMap(sv1, sv2));
            }

            UnicodeBuilder sb = new UnicodeBuilder(sv0.Length32());
            long s2len = sv2.Length();
            IIntIterator iter = sv0.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                long j = sv1.Content.IndexOf(c, 0);
                if (j < s2len)
                {
                    sb.Append(j < 0 ? c : sv2.Content.CodePointAt(j));
                }
            }

            return new StringValue(sb.ToUnicodeString());
        }

        private static IIntToIntMap BuildMap(StringValue arg1, StringValue arg2)
        {
            IIntToIntMap map = new IntToIntHashMap(arg1.Length32(), 0.5);

            // allow plenty of free space, it's better for lookups (though worse for iteration)
            IIntIterator iter = arg1.CodePoints();
            long arg2len = arg2.Length();
            long i = 0;
            while (iter.MoveNext())
            {
                int ch = iter.Current;
                if (!map.Contains(ch))
                {
                    map.Put(ch, i >= arg2len ? -1 : arg2.Content.CodePointAt(i));
                }

                i++; // else no action: duplicate
            }

            return map;
        }

        // Flat byte table distilled from a static map: table[c] = replacement codepoint for every
        // 8-bit input codepoint (-1 = delete). Null when some replacement falls outside Latin1
        // (the output could not stay a byte string). Map keys > 255 can never match a byte input,
        // so they are irrelevant here.
        internal static int[] TryBuildByteMap(IIntToIntMap map)
        {
            int[] table = new int[256];
            for (int c = 0; c < 256; c++)
            {
                int m = map.Get(c);
                int r = m == int.MaxValue ? c : m;
                if (r > 255)
                {
                    return null;
                }

                table[c] = r;
            }

            return table;
        }

        // Byte-for-byte translate over a single-span 8-bit value; null sends the caller to the
        // generic map route (16/24-bit content or a cross-segment slice).
        internal static StringValue TryTranslateBytes(StringValue @in, int[] table)
        {
            UnicodeString us = @in.Content;
            byte[] b;
            int off, len;
            if (us is Slice8 s8)
            {
                b = s8.ByteArray;
                off = s8.Start;
                len = s8.End - s8.Start;
            }
            else if (us is Twine8 t8)
            {
                b = t8.ByteArray;
                off = 0;
                len = b.Length;
            }
            else
            {
                return null;
            }

            byte[] outBytes = new byte[len];
            int w = 0;
            bool changed = false;
            for (int k = 0; k < len; k++)
            {
                int m = table[b[off + k]];
                if (m < 0)
                {
                    changed = true;
                    continue;
                }

                if (m != b[off + k])
                {
                    changed = true;
                }

                outBytes[w++] = (byte)m;
            }

            if (!changed)
            {
                // Fresh xs:string wrapper: @in itself may carry a subtype label (untypedAtomic/NCName)
                return new StringValue(us);
            }

            return new StringValue(w == len ? new Twine8(outBytes) : (UnicodeString)new Slice8(outBytes, 0, w));
        }

        public static StringValue TranslateUsingMap(StringValue @in, IIntToIntMap map)
        {
            UnicodeBuilder builder = new UnicodeBuilder(@in.Length32());
            IIntIterator iter = @in.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                int newchar = map.Get(c);
                if (newchar == int.MaxValue)
                {

                    // character not in map, so is not to be translated
                    newchar = c;
                }

                if (newchar != -1)
                {
                    builder.Append(newchar);
                } // else no action, delete the character
            }

            return new StringValue(builder.ToUnicodeString());
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue sv0 = (StringValue)arguments[0].Head();
            if (sv0 == null)
            {
                return StringValue.EMPTY_STRING;
            }

            if (staticMap != null)
            {
                return TranslateUsingMap(sv0, staticMap);
            }
            else
            {
                StringValue sv1 = (StringValue)arguments[1].Head();
                StringValue sv2 = (StringValue)arguments[2].Head();
                return TranslateFn(sv0, sv1, sv2);
            }
        }

        public Translate Copy()
        {
            Translate copy = (Translate)SystemFunction.MakeFunction(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), GetArity());
            copy.staticMap = staticMap;
            return copy;
        }

        public override Elaborator GetElaborator()
        {
            return new TranslateFnElaborator();
        }
        ISequence ICallable.Call(IXPathContext arg0, ISequence[] arg1) => Call(arg0, arg1);
        // net472 has no covariant return: delegate to the real Copy() (was => default = null → NRE in
        // SystemFunctionCall.Copy when the optimizer rebound a tree containing fn:translate).
        SystemFunction IStatefulSystemFunction.Copy() => Copy();

        internal sealed class TranslateFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Translate fn = (Translate)fnc.TargetFunction;
                IItemEvaluator arg0Eval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                IItemEvaluator arg1Eval = fnc.GetArg(1).MakeElaborator().ElaborateForItem();
                IItemEvaluator arg2Eval = fnc.GetArg(2).MakeElaborator().ElaborateForItem();
                // The optimizer's FixArguments (which precomputes staticMap for literal arg2/arg3) is not
                // wired in HE, so build the map here when both are string literals: turns the per-call
                // O(len(in)*len(from)) IndexOf scan into an O(1) map lookup per codepoint.
                IIntToIntMap staticMap = fn.StaticMap;
                if (staticMap == null && fnc.GetArg(1) is StringLiteral fromLit && fnc.GetArg(2) is StringLiteral toLit)
                {
                    staticMap = BuildMap(fromLit.GroundedValue, toLit.GroundedValue);
                }

                if (staticMap != null)
                {
                    int[] byteMap = TryBuildByteMap(staticMap);
                    return (context) =>
                    {
                        StringValue s0 = (StringValue)arg0Eval.Eval(context);
                        if (s0 == null || s0.IsEmpty())
                        {
                            return StringValue.EMPTY_STRING;
                        }

                        if (byteMap != null)
                        {
                            StringValue fast = TryTranslateBytes(s0, byteMap);
                            if (fast != null)
                            {
                                return fast;
                            }
                        }

                        return TranslateUsingMap(s0, staticMap);
                    };
                }
                else
                {
                    return (context) =>
                    {
                        StringValue s0 = (StringValue)arg0Eval.Eval(context);
                        if (s0 == null || s0.IsEmpty())
                        {
                            return StringValue.EMPTY_STRING;
                        }

                        StringValue s1 = (StringValue)arg1Eval.Eval(context);
                        StringValue s2 = (StringValue)arg2Eval.Eval(context);
                        return TranslateFn(s0, s1, s2);
                    };
                }
            }
        }
    }
}


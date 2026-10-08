////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Expressions.Instructions
{
    // Faithful port of net/sf/saxon/expr/instruct/MemoFunction.java (Saxon 12.9). Was a hollow stub
    // in functions/ whose implicit UserFunction conversion threw NIE — every xsl:function with
    // cache="yes" / new-each-time="no" died at compile time (function-1025..1035).
    // A user-defined function that remembers the results of previous calls.
    internal sealed class MemoFunction : UserFunction
    {
        private bool lookForNodes;  // true if the function signature allows nodes within argument values

        public override void SetParameterDefinitions(UserFunctionParameter[] @params)
        {
            base.SetParameterDefinitions(@params);
            foreach (UserFunctionParameter param in @params)
            {
                if (param.GetRequiredType().PrimaryType.GetUType().Overlaps(UType.ANY_NODE))
                {
                    lookForNodes = true;
                }
            }
        }

        public override void ComputeEvaluationMode()
        {
            bodyEvaluator = GetBody().MakeElaborator().Eagerly();
        }

        public override bool IsMemoFunction()
        {
            return true;
        }

        public override ISequence Call(IXPathContext context, ISequence[] actualArgs)
        {
            // Ensure the arguments are all grounded
            for (int i = 0; i < actualArgs.Length; i++)
            {
                actualArgs[i] = actualArgs[i].Materialize();
            }

            Controller controller = context.GetController();
            MemoFunctionCache cache = (MemoFunctionCache)controller.GetUserData(this, "memo-function-cache");
            if (cache == null)
            {
                cache = new MemoFunctionCache(lookForNodes);
                controller.SetUserData(this, "memo-function-cache", cache);
            }

            // If the function is tail-recursive, make a copy of the arguments, because a tail-call might overwrite them
            ISequence[] savedArgs = IsTailRecursive() || ContainsTailCalls()
                    ? (ISequence[])actualArgs.Clone()
                    : actualArgs;

            // Get a hash code for the supplied arguments
            int hash = cache.Hash(actualArgs);

            // Using this hash code, see if there is an entry in the cache for the supplied arguments
            IGroundedValue value = cache.Get(hash, actualArgs);
            if (value != null)
            {
                // if there is, use it as the return value
                return value;
            }

            // Otherwise, invoke the function
            value = base.Call(context, actualArgs).Materialize();

            // Save the result in the cache before returning it
            cache.Put(hash, savedArgs, value);
            return value;
        }

        private static IItem Substitute(NodeInfo node)
        {
            // Surrogates are only needed to ensure that temporary nodes are eligible
            // for garbage collection; lasting nodes are stored directly.
            Durability durability = node.GetTreeInfo().GetDurability();
            switch (durability)
            {
                case Durability.LASTING:
                case Durability.UNDEFINED:
                    return node;
                case Durability.TEMPORARY:
                    return new NodeSurrogate(node);
                default:
                    return null;
            }
        }

        private sealed class MemoFunctionCache
        {
            private readonly bool lookForNodes;

            /*
             * The cacheMap contains a set of buckets, indexed by the computed hash code of the argument
             * values; each bucket holds a sequence of groups of length (arity + 1), where the first
             * (arity) values in the group are the sequence of arguments to the function, and the final
             * value in the group is the corresponding function result. Temporary nodes are replaced by
             * surrogates so they stay eligible for garbage collection.
             */
            private readonly IntHashMap<List<IGroundedValue>> cacheMap = new IntHashMap<List<IGroundedValue>>();

            public MemoFunctionCache(bool lookForNodes)
            {
                this.lookForNodes = lookForNodes;
            }

            public int Hash(ISequence[] args)
            {
                int h = 0x389247ab;
                foreach (ISequence arg in args)
                {
                    IGroundedValue val = (IGroundedValue)arg;
                    if (val is IItem)
                    {
                        h ^= HashItem((IItem)val) + 1;
                    }
                    else
                    {
                        foreach (IItem it in val.AsIterable())
                        {
                            h ^= HashItem(it) + 1;
                        }
                    }
                }
                return h;
            }

            private int HashItem(IItem it)
            {
                // Hash codes aren't unique anyway; the strong equality check happens in SameValue.
                return it.GetHashCode();
            }

            public IGroundedValue Get(int hash, ISequence[] args)
            {
                int arity = args.Length;
                List<IGroundedValue> bucket = cacheMap.Get(hash);
                if (bucket == null)
                {
                    return null;
                }
                for (int i = 0; i < bucket.Count; i += (arity + 1))
                {
                    bool found = true;
                    for (int j = 0; j < arity; j++)
                    {
                        if (!SameValue((IGroundedValue)args[j], bucket[i + j]))
                        {
                            found = false;
                            break;
                        }
                    }
                    if (found)
                    {
                        return bucket[i + arity];
                    }
                }
                return null;
            }

            // Stronger test than XPath equality: the values must be indistinguishable
            // (e.g. identical type annotations, negative zero distinguished).
            private bool SameValue(IGroundedValue v0, IGroundedValue v1)
            {
                if (v0.GetLength() != v1.GetLength())
                {
                    return false;
                }
                for (int i = 0; i < v0.GetLength(); i++)
                {
                    IItem it0 = v0.ItemAt(i);
                    IItem it1 = v1.ItemAt(i);
                    if (ReferenceEquals(it0, it1))
                    {
                        continue;
                    }
                    Genre g0 = it0.GetGenre();

                    if (g0 == Genre.NODE)
                    {
                        if (it1 is NodeSurrogate)
                        {
                            return ((NodeSurrogate)it1).GetObject()((NodeInfo)it0);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    if (g0 != it1.GetGenre())
                    {
                        return false;
                    }
                    if (it0.GetGenre() == Genre.ATOMIC)
                    {
                        AtomicValue av0 = (AtomicValue)it0;
                        AtomicValue av1 = (AtomicValue)it1;
                        if (!av0.GetItemType().Equals(av1.GetItemType()))
                        {
                            return false;
                        }
                        if (!av0.IsIdentical(av1))
                        {
                            return false;
                        }
                        if (av0 is NumericValue &&
                                (((NumericValue)av0).IsNegativeZero() != ((NumericValue)av1).IsNegativeZero()))
                        {
                            return false; // The IsIdentical() test treats positive and negative zero as identical
                        }
                    }
                    else
                    {
                        if (!it0.Equals(it1))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            public void Put(int hash, ISequence[] args, IGroundedValue result)
            {
                List<IGroundedValue> bucket = cacheMap.Get(hash);
                if (bucket == null)
                {
                    bucket = new List<IGroundedValue>(args.Length + 1);
                    cacheMap.Put(hash, bucket);
                }

                // Any node in any GroundedValue is replaced by a surrogate value, so the cache
                // doesn't prevent garbage collection of temporary nodes.
                int initialSize = bucket.Count;
                foreach (ISequence val in args)
                {
                    IGroundedValue gVal = (IGroundedValue)val;
                    if (gVal is AtomicValue || gVal is IFunctionItem || gVal is EmptySequence)
                    {
                        bucket.Add(gVal);
                    }
                    else if (gVal is NodeInfo)
                    {
                        IItem subs = Substitute((NodeInfo)gVal);
                        if (subs == null)
                        {
                            return; // Value is not cacheable, e.g. a streamed or mutable node
                        }
                        bucket.Add((IGroundedValue)subs);
                    }
                    else if (lookForNodes)
                    {
                        ISequenceIterator iter = gVal.Iterate();
                        IItem it;
                        List<IItem> newSeq = new List<IItem>(gVal.GetLength());

                        while ((it = iter.Next()) != null)
                        {
                            if (it is NodeInfo)
                            {
                                IItem subs = Substitute((NodeInfo)it);
                                if (subs == null)
                                {
                                    // Value is not cacheable, e.g. a streamed or mutable node
                                    while (bucket.Count > initialSize)
                                    {
                                        bucket.RemoveAt(bucket.Count - 1);
                                    }
                                    return;
                                }
                                newSeq.Add(subs);
                            }
                            else
                            {
                                newSeq.Add(it);
                            }
                        }
                        bucket.Add(SequenceExtent.MakeSequenceExtent(newSeq));
                    }
                    else
                    {
                        bucket.Add(gVal);
                    }
                }
                bucket.Add(result);
            }
        }

        // A NodeSurrogate represents a node via a predicate that tests whether a
        // particular node is the one it stands for.
        internal sealed class NodeSurrogate : ObjectValue<Func<NodeInfo, bool>>
        {
            public NodeSurrogate(NodeInfo node) : base(Matcher(node))
            {
            }

            private static Func<NodeInfo, bool> Matcher(NodeInfo node)
            {
                if (node is TinyNodeImpl)
                {
                    long docNr = node.GetTreeInfo().GetDocumentNumber();
                    int nodeNr = ((TinyNodeImpl)node).NodeNumber;
                    bool isAttr = node is TinyAttributeImpl;
                    return node1 => (node1 is TinyAttributeImpl) == isAttr
                            && node1 is TinyNodeImpl
                            && docNr == node1.GetTreeInfo().GetDocumentNumber()
                            && nodeNr == ((TinyNodeImpl)node1).NodeNumber;
                }
                else
                {
                    string generatedId = GenerateId(node);
                    return node1 => generatedId.Equals(GenerateId(node1));
                }
            }

            private static string GenerateId(NodeInfo node)
            {
                StringBuilder sb = new StringBuilder();
                node.GenerateId(sb);
                return sb.ToString();
            }
        }
    }
}

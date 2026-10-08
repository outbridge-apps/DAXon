////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Accumulators
{
    public class AccumulatorManager
    {
        private readonly object syncLock = new object();

        private static readonly AccumulatorData MARKER = new AccumulatorData(null);
        private readonly Dictionary<ITreeInfo, Dictionary<Accumulator, IIAccumulatorData>> accumulatorDataIndex = new Dictionary<ITreeInfo, Dictionary<Accumulator, IIAccumulatorData>>();
        private readonly Dictionary<ITreeInfo, HashSet<Accumulator>> applicableAccumulators = new Dictionary<ITreeInfo, HashSet<Accumulator>>();
        public AccumulatorManager()
        {
        }

        public virtual void SetApplicableAccumulators(ITreeInfo tree, HashSet<Accumulator> accumulators)
        {
            applicableAccumulators[tree] = accumulators;
        }

        public virtual HashSet<Accumulator> GetApplicableAccumulators(ITreeInfo tree)
        {
            return applicableAccumulators.GetOrDefault(tree);
        }

        public virtual bool IsApplicable(ITreeInfo tree, Accumulator accumulator)
        {
            HashSet<Accumulator> accSet = applicableAccumulators.GetOrDefault(tree);
            return accSet == null || accSet.Contains(accumulator);
        }
        public virtual IIAccumulatorData GetAccumulatorData(ITreeInfo doc, Accumulator acc, IXPathContext context)
        {
            lock (syncLock)
            {
                Dictionary<Accumulator, IIAccumulatorData> map = accumulatorDataIndex.GetOrDefault(doc);
                if (map != null)
                {
                    IIAccumulatorData data = map.GetOrDefault(acc);
                    if (data != null)
                    {
                        if (data == MARKER)
                        {
                            throw new XPathException("Accumulator " + acc.AccumulatorName.DisplayName + " requires access to its own value", "XTDE3400");
                        }

                        return data;
                    }
                }
                else
                {
                    map = new Dictionary<Accumulator, IIAccumulatorData>();
                    accumulatorDataIndex[doc] = map;
                }

                map[acc] = MARKER;
                if (doc is VirtualTreeInfo && ((VirtualTreeInfo)doc).IsCopyAccumulators())
                {
                    NodeInfo original = ((VirtualCopy)doc.GetRootNode()).OriginalNode;
                    IIAccumulatorData originalData = GetAccumulatorData(original.GetTreeInfo(), acc, context);
                    VirtualAccumulatorData vad = new VirtualAccumulatorData(originalData);
                    map[acc] = vad;
                    return vad;
                }
                else if (doc is TinyTree && ((TinyTree)doc).CopiedFrom != null)
                {
                    IIAccumulatorData original = GetAccumulatorData(((TinyTree)doc).CopiedFrom.GetTreeInfo(), acc, context);
                    return new PathMappedAccumulatorData(original, ((TinyTree)doc).CopiedFrom);
                }
                else
                {
                    AccumulatorData d = new AccumulatorData(acc);
                    XPathContextMajor c2 = context.NewCleanContext();
                    c2.SetCurrentComponent(acc.DeclaringComponent);
                    try
                    {
                        d.BuildIndex(doc.GetRootNode(), c2);
                        map[acc] = d;
                        return d;
                    }
                    catch (XPathException err)
                    {
                        IIAccumulatorData failed = new FailedAccumulatorData(acc, err);
                        map[acc] = failed;
                        return failed;
                    }
                }
            }
        }

        public virtual void AddAccumulatorData(ITreeInfo doc, Accumulator acc, IIAccumulatorData accData)
        {
            lock (syncLock)
            {
                Dictionary<Accumulator, IIAccumulatorData> map = accumulatorDataIndex.GetOrDefault(doc);
                if (map != null)
                {
                    IIAccumulatorData data = map.GetOrDefault(acc);
                    if (data != null)
                    {
                        return;
                    }
                }
                else
                {
                    map = new Dictionary<Accumulator, IIAccumulatorData>();
                    accumulatorDataIndex[doc] = map;
                }

                map[acc] = accData;
            }
        }
    }
}
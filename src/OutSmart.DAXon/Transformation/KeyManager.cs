////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using static OutSmart.DAXon.Transformation.KeyIndex.Status;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Transformation
{
    public class KeyManager
    {
        private readonly object syncLock = new object();
        private readonly PackageData packageData;
        private readonly Dictionary<StructuredQName, KeyDefinitionSet> keyDefinitions;
        // one entry for each named key; the entry contains
        // a KeyDefinitionSet holding the key definitions with that name
        // WEAK KEYS. The KeyManager belongs to the compiled package, so it outlives every run;
        // a Dictionary keyed by the document therefore pinned every document a key() lookup ever
        // touched, for as long as the host held the executable - the weak VALUE only let the index
        // die, not the tree behind it (round AY: 15.4 MB retained per transform, unbounded).
        // Java uses a WeakHashMap here; ConditionalWeakTable is the .NET equivalent - the entry
        // disappears with the document. The value stays a WeakReference so the index itself can
        // still be collected independently, exactly as upstream.
        private ConditionalWeakTable<ITreeInfo, WeakReference<IntHashMap<KeyIndex>>> docIndexes;

        public virtual ICollection<KeyDefinitionSet> AllKeyDefinitionSets => keyDefinitions.Values;

        public virtual int NumberOfKeyDefinitions => keyDefinitions.Count;
        // one entry for each document that @is in memory;
        // the entry contains a HashMap mapping the fingerprint of the key name plus the primitive item type
        // to the HashMap that is the actual index of key/value pairs.
        public KeyManager(Configuration config, PackageData pack)
        {
            packageData = pack;
            keyDefinitions = new Dictionary<StructuredQName, KeyDefinitionSet>(10);
            docIndexes = new ConditionalWeakTable<ITreeInfo, WeakReference<IntHashMap<KeyIndex>>>();

            // Create a key definition for the idref() function
            RegisterIdrefKey(config);
        }

        private void RegisterIdrefKey(Configuration config)
        {
            lock (syncLock)
            {
                StructuredQName qName = StandardNames.GetStructuredQName(StandardNames.XS_IDREFS);
                if (keyDefinitions.GetOrDefault(qName) == null)
                {
                    BasePatternWithPredicate pp = new BasePatternWithPredicate(new NodeTestPattern(new MultipleNodeKindTest(UType.ELEMENT_OR_ATTRIBUTE)), IntegratedFunctionLibrary.MakeFunctionCall(new IsIdRef(), new Expression[] { }));
                    try
                    {
                        IndependentContext sc = new IndependentContext(config);
                        sc.SetPackageData(packageData);
                        sc.SetXPathLanguageLevel(packageData.HostLanguageVersion == 40 ? 40 : 31);
                        RetainedStaticContext rsc = new RetainedStaticContext(sc);
                        Expression sf = SystemFunction.MakeCall("string", rsc, new ContextItemExpression());
                        Expression use = SystemFunction.MakeCall("tokenize", rsc, sf); // Use the new tokenize#1
                        SymbolicName symbolicName = new SymbolicName(StandardNames.XSL_KEY, qName);
                        KeyDefinition key = new KeyDefinition(symbolicName, pp, use, null, null);
                        key.SetPackageData(packageData);
                        key.IndexedItemType = BuiltInAtomicType.STRING;
                        AddKeyDefinition(qName, key, true, config);
                    }
                    catch (XPathException err)
                    {
                        throw new InvalidOperationException(err.Message, err); // shouldn't happen
                    }
                }
            }
        }

        public virtual void PreRegisterKeyDefinition(StructuredQName keyName)
        {
            lock (syncLock)
            {
                KeyDefinitionSet keySet = keyDefinitions.GetOrDefault(keyName);
                if (keySet == null)
                {
                    keySet = new KeyDefinitionSet(keyName, keyDefinitions.Count);
                    keyDefinitions[keyName] = keySet;
                }
            }
        }

        public virtual void AddKeyDefinition(StructuredQName keyName, KeyDefinition keydef, bool reusable, Configuration config)
        {
            lock (syncLock)
            {
                KeyDefinitionSet keySet = keyDefinitions.GetOrDefault(keyName);
                if (keySet == null)
                {
                    keySet = new KeyDefinitionSet(keyName, keyDefinitions.Count);
                    keyDefinitions[keyName] = keySet;
                }

                keySet.AddKeyDefinition(keydef);
                if (!reusable)
                {
                    keySet.SetReusable(false);
                }

                bool backwardsCompatible = keySet.IsBackwardsCompatible();
                if (backwardsCompatible)
                {

                    // In backwards compatibility mode, convert all the use-expression results to sequences of strings
                    IList<KeyDefinition> v = keySet.KeyDefinitions;
                    foreach (KeyDefinition kd in v)
                    {
                        kd.SetBackwardsCompatible(true);
                        if (!kd.GetBody().GetItemType().Equals(BuiltInAtomicType.STRING))
                        {
                            AtomicSequenceConverter exp = new AtomicSequenceConverter(kd.GetBody(), BuiltInAtomicType.STRING);
                            exp.AllocateConverterStatically(config, false);
                            kd.SetBody(exp);
                        }
                    }
                }
            }
        }

        public virtual KeyDefinitionSet GetKeyDefinitionSet(StructuredQName qName)
        {
            return keyDefinitions.GetOrDefault(qName);
        }

        public virtual KeyDefinitionSet FindKeyDefinition(Patterns.Pattern finder, Expression use, string collationName)
        {
            foreach (KeyDefinitionSet keySet in keyDefinitions.Values)
            {
                if (keySet.KeyDefinitions.Count == 1)
                {
                    foreach (KeyDefinition keyDef in keySet.KeyDefinitions)
                    {
                        if (keyDef.Match.IsEqual(finder) && keyDef.Use.IsEqual(use) && keyDef.CollationName.Equals(collationName))
                        {
                            return keySet;
                        }
                    }
                }
            }

            return null;
        }

        private void BuildIndex(KeyIndex index, KeyDefinitionSet keySet, ITreeInfo doc, IXPathContext context)
        {
            index.BuildIndex(keySet, doc, context);
        }

        public virtual ISequenceIterator SelectByKey(KeyDefinitionSet keySet, ITreeInfo doc, AtomicValue soughtValue, IXPathContext context)
        {
            if (soughtValue == null)
            {
                return EmptyIterator.OfNodes();
            }

            if (keySet.IsBackwardsCompatible())
            {

                // if backwards compatibility @is in force, treat all values as strings
                ConversionRules rules = context.GetConfiguration().GetConversionRules();
                soughtValue = ((AtomicValue)Converter.Convert(soughtValue, BuiltInAtomicType.STRING, rules)).AsAtomic();
            }
            else
            {

                // If the key value is numeric, promote it to a double
                // Note: this could result in two decimals comparing equal because they convert to the same double
                BuiltInAtomicType itemType = soughtValue.PrimitiveType;
                if (itemType.Equals(BuiltInAtomicType.INTEGER) || itemType.Equals(BuiltInAtomicType.DECIMAL) || itemType.Equals(BuiltInAtomicType.FLOAT))
                {
                    soughtValue = new DoubleValue(((NumericValue)soughtValue).GetDoubleValue());
                }
            }


            // No special action needed for anyURI to string promotion (it just seems to work: tests idky44, 45)
            KeyIndex index = ObtainIndex(keySet, doc, context);
            return index.GetNodes(soughtValue);
        }

        public virtual ISequenceIterator SelectByCompositeKey(KeyDefinitionSet keySet, ITreeInfo doc, ISequenceIterator soughtValue, IXPathContext context)
        {
            KeyIndex index = ObtainIndex(keySet, doc, context);
            return index.GetComposite(soughtValue);
        }

        public virtual KeyIndex ObtainIndex(KeyDefinitionSet keySet, ITreeInfo doc, IXPathContext context)
        {
            if (keySet.IsReusable())
            {
                return ObtainSharedIndex(keySet, doc, context);
            }
            else
            {
                return ObtainLocalIndex(keySet, doc, context);
            }
        }

        private KeyIndex ObtainSharedIndex(KeyDefinitionSet keySet, ITreeInfo doc, IXPathContext context)
        {
            KeyIndex index;
            int keySetNumber = keySet.KeySetNumber;
            index = GetSharedIndex(doc, keySetNumber);
            if (index != null)
            {
                KeyIndex.Status status = index.GetStatus();
                if (status == UNDER_CONSTRUCTION)
                {
                    if (index.IsCreatedInThisThread())
                    {
                        throw new XPathException("Key definition " + keySet.KeyName.DisplayName + " is circular").WithXPathContext(context).WithErrorCode("XTDE0640");
                    }
                    else
                    {

                        // if the index is under construction in another thread, then we plough on regardless.
                        // Both threads will construct the index, but only one will be saved
                        index = null;
                    }
                }
                else if (status == FAILED)
                {
                    throw new XPathException("Construction of index for key " + keySet.KeyName.DisplayName + " was unsuccessful");
                }
            }


            // If the index does not yet exist, then create it.
            if (index == null)
            {

                // Mark the index as being under construction, in case the definition is circular
                index = new KeyIndex(keySet.IsRangeKey());
                lock (syncLock)
                {
                    index.SetStatus(UNDER_CONSTRUCTION);
                    KeyIndex index2 = PutSharedIndex(doc, keySetNumber, index, context);
                    if (index2.GetStatus() == BUILT)
                    {

                        // last chance to bail @out - another thread got there first
                        return index2;
                    }
                    else
                    {
                        index = index2;
                    }
                }


                // Now we build the index (which isn't synchronized because it doesn't write to any shared data)
                BuildIndex(index, keySet, doc, context);

                // On completion we synchronize again, and decide whether to use this index, or one that was
                // completed earlier by a different thread.
                lock (syncLock)
                {
                    index.SetStatus(BUILT);
                    index = PutSharedIndex(doc, keySetNumber, index, context);
                }
            }

            return index;
        }

        private KeyIndex ObtainLocalIndex(KeyDefinitionSet keySet, ITreeInfo doc, IXPathContext context)
        {
            KeyIndex index;
            int keySetNumber = keySet.KeySetNumber;

            // We don't synchronize the index construction (see bug 3984) because holding synchronization
            // locks while executing user code (the xsl:key/@use expression) can easily lead to deadlock.
            // Instead, we check if a completely constructed index exists; if it does, we use it. If an
            // index exists that is currently under construction, then if it's under construction in this
            // thread, we report a circularity. If it's being constructed by a different thread, then
            // we continue constructing the index, and at the end, the index that completes construction
            // first is used by all threads (which involves synchronizing for a very short time).
            index = GetLocalIndex(doc, keySetNumber, context);
            if (index != null)
            {
                KeyIndex.Status status = index.GetStatus();
                if (status == UNDER_CONSTRUCTION)
                {
                    if (index.IsCreatedInThisThread())
                    {
                        throw new XPathException("Key definition " + keySet.KeyName.DisplayName + " is circular").WithXPathContext(context).WithErrorCode("XTDE0640");
                    }
                    else
                    {

                        index = null;
                    }
                }
                else if (status == FAILED)
                {
                    throw new XPathException("Construction of index for key " + keySet.KeyName.DisplayName + " was unsuccessful");
                }
            }


            // If the index does not yet exist, then create it.
            if (index == null)
            {

                // Mark the index as being under construction, in case the definition is circular
                // putLocalIndex(doc, keySetNumber, underConstruction, context);
                index = new KeyIndex(keySet.IsRangeKey());
                lock (syncLock)
                {
                    index.SetStatus(UNDER_CONSTRUCTION);
                    KeyIndex index2 = PutLocalIndex(doc, keySetNumber, index, context);
                    if (index2.GetStatus() == BUILT)
                    {

                        // last chance to bail @out - another thread got there first
                        return index2;
                    }
                    else
                    {
                        index = index2;
                    }
                }


                // Now we build the index (which isn't synchronized because it doesn't write to any shared data)
                BuildIndex(index, keySet, doc, context);

                lock (syncLock)
                {
                    index.SetStatus(BUILT);
                    index = PutLocalIndex(doc, keySetNumber, index, context);
                }
            }

            return index;
        }

        private KeyIndex PutSharedIndex(ITreeInfo doc, int keyFingerprint, KeyIndex index, IXPathContext context)
        {
            lock (syncLock)
            {
                if (docIndexes == null)
                {

                    // it's transient, so it will be null when reloading a compiled stylesheet
                    docIndexes = new ConditionalWeakTable<ITreeInfo, WeakReference<IntHashMap<KeyIndex>>>();
                }

                docIndexes.TryGetValue(doc, out WeakReference<IntHashMap<KeyIndex>> indexRef);
                IntHashMap<KeyIndex> indexList;
                if (indexRef == null || !indexRef.TryGetTarget(out indexList))
                {
                    indexList = new IntHashMap<KeyIndex>(10);

                    // Ensure there is a firm reference to the indexList for the duration of a transformation
                    // But for keys associated with temporary trees, or documents that have been discarded from
                    // the document pool, keep the reference within the document node itself.
                    Controller controller = context.GetController();
                    if (controller.GetDocumentPool().Contains(doc))
                    {
                        context.GetController().SetUserData(doc, "saxon:key-index-list", indexList);
                    }
                    else
                    {
                        doc.SetUserData("saxon:key-index-list", indexList);
                    }


                    docIndexes.Remove(doc);   // Add throws on a duplicate key; a stale entry can only be a dead index
                    docIndexes.Add(doc, new WeakReference<IntHashMap<KeyIndex>>(indexList));
                }

                KeyIndex result = indexList[keyFingerprint];
                if (result == null || result.GetStatus() != BUILT)
                {

                    // Use this index in preference to one that is under construction in another thread
                    indexList.Put(keyFingerprint, index);
                    result = index;
                }

                return result;
            }
        }

        private KeyIndex PutLocalIndex(ITreeInfo doc, int keyFingerprint, KeyIndex index, IXPathContext context)
        {
            lock (syncLock)
            {
                Controller controller = context.GetController();
                IntHashMap<Dictionary<long, KeyIndex>> masterIndex = controller.LocalIndexes;
                Dictionary<long, KeyIndex> docIndexes = masterIndex[keyFingerprint];
                if (docIndexes == null)
                {
                    docIndexes = new Dictionary<long, KeyIndex>();
                    masterIndex.Put(keyFingerprint, docIndexes);
                }

                KeyIndex result = docIndexes.GetOrDefault(doc.GetDocumentNumber());
                if (result == null || result.GetStatus() != BUILT)
                {

                    // Use this index in preference to one that is under construction in another thread
                    docIndexes.PutAndGetPrevious(doc.GetDocumentNumber(), index);
                    result = index;
                }

                return result;
            }
        }

        private KeyIndex GetSharedIndex(ITreeInfo doc, int keyFingerprint)
        {
            lock (syncLock)
            {
                if (docIndexes == null)
                {

                    // it's transient, so it will be null when reloading a compiled stylesheet
                    docIndexes = new ConditionalWeakTable<ITreeInfo, WeakReference<IntHashMap<KeyIndex>>>();
                }

                docIndexes.TryGetValue(doc, out WeakReference<IntHashMap<KeyIndex>> @ref);
                if (@ref == null)
                {
                    return null;
                }

                @ref.TryGetTarget(out IntHashMap<KeyIndex> indexList);
                if (indexList == null)
                {
                    return null;
                }

                return indexList[keyFingerprint];
            }
        }

        private KeyIndex GetLocalIndex(ITreeInfo doc, int keyFingerprint, IXPathContext context)
        {
            lock (syncLock)
            {
                Controller controller = context.GetController();
                IntHashMap<Dictionary<long, KeyIndex>> masterIndex = controller.LocalIndexes;
                Dictionary<long, KeyIndex> docIndexes = masterIndex[keyFingerprint];
                if (docIndexes == null)
                {
                    return null;
                }
                else
                {
                    return docIndexes.GetOrDefault(doc.GetDocumentNumber());
                }
            }
        }

        public virtual void ClearDocumentIndexes(ITreeInfo doc)
        {
            lock (syncLock)
            {
                docIndexes.Remove(doc);
            }
        }

        public virtual void ExportKeys(ExpressionPresenter @out, Dictionary<Component, int> componentIdMap)
        {
            foreach (KeyValuePair<StructuredQName, KeyDefinitionSet> e in keyDefinitions)
            {
                bool reusable = e.Value.IsReusable();
                IList<KeyDefinition> list = e.Value.KeyDefinitions;
                foreach (KeyDefinition kd in list)
                {
                    if (!kd.GetObjectName().Equals(StandardNames.GetStructuredQName(StandardNames.XS_IDREFS)))
                    {
                        kd.Export(@out, reusable, componentIdMap);
                    }
                }
            }
        }
    }
}

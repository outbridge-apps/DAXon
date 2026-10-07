////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Trees.Tiny
{
    internal sealed class TinyTree : GenericTreeInfo, INodeVectorTree
    {
        private static readonly string[] EMPTY_STRING_ARRAY = new string[0];
        // A tree starts no larger than this and takes the size learned from recent trees when it first
        // grows; reserving that size at once (upstream) made the ten trees after a large one all large.
        private const int InitialCapacity = 4096;
        private int nodeHint, attributeHint, namespaceHint;
        private readonly long inputLength;   // of the parsed input, when known: sizes this tree and teaches the next
        public LargeTextBuffer textBuffer;
        public UnicodeString commentBuffer = null; // created when needed
        public int numberOfNodes = 0; // excluding attributes and namespaces

        // Whether any text (or textual-element inline text) is whitespace-only. When none is, a
        // space-stripping view of this tree is an identity view (rules only ever preserve more),
        // so the wrap - and its per-node wrapper tax - can be skipped. Lazy with early exit:
        // documents that do contain whitespace find the first one immediately. Benign idempotent
        // race on the cached verdict.
        private volatile int whitespaceTextState;   // 0 unknown, 1 present, 2 absent

        internal bool ContainsWhitespaceText()
        {
            int s = whitespaceTextState;
            if (s != 0)
            {
                return s == 1;
            }

            bool found = false;
            for (int i = 0; i < numberOfNodes; i++)
            {
                int kind = nodeKind[i];
                // WHITESPACE_TEXT is all-white by construction (its alpha/beta hold the packed
                // compressed run, not buffer offsets - never read them as text coordinates).
                if (kind == Types.Type.WHITESPACE_TEXT
                    || ((kind == Types.Type.TEXT || kind == Types.Type.TEXTUAL_ELEMENT)
                        && TinyTextImpl.IsWhitespaceOnly(this, i)))
                {
                    found = true;
                    break;
                }
            }

            whitespaceTextState = found ? 1 : 2;
            return found;
        }

        // What the tree holds, for a call's memory limit (ProcessorOptions.MaxMemoryBytes): its node, attribute and
        // namespace arrays, its text, its attribute values and its large namespace maps. Small indexes are left out.
        internal long RetainedBytes()
        {
            return Size(nodeKind, 1) + Size(depth, 2) + Size(next, 4) + Size(alpha, 4) + Size(beta, 4) + Size(nameCode, 4)
                + Size(prior, 4) + Size(typeArray, 8) + Size(typedValueArray, 8) + Size(lineNumbers, 4) + Size(columnNumbers, 4)
                + Size(attParent, 4) + Size(attCode, 4) + Size(attValue, 8) + Size(attTypedValue, 8) + Size(attType, 8)
                + Size(namespaceMaps, 8) + (textBuffer?.RetainedBytes() ?? 0) + 2L * (commentBuffer?.Length32() ?? 0)
                + AttributeValueBytes() + namespaceBytes + TreeOverhead;
        }

        // The tree's own objects and its arrays' headers, which a small tree mostly consists of (measured: 300k one-element
        // trees held 1.2 KB each).
        private const int TreeOverhead = 1100;

        private static long Size(Array array, int width)
        {
            return array == null ? 0 : (long)array.Length * width;
        }

        // Attributes are only ever appended, so the sum goes on from the last one: a tree being built is summed again
        // and again, and a tree handed to many calls is measured for each.
        private long[] attributeValueBytes;   // {attributes summed, their bytes}, replaced whole

        private long AttributeValueBytes()
        {
            long[] memo = attributeValueBytes;
            if (memo != null && memo[0] == numberOfAttributes)
            {
                return memo[1];
            }

            int from = memo != null && memo[0] < numberOfAttributes ? (int)memo[0] : 0;
            long bytes = from > 0 ? memo[1] : 0;
            for (int i = from; i < numberOfAttributes; i++)
            {
                string value = attValue[i];
                if (value != null)
                {
                    bytes += 24 + 2L * value.Length;
                }
            }

            attributeValueBytes = new long[] { numberOfAttributes, bytes };
            return bytes;
        }

        // A namespace map binding one more prefix per level of nesting copies its parent's, so a deep tree's maps grow
        // with the square of its depth. Maps this large count their contents; trees share the small ones.
        private const int BigNamespaceMap = 256;
        private long namespaceBytes;

        // A tree that grows large is entered in the ledger of the call building it, which then sums it as it grows; a
        // small one is sampled once built.
        private bool inLedger;
        private static readonly Func<object, long> Sizer = t => ((TinyTree)t).RetainedBytes();

        private void Grown()
        {
            if (!inLedger && RetainedBytes() >= MemoryLedger.Big)
            {
                inLedger = true;
                MemoryLedger.Hold(this, Sizer);
            }
        }

        // RetainedBytes() without walking the attribute values or the text segments.
        private long EstimatedBytes()
        {
            return 19L * nodeKind.Length + 16L * attParent.Length + 2L * textBuffer.Length() + 8L * namespaceMaps.Length
                + namespaceBytes + TreeOverhead;
        }

        public byte[] nodeKind;
        public short[] depth;
        public int[] next;
        public int[] alpha;
        public int[] beta;
        public int[] nameCode;
        internal int[] prior = null;
        ISchemaType[] typeArray = null;
        IAtomicSequence[] typedValueArray = null;
        IntSet idRefAttributes = null;
        IntSet nilledElements = null;
        IntSet defaultedAttributes = null;
        IntSet topWithinEntity = null;
        private bool allowTypedValueCache = true;
        private Dictionary<string, IntSet> localNameIndex = null;
        public int numberOfAttributes = 0;
        public int[] attParent;
        public int[] attCode;
        public string[] attValue;
        IAtomicSequence[] attTypedValue;
        public ISimpleType[] attType;
        int numberOfNamespaces = 0;
        public NamespaceMap[] namespaceMaps;
        private NamespaceMap lastAddedNsMap; // memo: elements overwhelmingly share one map instance
        private int lastAddedNsIndex;
        private int[] lineNumbers = null;
        private int[] columnNumbers = null;
        private SystemIdMap systemIdMap = null;
        public bool usesNamespaces = false;
        public PrefixPool prefixPool = new PrefixPool();
        private TinyDocumentImpl documentRoot;
        private Dictionary<string, NodeInfo> idTable;
        public Dictionary<string, string[]> entityTable;
        private NodeInfo copiedFrom;
        public IntHashMap<string> knownBaseUris;
        private string uniformBaseUri = null;

        public NodeInfo CopiedFrom { get => this.copiedFrom; set => this.copiedFrom = value; }

        public string UniformBaseUri { get => this.uniformBaseUri; set => this.uniformBaseUri = value; }

        public override IEnumerator<string> UnparsedEntityNames
        {
            get
            {
                if (entityTable == null)
                {
                    return System.Linq.Enumerable.Empty<string>().GetEnumerator();
                }
                else
                {
                    return entityTable.Keys.GetEnumerator();
                }
            }
        }

        public int NumberOfNodes => numberOfNodes;

        public byte[] NodeKindArray => nodeKind;

        public int[] NameCodeArray => nameCode;

        public ISchemaType[] TypeArray => typeArray;

        //                case OutSmart.DAXon.Types.Type.TEXTUAL_ELEMENT: {
        //                    nameCode[to] = (source.nameCode[from] & NamePool.FP_MASK) |
        //                case OutSmart.DAXon.Types.Type.TEXT: {
        //                case OutSmart.DAXon.Types.Type.WHITESPACE_TEXT: {
        //                    alpha[to] = source.alpha[from];
        //                    beta[to] = source.beta[from];
        //                case OutSmart.DAXon.Types.Type.COMMENT: {
        //                    string text = source.commentBuffer.subSequence(start, start+len);
        //                case OutSmart.DAXon.Types.Type.PROCESSING_INSTRUCTION:
        //                    nameCode[to] = source.nameCode[from];
        //                    string text = source.commentBuffer.subSequence(start, start + len);
        //
        //                case OutSmart.DAXon.Types.Type.PARENT_POINTER:
        //                    alpha[to] = source.alpha[from] + (to - from);
        //                    beta[to] = -1;
        //                default:
        //        numberOfNodes += length;
        public Dictionary<string, IntSet> LocalNameIndex
        {
            get
            {
                lock (syncLock)
                {
                    if (localNameIndex == null)
                    {
                        localNameIndex = new Dictionary<string, IntSet>();
                        IntHashSet indexed = new IntHashSet();
                        for (int i = 0; i < numberOfNodes; i++)
                        {
                            if ((nodeKind[i] & 0xf) == Types.Type.ELEMENT)
                            {
                                int fp = nameCode[i] & NamePool.FP_MASK;
                                if (!indexed.Contains(fp))
                                {
                                    string local = GetNamePool().GetLocalName(fp);
                                    indexed.Add(fp);
                                    IntSet existing = localNameIndex.GetOrDefault(local);
                                    if (existing == null)
                                    {
                                        localNameIndex[local] = new IntSingletonSet(fp);
                                    }
                                    else
                                    {
                                        IntSet copy = existing.IsMutable() ? existing : existing.MutableCopy();
                                        copy.Add(fp);
                                        localNameIndex[local] = copy;
                                    }
                                }
                            }
                        }
                    }

                    return localNameIndex;
                }
            }
        }
        public TinyTree(Configuration config, Statistics statistics) : this(config, statistics, -1)
        {
        }

        // inputLength: bytes (chars for a string) of the input being parsed, or -1. With it the arrays are
        // sized from this input at the latest such tree's density; growing from a small start cost a
        // large document after small ones +23% allocation.
        internal TinyTree(Configuration config, Statistics statistics, long inputLength) : base(config)
        {
            this.inputLength = inputLength;
            nodeHint = statistics.AverageNodes + 1;
            attributeHint = statistics.AverageAttributes + 1;
            namespaceHint = statistics.AverageNamespaces + 1;
            int nodes = Math.Min(nodeHint, InitialCapacity);
            int attributes = Math.Min(attributeHint, InitialCapacity);
            if (inputLength > 0)
            {
                // Small after small keeps the last ten's size, exact for them. Otherwise recent trees about
                // this input's estimate give their size (exact for a repeated document); the estimate has a margin.
                statistics.SizesFromInput(inputLength, out int fromNodes, out int fromAttributes);
                if (fromNodes > 0 && (fromNodes > InitialCapacity || nodeHint > InitialCapacity))
                {
                    nodes = nodeHint >= fromNodes - fromNodes / 6 && nodeHint <= fromNodes ? nodeHint : fromNodes;
                    nodeHint = 0;
                }

                if (fromAttributes > 0 && (fromAttributes > InitialCapacity || attributeHint > InitialCapacity))
                {
                    attributes = attributeHint >= fromAttributes - fromAttributes / 6 && attributeHint <= fromAttributes ? attributeHint : fromAttributes;
                    attributeHint = 0;
                }
            }

            int namespaces = Math.Min(namespaceHint, InitialCapacity);
            int characters = Math.Min(statistics.AverageCharacters + 10, 65536);
            nodeKind = new byte[nodes];
            depth = new short[nodes];
            next = new int[nodes];
            alpha = new int[nodes];
            beta = new int[nodes];
            nameCode = new int[nodes];
            numberOfAttributes = 0;
            attParent = new int[attributes];
            attCode = new int[attributes];
            attValue = new string[attributes];
            numberOfNamespaces = 0;
            namespaceMaps = new NamespaceMap[namespaces];
            textBuffer = new LargeTextBuffer(characters);
            SetConfiguration(config);
        }

        public override void SetConfiguration(Configuration config)
        {
            base.SetConfiguration(config);
            allowTypedValueCache = config.IsLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION) && config.GetBooleanProperty(Feature<bool>.USE_TYPED_VALUE_CACHE); //addNamespace(0, NamespaceBinding.XML);
        }

        private void EnsureNodeCapacity(short kind, int needed)
        {
            if (nodeKind.Length < numberOfNodes + needed)
            {

                int k = kind == Types.Type.STOPPER ? numberOfNodes + 1 : Math.Max(Math.Max(numberOfNodes * 2, numberOfNodes + needed), nodeHint);
                nodeHint = 0;
                Array.Resize(ref nodeKind, k);
                Array.Resize(ref next, k);
                Array.Resize(ref depth, k);
                Array.Resize(ref alpha, k);
                Array.Resize(ref beta, k);
                Array.Resize(ref nameCode, k);
                if (typeArray != null)
                {
                    Array.Resize(ref typeArray, k);
                }

                if (typedValueArray != null)
                {
                    Array.Resize(ref typedValueArray, k);
                }

                if (lineNumbers != null)
                {
                    Array.Resize(ref lineNumbers, k);
                    Array.Resize(ref columnNumbers, k);
                }

                Grown();
            }
        }

        private void EnsureAttributeCapacity(int needed)
        {
            if (attParent.Length < numberOfAttributes + needed)
            {
                int k = Math.Max(Math.Max(numberOfAttributes + needed, numberOfAttributes * 2), attributeHint);
                attributeHint = 0;
                if (k == 0)
                {
                    k = 10 + needed;
                }

                Array.Resize(ref attParent, k);
                Array.Resize(ref attCode, k);
                Array.Resize(ref attValue, k);
                if (attType != null)
                {
                    Array.Resize(ref attType, k);
                }

                if (attTypedValue != null)
                {
                    Array.Resize(ref attTypedValue, k);
                }

                Grown();
            }
        }

        private void EnsureNamespaceCapacity(int needed)
        {
            if (namespaceMaps.Length < numberOfNamespaces + needed)
            {
                int k = Math.Max(Math.Max(numberOfNamespaces * 2, numberOfNamespaces + needed), namespaceHint);
                namespaceHint = 0;
                if (k == 0)
                {
                    k = 10;
                }

                Array.Resize(ref namespaceMaps, k);
                Grown();
            }
        }

        public int AddDocumentNode(TinyDocumentImpl doc)
        {
            SetRootNode(doc);
            return AddNode(Types.Type.DOCUMENT, 0, 0, 0, -1);
        }

        public int AddNode(short kind, int depth, int alpha, int beta, int nameCode)
        {
            // The depth array is short[] - 2 bytes per node, retained for the tree's whole life -
            // so the implementation cannot represent a tree deeper than short.MaxValue. Before
            // round BF-2 the narrowing cast below simply WRAPPED to negative, and the wrap was
            // silent AND lossy: the depth comparisons that drive string-value and the axis walks
            // (see GetAtomizedValueOfUntypedNode) run while depth[next] > level, so they stopped
            // dead at the first wrapped node and every descendant below it vanished from the
            // result with no error at all. count() and ancestor:: kept working, which is what made
            // it so easy to miss. The limit is enforced HERE, at the cast, so no node-adding path
            // can bypass it - widening to int[] was the alternative and was rejected: it would
            // cost 2 bytes per node on every tree in the product to buy depth nobody needs.
            //   The boundary in terms of INPUT nesting is one level lower than the constant when the
            // innermost element has text content: a text node is added a level below its parent and
            // only folded into a TEXTUAL_ELEMENT afterwards, so a 32767-deep element chain with a
            // text leaf puts a node at 32768. Measured: such a document DID answer correctly before
            // this guard - but only because the offending node was discarded by that fold, while the
            // fold itself compares an already-wrapped depth. Depending on that is not a contract, so
            // the honest rule is the one enforced here: no node may be deeper than short.MaxValue.
            if (depth > short.MaxValue)
            {
                throw new XPathException("Tree depth limit exceeded: this implementation cannot build a tree "
                    + "nested deeper than " + short.MaxValue + " levels", DAXonErrorCode.SXLM0002);
            }

            EnsureNodeCapacity(kind, 1);
            nodeKind[numberOfNodes] = (byte)kind;
            this.depth[numberOfNodes] = (short)depth;
            this.alpha[numberOfNodes] = alpha;
            this.beta[numberOfNodes] = beta;
            this.nameCode[numberOfNodes] = nameCode;
            next[numberOfNodes] = -1; // safety precaution
            if (typeArray != null)
            {
                typeArray[numberOfNodes] = Untyped.INSTANCE;
            }

            if (numberOfNodes == 0)
            {
                SetDocumentNumber(GetConfiguration().DocumentNumberAllocator.AllocateDocumentNumber());
            }


            //                int[] r2 = new int[rootIndexUsed * 2];
            //                System.arraycopy(rootIndex, 0, r2, 0, rootIndexUsed);
            //                rootIndex = r2;
            //            }
            //            rootIndex[rootIndexUsed++] = numberOfNodes;
            //        }
            return numberOfNodes++;
        }

        public void AppendChars(UnicodeString chars)
        {
            textBuffer.AppendUnicodeString(chars); //        chars.supplyContent(textBuffer, 0, chars.length());
            if (!inLedger && textBuffer.Length() >= MemoryLedger.BigChars)
            {
                Grown();
            }

            //        ensureTextCapacity(1);
            //        textChunks[textChunksUsed++] = chars;
        }

        public void Condense(Statistics statistics)
        {

            // Up to an eighth unused stays (upstream: 20000 nodes, 1000 attributes): a size estimated from the
            // input runs up to 10% over, and trimming that would copy the arrays a second time.
            if (numberOfNodes * 3 < nodeKind.Length || (nodeKind.Length - numberOfNodes > Math.Max(20000, nodeKind.Length / 8)))
            {

                Array.Resize(ref nodeKind, numberOfNodes);
                Array.Resize(ref next, numberOfNodes);
                Array.Resize(ref depth, numberOfNodes);
                Array.Resize(ref alpha, numberOfNodes);
                Array.Resize(ref beta, numberOfNodes);
                Array.Resize(ref nameCode, numberOfNodes);
                if (typeArray != null)
                {
                    Array.Resize(ref typeArray, numberOfNodes);
                }

                if (lineNumbers != null)
                {
                    Array.Resize(ref lineNumbers, numberOfNodes);
                    Array.Resize(ref columnNumbers, numberOfNodes);
                }
            }

            if ((numberOfAttributes * 3 < attParent.Length) || (attParent.Length - numberOfAttributes > Math.Max(1000, attParent.Length / 8)))
            {
                int k = numberOfAttributes;

                if (k == 0)
                {
                    attParent = IntArraySet.EMPTY_INT_ARRAY;
                    attCode = IntArraySet.EMPTY_INT_ARRAY;
                    attValue = EMPTY_STRING_ARRAY;
                    attType = null;
                }
                else
                {
                    Array.Resize(ref attParent, numberOfAttributes);
                    Array.Resize(ref attCode, numberOfAttributes);
                    Array.Resize(ref attValue, numberOfAttributes);
                }

                if (attType != null)
                {
                    Array.Resize(ref attType, numberOfAttributes);
                }
            }

            if (numberOfNamespaces * 3 < namespaceMaps.Length)
            {
                Array.Resize(ref namespaceMaps, numberOfNamespaces);
            }

            prefixPool.Condense();
            if (!inLedger)
            {
                inLedger = true;
                MemoryLedger.Active?.Sample(this, EstimatedBytes());
            }

            statistics.UpdateStatistics(numberOfNodes, numberOfAttributes, numberOfNamespaces, textBuffer, inputLength); //        System.Console.Error.println("STATS: " + averageNodes + ", " + averageAttributes + ", "
            //                + averageNamespaces + ", " + averageCharacters);
            //        if (charBufferLength * 3 < charBuffer.length ||
            //                charBuffer.length - charBufferLength > 10000) {
            //            charBuffer = c2;
            //        }
        }

        public void SetElementAnnotation(int nodeNr, ISchemaType type)
        {
            if (!type.Equals(Untyped.INSTANCE))
            {
                if (typeArray == null)
                {
                    typeArray = new ISchemaType[nodeKind.Length];
                    ArrayTools.Fill(typeArray, 0, nodeKind.Length, Untyped.INSTANCE);
                }

                typeArray[nodeNr] = type;
            }
        }

        public ISchemaType GetSchemaType(int nodeNr)
        {
            if (typeArray == null)
            {
                return Untyped.INSTANCE;
            }

            return typeArray[nodeNr];
        }

        public IAtomicSequence GetTypedValueOfElement(TinyElementImpl element)
        {
            int nodeNr = element.nodeNr;
            if (typeArray == null)
            {
                return StringValue.MakeUntypedAtomic(TinyParentNodeImpl.GetStringValue(this, nodeNr));
            }

            if (typedValueArray == null || typedValueArray[nodeNr] == null)
            {
                ISchemaType stype = GetSchemaType(nodeNr);
                int annotation = stype.Fingerprint;
                if (annotation == StandardNames.XS_UNTYPED || annotation == StandardNames.XS_UNTYPED_ATOMIC || annotation == StandardNames.XS_ANY_TYPE)
                {
                    UnicodeString stringValue = TinyParentNodeImpl.GetStringValue(this, nodeNr);
                    return StringValue.MakeUntypedAtomic(stringValue);
                }
                else if (annotation == StandardNames.XS_STRING)
                {
                    UnicodeString stringValue = TinyParentNodeImpl.GetStringValue(this, nodeNr);
                    return new StringValue(stringValue);
                }
                else if (annotation == StandardNames.XS_ANY_URI)
                {
                    UnicodeString stringValue = TinyParentNodeImpl.GetStringValue(this, nodeNr);
                    return new AnyURIValue(stringValue);
                }
                else
                {
                    IAtomicSequence value = stype.Atomize(element);
                    if (allowTypedValueCache)
                    {
                        if (typedValueArray == null)
                        {
                            typedValueArray = new IAtomicSequence[nodeKind.Length];
                        }

                        typedValueArray[nodeNr] = value;
                    }

                    return value;
                }
            }
            else
            {
                return typedValueArray[nodeNr];
            }
        }

        public IAtomicSequence GetTypedValueOfElement(int nodeNr)
        {
            if (typedValueArray == null || typedValueArray[nodeNr] == null)
            {
                ISchemaType stype = GetSchemaType(nodeNr);
                int annotation = stype.Fingerprint;
                if (annotation == StandardNames.XS_UNTYPED_ATOMIC || annotation == StandardNames.XS_UNTYPED)
                {
                    return StringValue.MakeUntypedAtomic(TinyParentNodeImpl.GetStringValue(this, nodeNr));
                }
                else if (annotation == StandardNames.XS_STRING)
                {
                    return new StringValue(TinyParentNodeImpl.GetStringValue(this, nodeNr).Tidy());
                }
                else if (annotation == StandardNames.XS_ANY_URI)
                {
                    return new AnyURIValue(TinyParentNodeImpl.GetStringValue(this, nodeNr));
                }
                else if (annotation == StandardNames.XS_ID)
                {
                    return new StringValue(TinyParentNodeImpl.GetStringValue(this, nodeNr).Tidy(), BuiltInAtomicType.ID);
                }
                else
                {
                    TinyNodeImpl element = GetNode(nodeNr);
                    IAtomicSequence value = stype.Atomize(element);
                    if (allowTypedValueCache)
                    {
                        if (typedValueArray == null)
                        {
                            typedValueArray = new IAtomicSequence[nodeKind.Length];
                        }

                        typedValueArray[nodeNr] = value;
                    }

                    return value;
                }
            }
            else
            {
                return typedValueArray[nodeNr];
            }
        }

        public IAtomicSequence GetTypedValueOfAttribute(TinyAttributeImpl att, int nodeNr)
        {
            if (attType == null)
            {

                // it's an untyped tree
                return new StringValue(attValue[nodeNr], BuiltInAtomicType.UNTYPED_ATOMIC);
            }

            if (attTypedValue == null || attTypedValue[nodeNr] == null)
            {
                ISimpleType type = GetAttributeType(nodeNr);
                if (type.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    return new StringValue(attValue[nodeNr], BuiltInAtomicType.UNTYPED_ATOMIC);
                }
                else if (type.Equals(BuiltInAtomicType.STRING))
                {
                    return new StringValue(attValue[nodeNr]);
                }
                else if (type.Equals(BuiltInAtomicType.ANY_URI))
                {
                    return new AnyURIValue((attValue[nodeNr].ToString()));
                }
                else
                {
                    if (att == null)
                    {
                        att = new TinyAttributeImpl(this, nodeNr);
                    }

                    IAtomicSequence value = type.Atomize(att);
                    if (allowTypedValueCache)
                    {
                        if (attTypedValue == null)
                        {
                            attTypedValue = new IAtomicSequence[attParent.Length];
                        }

                        attTypedValue[nodeNr] = value;
                    }

                    return value;
                }
            }
            else
            {
                return attTypedValue[nodeNr];
            }
        }

        public int GetNodeKind(int nodeNr)
        {
            int kind = nodeKind[nodeNr];
            return kind == Types.Type.WHITESPACE_TEXT ? Types.Type.TEXT : kind;
        }

        public int GetFingerprint(int nodeNr)
        {
            int nc = nameCode[nodeNr];
            return nc == -1 ? -1 : nc & NamePool.FP_MASK;
        }

        public string GetPrefix(int nodeNr)
        {
            int code = nameCode[nodeNr] >> 20;
            if (code <= 0)
            {
                return code == 0 ? "" : null;
            }

            return prefixPool.GetPrefix(code);
        }

        internal void EnsurePriorIndex()
        {
            if (prior == null || prior.Length < numberOfNodes)
            {

                // bug 3665
                MakePriorIndex();
            }
        }

        private void MakePriorIndex()
        {
            lock (syncLock)
            {
                int[] p = new int[numberOfNodes];
                ArrayTools.Fill(p, 0, numberOfNodes, -1);
                for (int i = 0; i < numberOfNodes; i++)
                {
                    int nextNode = next[i];
                    if (nextNode > i)
                    {
                        p[nextNode] = i;
                    }
                }

                prior = p;
            }
        }

        public void AddAttribute(NodeInfo root, int parent, int nameCode, ISimpleType type, string attValue, int properties)
        {
            EnsureAttributeCapacity(1);
            attParent[numberOfAttributes] = parent;
            attCode[numberOfAttributes] = nameCode;
            this.attValue[numberOfAttributes] = attValue.ToString();
            if (!inLedger && attValue.Length >= MemoryLedger.BigChars)
            {
                Grown();
            }

            if (!type.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                InitializeAttributeTypeCodes();
            }

            if (attType != null)
            {
                attType[numberOfAttributes] = type;
            }

            if (alpha[parent] == -1)
            {
                alpha[parent] = numberOfAttributes;
            }

            if (root is TinyDocumentImpl)
            {
                HandleRootTinyDoc(parent, type, nameCode, attValue, properties);
            }


            // Note: IDREF attributes are not indexed at this stage; that happens only if and when
            // the idref() function is called.
            // Note that an attTypes array will be created for all attributes if any IDREF value is reported.
            numberOfAttributes++;
        }

        private void HandleRootTinyDoc(int parent, ISimpleType type, int nameCode, string attValue, int properties)
        {
            bool isID = false;
            try
            {
                if (ReceiverOption.Contains(properties, ReceiverOption.IS_ID))
                {
                    isID = true;
                }
                else if ((nameCode & NamePool.FP_MASK) == StandardNames.XML_ID)
                {
                    isID = true;
                }
                else if (type.IsIdType())
                {
                    isID = true;
                }
            }
            catch (MissingComponentException e)
            {
            }

            if (isID)
            {

                // The attribute is marked as being an ID. But we don't trust it - it
                // might come from a non-validating parser. Before adding it to the index, we
                // check that it really is an ID.
                string id = Whitespace.Trim(attValue);

                // Make an exception to our usual policy of storing the original string value.
                // This is because xml:id processing applies whitespace trimming at an earlier stage
                this.attValue[numberOfAttributes] = id;
                if (NameChecker.IsValidNCName(id))
                {
                    NodeInfo e = GetNode(parent);
                    RegisterID(e, id);
                }
                else if (attType != null)
                {
                    attType[numberOfAttributes] = BuiltInAtomicType.UNTYPED_ATOMIC;
                }
            }

            bool isIDREF = false;
            try
            {
                if (ReceiverOption.Contains(properties, ReceiverOption.IS_IDREF))
                {
                    isIDREF = true;
                }
                else if (type == BuiltInAtomicType.IDREF || type == BuiltInListType.IDREFS)
                {
                    isIDREF = true;
                }
                else if (type.IsIdRefType())
                {

                    // The attribute has the idref property only if at least one item in its typed value
                    // is an IDREF: see Saxon bug 2331
                    try
                    {
                        IAtomicSequence @as = type.GetTypedValue(StringView.Of(attValue).Tidy(), null, GetConfiguration().GetConversionRules());
                        foreach (AtomicValue v in @as)
                        {
                            if (v.GetItemType().IsIdRefType())
                            {
                                isIDREF = true;
                                break;
                            }
                        }
                    }
                    catch (ValidationException ve)
                    {
                    }
                }
            }
            catch (MissingComponentException e)
            {
            }

            if (isIDREF)
            {
                if (idRefAttributes == null)
                {
                    idRefAttributes = new IntHashSet();
                }

                idRefAttributes.Add(numberOfAttributes);
            }
        }

        private void InitializeAttributeTypeCodes()
        {
            if (attType == null)
            {

                // this is the first typed attribute;
                // create an array for the types, and set all previous attributes to untyped
                attType = new ISimpleType[attParent.Length];
                ArrayTools.Fill(attType, 0, numberOfAttributes, BuiltInAtomicType.UNTYPED_ATOMIC); //            for (int i=0; i<numberOfAttributes; i++) {
                //                attTypeCode[i] = StandardNames.XDT_UNTYPED_ATOMIC;
                //            }
            }
        }

        public void MarkDefaultedAttribute(int attNr)
        {
            if (defaultedAttributes == null)
            {
                defaultedAttributes = new IntHashSet();
            }

            defaultedAttributes.Add(attNr);
        }

        public void IndexIDElement(NodeInfo root, int nodeNr)
        {
            string id = Whitespace.Trim(TinyParentNodeImpl.GetStringValue(this, nodeNr).Tidy()).ToString();
            if (root.GetNodeKind() == Types.Type.DOCUMENT && NameChecker.IsValidNCName(id))
            {
                NodeInfo e = GetNode(nodeNr);
                RegisterID(e, id);
            }
        }

        public bool HasXmlSpacePreserveAttribute()
        {
            for (int i = 0; i < numberOfAttributes; i++)
            {
                if ((attCode[i] & NamePool.FP_MASK) == StandardNames.XML_SPACE && attValue[i].ToString() == "preserve")
                {
                    return true;
                }
            }

            return false;
        }

        public void AddNamespaces(int parent, NamespaceMap nsMap)
        {
            usesNamespaces = true;
            if (nsMap == lastAddedNsMap)
            {
                beta[parent] = lastAddedNsIndex;
                return;
            }

            // reuse existing entry if possible
            for (int i = 0; i < numberOfNamespaces; i++)
            {
                if (namespaceMaps[i].Equals(nsMap))
                {
                    beta[parent] = i;
                    lastAddedNsMap = nsMap;
                    lastAddedNsIndex = i;
                    return;
                }
            }

            EnsureNamespaceCapacity(1);
            namespaceMaps[numberOfNamespaces] = nsMap;
            beta[parent] = numberOfNamespaces;
            lastAddedNsMap = nsMap;
            lastAddedNsIndex = numberOfNamespaces;
            numberOfNamespaces++;
            int bindings = nsMap.Size();
            if (bindings >= BigNamespaceMap)
            {
                namespaceBytes += 80 + 16L * bindings;
                Grown();
            }
        }

        public TinyNodeImpl GetNode(int nr)
        {
            switch ((short)nodeKind[nr])
            {
                case Types.Type.DOCUMENT:
                    return (TinyDocumentImpl)GetRootNode();
                case Types.Type.ELEMENT:
                    return new TinyElementImpl(this, nr);
                case Types.Type.TEXTUAL_ELEMENT:
                    return new TinyTextualElement(this, nr);
                case Types.Type.TEXT:
                    return new TinyTextImpl(this, nr);
                case Types.Type.WHITESPACE_TEXT:
                    return new WhitespaceTextImpl(this, nr);
                case Types.Type.COMMENT:
                    return new TinyCommentImpl(this, nr);
                case Types.Type.PROCESSING_INSTRUCTION:
                    return new TinyProcInstImpl(this, nr);
                case Types.Type.PARENT_POINTER:
                    throw new ArgumentException("Attempting to treat a parent pointer as a node");
                case Types.Type.STOPPER:
                    throw new ArgumentException("Attempting to treat a stopper entry as a node");
                default:
                    throw new InvalidOperationException("Unknown node kind " + nodeKind[nr]);
            }
        }

        public AtomicValue GetAtomizedValueOfUntypedNode(int nodeNr)
        {
            switch ((short)nodeKind[nodeNr])
            {
                case Types.Type.ELEMENT:
                case Types.Type.DOCUMENT:
                    int level = depth[nodeNr];
                    int next = nodeNr + 1;

                    // we optimize two special cases: firstly, where the node has no children, and secondly,
                    // where it has a single text node as a child.
                    if (depth[next] <= level)
                    {
                        return StringValue.ZERO_LENGTH_UNTYPED;
                    }
                    else if (nodeKind[next] == Types.Type.TEXT && depth[next + 1] <= level)
                    {

                        int length = beta[next];
                        int start = alpha[next];
                        return StringValue.MakeUntypedAtomic(textBuffer.Substring(start, start + length));
                    }
                    else if (nodeKind[next] == Types.Type.WHITESPACE_TEXT && depth[next + 1] <= level)
                    {
                        long compressedValue = ((long)alpha[next] << 32) | ((long)beta[next] & 0xffffffff);
                        return StringValue.MakeUntypedAtomic(new CompressedWhitespace(compressedValue));
                    }


                    // Now handle the general case
                    UnicodeBuilder sb = new UnicodeBuilder();
                    while (next < numberOfNodes && depth[next] > level)
                    {
                        if (nodeKind[next] == Types.Type.TEXT)
                        {
                            sb.Accept(TinyTextImpl.GetStringValue(this, next));
                        }
                        else if (nodeKind[next] == Types.Type.WHITESPACE_TEXT)
                        {
                            WhitespaceTextImpl.AppendStringValue(this, next, sb);
                        }

                        next++;
                    }

                    return sb.ToStringItem(BuiltInAtomicType.UNTYPED_ATOMIC);
                case Types.Type.TEXT:
                    return new StringValue(TinyTextImpl.GetStringValue(this, nodeNr));
                case Types.Type.WHITESPACE_TEXT:
                    {
                        long compressedValue = ((long)alpha[nodeNr] << 32) | ((long)beta[nodeNr] & 0xffffffff);
                        return StringValue.MakeUntypedAtomic(new CompressedWhitespace(compressedValue));
                    }

                case Types.Type.COMMENT:
                case Types.Type.PROCESSING_INSTRUCTION:
                    {
                        int start2 = alpha[nodeNr];
                        int len2 = beta[nodeNr];
                        if (len2 == 0)
                        {
                            return StringValue.ZERO_LENGTH_UNTYPED;
                        }

                        char[] dest = new char[len2];
                        return new StringValue(commentBuffer.Substring(start2, start2 + len2));
                    }

                default:
                    throw new InvalidOperationException("Unknown node kind");
            }
        }

        public ISimpleType GetAttributeType(int nr)
        {
            if (attType == null)
            {
                return BuiltInAtomicType.UNTYPED_ATOMIC;
            }
            else
            {
                return attType[nr];
            }
        }

        public bool IsIdAttribute(int nr)
        {
            try
            {
                return attType != null && GetAttributeType(nr).IsIdType();
            }
            catch (MissingComponentException e)
            {
                return false;
            }
        }

        public bool IsIdrefAttribute(int nr)
        {
            return idRefAttributes != null && idRefAttributes.Contains(nr);
        }

        public bool IsIdElement(int nr)
        {
            try
            {
                return GetSchemaType(nr).IsIdType() && GetTypedValueOfElement(nr).GetLength() == 1;
            }
            catch (XPathException e)
            {
                return false;
            }
        }

        public bool IsIdrefElement(int nr)
        {
            ISchemaType type = GetSchemaType(nr);
            try
            {
                if (type.IsIdRefType())
                {
                    if (type == BuiltInAtomicType.IDREF || type == BuiltInListType.IDREFS)
                    {
                        return true;
                    }

                    try
                    {
                        foreach (AtomicValue av in GetTypedValueOfElement(nr))
                        {
                            if (av.GetItemType().IsIdRefType())
                            {
                                return true;
                            }
                        }
                    }
                    catch (XPathException err)
                    {
                    }
                }
            }
            catch (MissingComponentException e)
            {
                return false;
            }

            return false;
        }

        public void SetSystemId(int seq, string uri)
        {
            if (uri == null)
            {
                uri = "";
            }

            if (systemIdMap == null)
            {
                systemIdMap = new SystemIdMap();
            }

            systemIdMap.SetSystemId(seq, uri);
        }

        public string GetSystemId(int seq)
        {
            if (systemIdMap == null)
            {
                return null;
            }

            return systemIdMap.GetSystemId(seq);
        }

        public override NodeInfo GetRootNode()
        {
            if (GetNodeKind(0) == Types.Type.DOCUMENT)
            {
                if (documentRoot != null)
                {
                    return documentRoot;
                }
                else
                {
                    documentRoot = new TinyDocumentImpl(this);
                    return documentRoot;
                }
            }
            else
            {
                return GetNode(0);
            }
        }

        public void SetLineNumbering()
        {
            lineNumbers = new int[nodeKind.Length];
            ArrayTools.Fill(lineNumbers, -1);
            columnNumbers = new int[nodeKind.Length];
            ArrayTools.Fill(columnNumbers, -1);
        }

        public void SetLineNumber(int sequence, int line, int column)
        {
            if (lineNumbers != null)
            {
                lineNumbers[sequence] = line;
                columnNumbers[sequence] = column;
            }
        }

        public int GetLineNumber(int sequence)
        {
            if (lineNumbers != null)
            {

                // find the nearest preceding node that has a known line number, and return it
                for (int i = sequence; i >= 0; i--)
                {
                    int c = lineNumbers[i];
                    if (c > 0)
                    {
                        return c;
                    }
                }
            }

            return -1;
        }

        public int GetColumnNumber(int sequence)
        {
            if (columnNumbers != null)
            {

                // find the nearest preceding node that has a known column number, and return it
                for (int i = sequence; i >= 0; i--)
                {
                    int c = columnNumbers[i];
                    if (c > 0)
                    {
                        return c;
                    }
                }
            }

            return -1;
        }

        public void SetNilled(int nodeNr)
        {
            if (nilledElements == null)
            {
                nilledElements = new IntHashSet();
            }

            nilledElements.Add(nodeNr);
        }

        public bool IsNilled(int nodeNr)
        {
            return nilledElements != null && nilledElements.Contains(nodeNr);
        }

        void RegisterID(NodeInfo e, string id)
        {
            if (idTable == null)
            {
                idTable = new Dictionary<string, NodeInfo>(256);
            }


            // the XPath spec (5.2.1) says ignore the second ID if it's not unique
            idTable.PutIfAbsent(id, e);
        }

        public override NodeInfo SelectID(string id, bool getParent)
        {
            if (idTable == null)
            {
                return null; // no ID values found
            }

            NodeInfo node = idTable.GetOrDefault(id);
            if (node != null && getParent && node.IsId() && node.GetStringValue().Equals(id))
            {
                node = node.GetParent();
            }

            return node;
        }

        public void SetUnparsedEntity(string name, string uri, string publicId)
        {
            if (entityTable == null)
            {
                entityTable = new Dictionary<string, string[]>(20);
            }

            string[] ids = new string[2];
            ids[0] = uri;
            ids[1] = publicId;
            entityTable[name] = ids;
        }

        public override String[] GetUnparsedEntity(string name)
        {
            if (entityTable == null)
            {
                return null;
            }

            return entityTable.GetOrDefault(name);
        }

        public NamePool GetNamePool()
        {
            return GetConfiguration().GetNamePool();
        }

        public void MarkTopWithinEntity(int nodeNr)
        {
            if (topWithinEntity == null)
            {
                topWithinEntity = new IntHashSet();
            }

            topWithinEntity.Add(nodeNr);
        }

        public bool IsTopWithinEntity(int nodeNr)
        {
            return topWithinEntity != null && topWithinEntity.Contains(nodeNr);
        }

        public void ShowSize(Logger logger)
        {
            logger.Info("Tree size: " + numberOfNodes + " nodes, " + textBuffer.Length() + " characters, " + numberOfAttributes + " attributes");
        }

        public override bool IsTyped()
        {
            return typeArray != null;
        }
        NodeInfo INodeVectorTree.GetNode(int arg0) => GetNode(arg0); // was => default (null), breaking NodeTest.GetMatcher fallback
    }
}
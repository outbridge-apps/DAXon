////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Transformation
{
    public class SimpleMode : Mode
    {
        private readonly object syncLock = new object();
        protected readonly RuleChain genericRuleChain = new RuleChain();
        // Real template rules registered (hasRules is also true for a merely DECLARED mode)
        private int templateRuleCount = 0;
        protected RuleChain atomicValueRuleChain = new RuleChain();
        protected RuleChain functionItemRuleChain = new RuleChain();
        protected RuleChain documentRuleChain = new RuleChain();
        protected RuleChain textRuleChain = new RuleChain();
        protected RuleChain commentRuleChain = new RuleChain();
        protected RuleChain processingInstructionRuleChain = new RuleChain();
        protected RuleChain namespaceRuleChain = new RuleChain();
        protected RuleChain unnamedElementRuleChain = new RuleChain();
        protected RuleChain unnamedAttributeRuleChain = new RuleChain();
        protected IntHashMap<RuleChain> namedElementRuleChains = new IntHashMap<RuleChain>(32);
        protected IntHashMap<RuleChain> namedAttributeRuleChains = new IntHashMap<RuleChain>(8);
        protected Dictionary<StructuredQName, RuleChain> qNamedElementRuleChains;
        protected Dictionary<StructuredQName, RuleChain> qNamedAttributeRuleChains;
        private IBuiltInRuleSet builtInRuleSet = TextOnlyCopyRuleSet.GetInstance();
        private Rule mostRecentRule;
        private int mostRecentModuleHash;
        private int stackFrameSlotsNeeded = 0;
        private int highestRank;
        private readonly Dictionary<string, int> explicitPropertyPrecedences = new Dictionary<string, int>();
        private readonly Dictionary<string, string> explicitPropertyValues = new Dictionary<string, string>();

        public override SimpleMode ActivePart => this;

        public virtual string Label => IsUnnamedMode() ? "the unnamed mode" : "mode " + modeName.DisplayName;

        public override int MaxPrecedence
        {
            get
            {
                try
                {
                    IList<int> capturedPrecedence = new List<int>(1);
                    capturedPrecedence.Add(0);
                    ProcessRules((r) =>
                    {
                        if (r.Precedence > capturedPrecedence[0])
                        {
                            capturedPrecedence[0] = r.Precedence;
                        }
                    });
                    return capturedPrecedence[0];
                }
                catch (XPathException e)
                {
                    throw new InvalidOperationException(e.Message, e);
                }
            }
        }

        public override int MaxRank => highestRank;
        public SimpleMode(StructuredQName modeName) : base(modeName)
        {
        }

        public virtual void SetBuiltInRuleSet(IBuiltInRuleSet defaultRules)
        {
            this.builtInRuleSet = defaultRules;
            hasRules = true; // if mode is explicitly declared, treat it as containing rules
        }

        public override IBuiltInRuleSet GetBuiltInRuleSet()
        {
            return this.builtInRuleSet;
        }

        public virtual void ResolveProperties(RuleManager manager)
        {
            bool failOnMultipleMatch = false;
            bool warningOnMultipleMatch = true;
            foreach (KeyValuePair<string, string> entry in ActivePart.explicitPropertyValues)
            {
                string prop = entry.Key;
                string value = entry.Value;
                if (value.Equals("##conflict##"))
                {
                    throw new XPathException("For " + Label + ", there are conflicting values for xsl:mode/@" + prop + " at the same import precedence", "XTSE0545").MaybeWithLocation(ActivePart);
                }

                switch (prop)
                {
                    case "streamable":
                        bool streamable = value == "yes";
                        SetStreamable(streamable);

                        //                    if (streamable) {
                        //                        Mode omniMode = manager.obtainMode(Mode.OMNI_MODE, true);
                        //                    }
                        break;
                    case "typed":
                        mustBeTyped = value == "yes" || value == "strict" || value == "lax";
                        mustBeUntyped = value == "no";
                        break;
                    case "on-no-match":
                        IBuiltInRuleSet @base = null;
                        switch (value)
                        {
                            case "text-only-copy":
                                @base = TextOnlyCopyRuleSet.GetInstance();
                                break;
                            case "shallow-copy":
                                @base = ShallowCopyRuleSet.GetInstance();
                                break;
                            case "deep-copy":
                                @base = DeepCopyRuleSet.GetInstance();
                                break;
                            case "shallow-skip":
                                @base = ShallowSkipRuleSet.GetInstance();
                                break;
                            case "deep-skip":
                                @base = DeepSkipRuleSet.GetInstance();
                                break;
                            case "fail":
                                @base = FailRuleSet.GetInstance();
                                break;
                            case "shallow-copy-all":
                                @base = ShallowCopyAllRuleSet.GetInstance();
                                break;
                            default:

                                // already validated
                                break;
                        }

                        if (explicitPropertyValues.GetOrDefault("warning-on-no-match") == "yes")
                        {
                            @base = new RuleSetWithWarnings(@base);
                        }

                        SetBuiltInRuleSet(@base);
                        break;
                    case "on-multiple-match":
                        if (value.Equals("fail"))
                        {
                            failOnMultipleMatch = true;
                        }

                        break;
                    case "warning-on-multiple-match":
                        warningOnMultipleMatch = value.Equals("yes");
                        break;
                    case "use-accumulators":
                        AccumulatorRegistry registry = manager.GetStylesheetPackage().AccumulatorRegistry;
                        HashSet<Accumulator> accumulators = new HashSet<Accumulator>();
                        if (!(value.Length == 0))
                        {
                            string[] tokens = value.SplitRegex("[ \t\r\n]+");
                            foreach (string eqname in tokens)
                            {
                                Accumulator acc = registry.GetAccumulator(StructuredQName.FromEQName(eqname));
                                accumulators.Add(acc);
                            }
                        }

                        Accumulators = accumulators;
                        break;
                }
            }

            if (failOnMultipleMatch)
            {
                RecoveryPolicy = RecoveryPolicy.DO_NOT_RECOVER;
            }
            else if (warningOnMultipleMatch)
            {
                RecoveryPolicy = RecoveryPolicy.RECOVER_WITH_WARNINGS;
            }
            else
            {
                RecoveryPolicy = RecoveryPolicy.RECOVER_SILENTLY;
            }
        }

        protected virtual RuleSearchState MakeRuleSearchState(RuleChain chain, IXPathContext context)
        {
            return RuleSearchState.GetInstance();
        }

        public override bool IsEmpty()
        {
            return !hasRules;
        }

        public override bool HasNoTemplateRules => templateRuleCount == 0;

        public virtual void SetExplicitProperty(string name, string value, int precedence)
        {
            int p = explicitPropertyPrecedences.GetOrDefault(name, int.MinValue);
            if (p != int.MinValue)
            {
                if (p < precedence)
                {
                    explicitPropertyPrecedences[name] = precedence;
                    explicitPropertyValues[name] = value;
                }
                else if (p == precedence)
                {
                    string v = explicitPropertyValues.GetOrDefault(name);
                    if (v != null & !v.Equals(value))
                    {

                        // We don't throw an exception, because the conflict is an error only if this
                        // is the highest-precedence declaration of this mode
                        explicitPropertyValues.PutAndGetPrevious(name, "##conflict##");
                    }
                }
                else
                {
                }
            }
            else
            {
                explicitPropertyPrecedences[name] = precedence;
                explicitPropertyValues[name] = value;
            }

            string typed = explicitPropertyValues.GetOrDefault("typed");
            mustBeTyped = typed == "yes" || typed == "strict" || typed == "lax";
            mustBeUntyped = typed == "no";
        }

        public virtual string GetPropertyValue(string name)
        {
            return explicitPropertyValues != null && explicitPropertyValues.TryGetValue(name, out var __v) ? __v : null;
        }

        public override HashSet<NamespaceUri> GetExplicitNamespaces(NamePool pool)
        {
            HashSet<NamespaceUri> namespaces = new HashSet<NamespaceUri>();
            IIntIterator ii = namedElementRuleChains.KeyIterator();
            while (ii.MoveNext())
            {
                int fp = ii.Current;
                namespaces.Add(pool.GetURI(fp));
            }

            return namespaces;
        }

        public virtual void AddRule(Patterns.Pattern pattern, IRuleTarget action, StylesheetModule module, int precedence, double priority, int position, int part)
        {
            hasRules = true;

            // Ignore a pattern that will never match, e.g. "@comment"
            if (pattern.GetItemType() is ErrorType)
            {
                return;
            }

            // hasRules also covers "mode explicitly declared"; this counts REAL template rules
            // (gates the bulk shallow-copy fast path in Mode.ApplyTemplates)
            templateRuleCount++;


            // for fast lookup, we maintain one list for each element name for patterns that can only
            // match elements of a given name, one list for each node type for patterns that can only
            // match one kind of non-element node, and one generic list.
            // Each list is sorted in precedence/priority order so we find the highest-priority rule first
            // This logic is designed to ensure that when a UnionPattern contains multiple branches
            // with the same priority, next-match doesn't select the same template twice (next-match-024)
            int moduleHash = module.GetHashCode();

            //        int sequence;
            //        if (mostRecentRule == null) {
            //            sequence = 0;
            int minImportPrecedence = module.MinImportPrecedence;
            Rule newRule = MakeRule(pattern, action, precedence, minImportPrecedence, priority, position, part);
            if (pattern is NodeTestPattern)
            {
                ItemType test = pattern.GetItemType();
                if (test is AnyNodeTest)
                {
                    newRule.SetAlwaysMatches(true);
                }
                else if (test is NodeKindTest)
                {
                    newRule.SetAlwaysMatches(true);
                }
                else if (test is NameTest)
                {
                    int kind = test.PrimitiveType;
                    if (kind == Types.Type.ELEMENT || kind == Types.Type.ATTRIBUTE)
                    {
                        newRule.SetAlwaysMatches(true);
                    }
                }
            }

            mostRecentRule = newRule;
            mostRecentModuleHash = moduleHash;
            AddRule(pattern, newRule);
        }

        public virtual Rule MakeRule(Patterns.Pattern pattern, IRuleTarget action, int precedence, int minImportPrecedence, double priority, int sequence, int part)
        {
            return new Rule(pattern, action, precedence, minImportPrecedence, priority, sequence, part);
        }

        public virtual void AddRule(Patterns.Pattern pattern, Rule newRule)
        {
            UType uType = pattern.GetUType();
            if (uType.Equals(UType.ELEMENT))
            {
                int fp = pattern.Fingerprint;
                AddRuleToNamedOrUnnamedChain(newRule, fp, unnamedElementRuleChain, namedElementRuleChains);
            }
            else if (uType.Equals(UType.ATTRIBUTE))
            {
                int fp = pattern.Fingerprint;
                AddRuleToNamedOrUnnamedChain(newRule, fp, unnamedAttributeRuleChain, namedAttributeRuleChains);
            }
            else if (uType.Equals(UType.DOCUMENT))
            {
                AddRuleToList(newRule, documentRuleChain);
            }
            else if (uType.Equals(UType.TEXT))
            {
                AddRuleToList(newRule, textRuleChain);
            }
            else if (uType.Equals(UType.COMMENT))
            {
                AddRuleToList(newRule, commentRuleChain);
            }
            else if (uType.Equals(UType.PI))
            {
                AddRuleToList(newRule, processingInstructionRuleChain);
            }
            else if (uType.Equals(UType.NAMESPACE))
            {
                AddRuleToList(newRule, namespaceRuleChain);
            }
            else if (UType.ANY_ATOMIC.Subsumes(uType))
            {
                AddRuleToList(newRule, atomicValueRuleChain);
            }
            else if (UType.FUNCTION.Subsumes(uType))
            {
                AddRuleToList(newRule, functionItemRuleChain);
            }
            else
            {
                AddRuleToList(newRule, genericRuleChain);
            }
        }

        protected virtual void AddRuleToNamedOrUnnamedChain(Rule newRule, int fp, RuleChain unnamedRuleChain, IntHashMap<RuleChain> namedRuleChains)
        {
            if (fp == -1)
            {
                AddRuleToList(newRule, unnamedRuleChain);
            }
            else
            {
                RuleChain chain = namedRuleChains[fp];
                if (chain == null)
                {
                    chain = new RuleChain(newRule);
                    namedRuleChains.Put(fp, chain);
                }
                else
                {
                    AddRuleToList(newRule, chain);
                }
            }
        }

        private void AddRuleToList(Rule newRule, RuleChain list)
        {
            if (list.Head() == null)
            {
                list.SetHead(newRule);
            }
            else
            {
                int precedence = newRule.Precedence;
                double priority = newRule.Priority;
                Rule rule = list.Head();
                Rule prev = null;
                while (rule != null)
                {
                    if ((rule.Precedence < precedence) || (rule.Precedence == precedence && rule.Priority <= priority))
                    {
                        newRule.Next = rule;
                        if (prev == null)
                        {
                            list.SetHead(newRule);
                        }
                        else
                        {
                            prev.Next = newRule;
                        }

                        break;
                    }
                    else
                    {
                        prev = rule;
                        rule = rule.Next;
                    }
                }

                if (rule == null)
                {
                    prev.Next = newRule;
                    newRule.Next = null;
                }
            }
        }

        public virtual void AllocatePatternSlots(int slots)
        {
            stackFrameSlotsNeeded = Math.Max(stackFrameSlotsNeeded, slots);
        }

        public override Rule GetRule(IItem item, IXPathContext context)
        {

            // If there are match patterns in the stylesheet that use local variables, we need to allocate
            // a new stack frame for evaluating the match patterns. We base this on the match pattern with
            // the highest number of range variables, so we can reuse the same stack frame for all rules
            // that we test against. If no patterns use range variables, we don't bother allocating a new
            // stack frame.
            // Note, this method isn't functionally necessary; we could call the 3-argument version
            // with a filter that always returns true. But this is the common path for apply-templates,
            // and we want to squeeze every drop of performance from it.
            if (stackFrameSlotsNeeded > 0)
            {
                context = MakeNewContext(context);
            }


            // search the specific list for this node type / node name
            Rule bestRule = null;
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                bestRule = FindBestRuleForNodeInfo(node, context);
            }
            else if (item is AtomicValue)
            {
                if (atomicValueRuleChain != null)
                {
                    bestRule = SearchRuleChain(item, context, bestRule, atomicValueRuleChain);
                }

                bestRule = SearchRuleChain(item, context, bestRule, genericRuleChain);
            }
            else if (item is IFunctionItem)
            {
                if (functionItemRuleChain != null)
                {
                    bestRule = SearchRuleChain(item, context, bestRule, functionItemRuleChain);
                }

                bestRule = SearchRuleChain(item, context, bestRule, genericRuleChain);
            }

            return bestRule;
        }

        // Resolved-rule memo for content-independent element dispatch (W4). When every rule that a
        // fingerprinted element could match by name/wildcard is unconditional (IsAlwaysMatches -- a plain
        // name pattern, no predicate/structure), the rule the search returns is a pure function of the
        // fingerprint: the same for every element of that name. On a template-dispatch pass (millions of
        // elements, a handful of distinct names) that turns the whole per-node chain search
        // (SearchRuleChain + fingerprint lookup + rule-search state) into one dictionary lookup. Any name
        // whose chain carries a predicate rule -- content-dependent -- is marked NotMemoizable and always
        // runs the full search, so mixed modes (some names predicated, some not) still get the win on the
        // unconditional names. The memo is populated with deterministic values (immutable compiled rules),
        // so concurrent transforms racing to fill it are harmless; reads are lock-free.
        private sealed class SentinelRule : Rule
        {
            internal SentinelRule() { }
        }

        // Marks a fingerprint whose element-rule resolution depends on node content: skip the memo, run
        // the full search every time. Compared by reference only; never returned to a caller.
        private static readonly Rule NotMemoizable = new SentinelRule();

        // Bounded (round BG-P3): the memo lives on the Mode, i.e. on the compiled stylesheet a host
        // keeps for years, and it used to grow per distinct element fingerprint with no cap - a
        // workload whose element names encode data walked it toward the NamePool's own ceiling.
        // 1024 covers any real stylesheet's element vocabulary outright (hits stay lock-free CD
        // reads, same as before); only generated-name inputs ever evict, and for those a re-search
        // after eviction is the price of boundedness. Per-Mode deliberately, not static: the memo's
        // content is meaningless outside its owning mode's rule chains.
        private const int ElementRuleMemoCapacity = 1024;
        private volatile Internal.Caching.ClockCache<int, Rule> elementRuleMemo;
        private volatile int elementMemoState;   // 0 = not yet decided, 1 = enabled, 2 = disabled

        // Direct-mapped front cache over elementRuleMemo: the ClockCache hit (concurrent-dictionary
        // probe + ref-bit write) was the hottest frame of pure apply-templates dispatch. The memo
        // value for a (mode, fingerprint) is deterministic forever, so a slot overwritten by a
        // colliding name is never wrong, only re-fetched - and 64 fixed references cannot grow.
        private const int MemoFrontSize = 64;
        private volatile MemoSlot[] memoFront;

        // Text nodes have no name, so when the text and generic chains are unconditional every
        // text node in the mode resolves to the same rule forever — one field replaces the
        // per-node chain searches (half the nodes of a typical dispatch workload are texts).
        // Value written before state (build-then-publish); racing writers store the same rule.
        private volatile Rule textRuleMemoValue;
        private volatile int textMemoState;   // 0 = undecided, 1 = memo valid, 2 = disabled

        private sealed class MemoSlot
        {
            internal readonly int Fp;
            internal readonly Rule Rule;   // null = no rule matches; may also be NotMemoizable

            internal MemoSlot(int fp, Rule rule)
            {
                Fp = fp;
                Rule = rule;
            }
        }

        // The memo can help only if the chains searched for EVERY element (the unnamed-element and generic
        // chains) are themselves unconditional; otherwise a predicate there could change any name's result.
        // The lock lives in a NoInlining helper: net472 refuses to inline methods with EH/locks, and this
        // check runs once per element — the decided-state read must stay inlinable.
        private bool ElementMemoEnabled()
        {
            int s = elementMemoState;
            return s != 0 ? s == 1 : ElementMemoEnabledSlow();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool ElementMemoEnabledSlow()
        {
            lock (syncLock)
            {
                if (elementMemoState == 0)
                {
                    if (ChainAllUnconditional(unnamedElementRuleChain) && ChainAllUnconditional(genericRuleChain))
                    {
                        elementRuleMemo = new Internal.Caching.ClockCache<int, Rule>(ElementRuleMemoCapacity);
                        memoFront = new MemoSlot[MemoFrontSize];
                        elementMemoState = 1;
                    }
                    else
                    {
                        elementMemoState = 2;
                    }
                }
            }

            return elementMemoState == 1;
        }

        // A fingerprint is content-independent when its named chain is unconditional (the shared chains are
        // already known unconditional via ElementMemoEnabled). A fingerprint with no named rule falls
        // through to those shared chains, whose result is likewise deterministic.
        private bool IsFpContentIndependent(int fp)
        {
            return ChainAllUnconditional(namedElementRuleChains[fp]);
        }

        private static bool ChainAllUnconditional(RuleChain chain)
        {
            if (chain == null)
            {
                return true;
            }

            for (Rule r = chain.Head(); r != null; r = r.Next)
            {
                if (!r.IsAlwaysMatches())
                {
                    return false;
                }
            }

            return true;
        }

        private Rule FindBestRuleForNodeInfo(NodeInfo node, IXPathContext context)
        {
            RuleChain unnamedNodeChain;
            Rule bestRule = null;

            int nodeKind;
            if (node is TinyElementImpl)
            {
                nodeKind = Types.Type.ELEMENT;
            }
            else
            {
                nodeKind = node.GetNodeKind();
            }

            int memoFp = -1;
            if (nodeKind == Types.Type.ELEMENT && node.HasFingerprint() && ElementMemoEnabled())
            {
                memoFp = node.Fingerprint;
                MemoSlot[] front = memoFront;
                MemoSlot slot = front[memoFp & (MemoFrontSize - 1)];
                Rule cached;
                bool hit;
                if (slot != null && slot.Fp == memoFp)
                {
                    cached = slot.Rule;
                    hit = true;
                }
                else
                {
                    hit = elementRuleMemo.TryGet(memoFp, out cached);
                    if (hit)
                    {
                        front[memoFp & (MemoFrontSize - 1)] = new MemoSlot(memoFp, cached);
                    }
                }

                if (hit)
                {
                    if (!ReferenceEquals(cached, NotMemoizable))
                    {
                        return cached;   // may be null: this name matches no rule
                    }

                    memoFp = -1;   // known content-dependent: run the full search, do not re-store
                }
            }

            switch (nodeKind)
            {
                case Types.Type.DOCUMENT:
                    unnamedNodeChain = documentRuleChain;
                    break;
                case Types.Type.ELEMENT:
                    {
                        unnamedNodeChain = unnamedElementRuleChain;
                        RuleChain namedNodeChain;
                        if (node.HasFingerprint())
                        {
                            namedNodeChain = namedElementRuleChains[node.Fingerprint];
                        }
                        else
                        {
                            namedNodeChain = GetNamedRuleChain(context, Types.Type.ELEMENT, node.GetNamespaceUri(), node.GetLocalPart());
                        }

                        if (namedNodeChain != null)
                        {
                            bestRule = SearchRuleChain(node, context, null, namedNodeChain);
                        }

                        break;
                    }

                case Types.Type.ATTRIBUTE:
                    {
                        unnamedNodeChain = unnamedAttributeRuleChain;
                        RuleChain namedNodeChain;
                        if (node.HasFingerprint())
                        {
                            namedNodeChain = namedAttributeRuleChains[node.Fingerprint];
                        }
                        else
                        {
                            namedNodeChain = GetNamedRuleChain(context, Types.Type.ATTRIBUTE, node.GetNamespaceUri(), node.GetLocalPart());
                        }

                        if (namedNodeChain != null)
                        {
                            bestRule = SearchRuleChain(node, context, null, namedNodeChain);
                        }

                        break;
                    }

                case Types.Type.TEXT:
                    if (textMemoState == 1)
                    {
                        return textRuleMemoValue;
                    }

                    unnamedNodeChain = textRuleChain;
                    break;
                case Types.Type.COMMENT:
                    unnamedNodeChain = commentRuleChain;
                    break;
                case Types.Type.PROCESSING_INSTRUCTION:
                    unnamedNodeChain = processingInstructionRuleChain;
                    break;
                case Types.Type.NAMESPACE:
                    unnamedNodeChain = namespaceRuleChain;
                    break;
                default:
                    throw new InvalidOperationException("Unknown node kind");
            }


            // search the list for unnamed nodes of a particular kind
            if (unnamedNodeChain != null)
            {
                bestRule = SearchRuleChain(node, context, bestRule, unnamedNodeChain);
            }


            // Search the list for rules for nodes of unknown node kind
            if (genericRuleChain != null)
            {
                bestRule = SearchRuleChain(node, context, bestRule, genericRuleChain);
            }

            if (memoFp >= 0)
            {
                // first resolution for this element name (or first since eviction): cache it if
                // content-independent, else record that it must always be searched. Set rather than
                // TryAdd: racing writers store the same deterministic value, so last-wins is fine.
                Rule memoValue = IsFpContentIndependent(memoFp) ? bestRule : NotMemoizable;
                elementRuleMemo.Set(memoFp, memoValue);
                memoFront[memoFp & (MemoFrontSize - 1)] = new MemoSlot(memoFp, memoValue);
            }

            if (nodeKind == Types.Type.TEXT && textMemoState == 0)
            {
                if (ChainAllUnconditional(textRuleChain) && ChainAllUnconditional(genericRuleChain))
                {
                    textRuleMemoValue = bestRule;
                    textMemoState = 1;
                }
                else
                {
                    textMemoState = 2;
                }
            }

            return bestRule;
        }

        protected virtual RuleChain GetNamedRuleChain(IXPathContext c, int kind, NamespaceUri uri, string local)
        {

            // If this is the first attempt to match a non-fingerprinted node, build indexes
            // to the rule chains based on StructuredQName rather than fingerprint
            lock (syncLock)
            {
                if (qNamedElementRuleChains == null)
                {
                    // Publish only after both indexes are complete: a throw mid-build used to
                    // leave the guard field assigned, so every later call skipped the rebuild
                    // and matched against a permanently half-populated index.
                    var elementChains = new Dictionary<StructuredQName, RuleChain>(namedElementRuleChains.Count);
                    var attributeChains = new Dictionary<StructuredQName, RuleChain>(namedAttributeRuleChains.Count);
                    NamePool pool = c.GetNamePool();
                    IndexByQName(pool, namedElementRuleChains, elementChains);
                    IndexByQName(pool, namedAttributeRuleChains, attributeChains);
                    qNamedAttributeRuleChains = attributeChains;
                    qNamedElementRuleChains = elementChains;
                }
            }

            return (kind == Types.Type.ELEMENT ? qNamedElementRuleChains : qNamedAttributeRuleChains).GetOrDefault(new StructuredQName("", uri, local));
        }

        private static void IndexByQName(NamePool pool, IntHashMap<RuleChain> indexByFP, Dictionary<StructuredQName, RuleChain> indexByQN)
        {
            IIntIterator ii = indexByFP.KeyIterator();
            while (ii.MoveNext())
            {
                int fp = ii.Current;
                RuleChain eChain = indexByFP[fp];
                StructuredQName name = pool.GetStructuredQName(fp);
                indexByQN[name] = eChain;
            }
        }

        protected virtual Rule SearchRuleChain(IItem item, IXPathContext context, Rule bestRule, RuleChain chain)
        {
            // An empty (or null) chain contributes nothing: bail before the major-context walk and the
            // rule-search state fetch. On apply-templates most nodes have no rules on their unnamed and
            // generic chains -- and those chains are non-null empty RuleChains, not null -- so two of the
            // (up to three) per-node calls hit this and would otherwise pay both for nothing.
            Rule head = chain == null ? null : chain.Head();
            if (head == null)
            {
                return bestRule;
            }

            context = context.MajorContext;

            // Get the rule search state object - this could be reusable within a rule chain.
            RuleSearchState ruleSearchState = MakeRuleSearchState(chain, context);
            while (head != null)
            {
                if (bestRule != null)
                {
                    int rank = head.CompareRank(bestRule);
                    if (rank < 0)
                    {

                        // if we already have a match, and the precedence or priority of this
                        // rule is lower, quit the search
                        break;
                    }
                    else if (rank == 0)
                    {

                        // this rule has the same precedence and priority as the matching rule already found
                        if (RuleMatches(head, item, (XPathContextMajor)context, ruleSearchState))
                        {
                            if (head.Sequence != bestRule.Sequence)
                            {
                                ReportAmbiguity(item, bestRule, head, context);
                            }


                            // choose whichever one comes last (assuming the error wasn't fatal)
                            int seqComp = bestRule.Sequence.CompareTo(head.Sequence);
                            if (seqComp > 0)
                            {
                                return bestRule;
                            }
                            else if (seqComp < 0)
                            {
                                return head;
                            }
                            else
                            {

                                // we're dealing with two rules formed by partitioning a union pattern
                                bestRule = bestRule.PartNumber > head.PartNumber ? bestRule : head;
                            }

                            break;
                        }
                        else
                        {
                        }
                    }
                    else
                    {

                        // this rule has higher rank than the matching rule already found
                        if (RuleMatches(head, item, (XPathContextMajor)context, ruleSearchState))
                        {
                            bestRule = head;
                        }
                    }
                }
                else if (RuleMatches(head, item, (XPathContextMajor)context, ruleSearchState))
                {
                    bestRule = head;
                    if (RecoveryPolicy == RecoveryPolicy.RECOVER_SILENTLY)
                    {

                        break; // choose the first match; rules within a chain are in order of rank
                    }
                }


                //ruleSearchState.count();// Keep tab of the number of checks
                head = head.Next;
            }

            return bestRule;
        }

        protected virtual bool RuleMatches(Rule r, IItem item, XPathContextMajor context, RuleSearchState pre)
        {
            return r.IsAlwaysMatches() || r.Matches(item, context);
        }

        public override Rule GetRule(IItem item, IXPathContext context, Func<Rule, bool> filter)
        {

            // If there are match patterns in the stylesheet that use local variables, we need to allocate
            // a new stack frame for evaluating the match patterns. We base this on the match pattern with
            // the highest number of range variables, so we can reuse the same stack frame for all rules
            // that we test against. If no patterns use range variables, we don't bother allocating a new
            // stack frame.
            if (stackFrameSlotsNeeded > 0)
            {
                context = MakeNewContext(context);
            }


            // Get the rule search state object
            RuleSearchState ruleSearchState;

            // search the specific list for this node type / node name
            Rule bestRule = null;
            RuleChain unnamedNodeChain;

            // Search the list for unnamed nodes of a particular kind
            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                switch (node.GetNodeKind())
                {
                    case Types.Type.DOCUMENT:
                        unnamedNodeChain = documentRuleChain;
                        break;
                    case Types.Type.ELEMENT:
                        {
                            unnamedNodeChain = unnamedElementRuleChain;
                            RuleChain namedNodeChain;
                            if (node.HasFingerprint())
                            {
                                namedNodeChain = namedElementRuleChains[node.Fingerprint];
                            }
                            else
                            {
                                namedNodeChain = GetNamedRuleChain(context, Types.Type.ELEMENT, node.GetNamespaceUri(), node.GetLocalPart());
                            }

                            if (namedNodeChain != null)
                            {
                                ruleSearchState = MakeRuleSearchState(namedNodeChain, context);
                                bestRule = SearchRuleChain(item, context, null, namedNodeChain, ruleSearchState, filter);
                            }

                            break;
                        }

                    case Types.Type.ATTRIBUTE:
                        {
                            unnamedNodeChain = unnamedAttributeRuleChain;
                            RuleChain namedNodeChain;
                            if (node.HasFingerprint())
                            {
                                namedNodeChain = namedAttributeRuleChains[node.Fingerprint];
                            }
                            else
                            {
                                namedNodeChain = GetNamedRuleChain(context, Types.Type.ATTRIBUTE, node.GetNamespaceUri(), node.GetLocalPart());
                            }

                            if (namedNodeChain != null)
                            {
                                ruleSearchState = MakeRuleSearchState(namedNodeChain, context);
                                bestRule = SearchRuleChain(item, context, null, namedNodeChain, ruleSearchState, filter);
                            }

                            break;
                        }

                    case Types.Type.TEXT:
                        unnamedNodeChain = textRuleChain;
                        break;
                    case Types.Type.COMMENT:
                        unnamedNodeChain = commentRuleChain;
                        break;
                    case Types.Type.PROCESSING_INSTRUCTION:
                        unnamedNodeChain = processingInstructionRuleChain;
                        break;
                    case Types.Type.NAMESPACE:
                        unnamedNodeChain = namespaceRuleChain;
                        break;
                    default:
                        throw new InvalidOperationException("Unknown node kind");
                }

                ruleSearchState = MakeRuleSearchState(unnamedNodeChain, context);
                bestRule = SearchRuleChain(item, context, bestRule, unnamedNodeChain, ruleSearchState, filter);

                // Search the list for rules for nodes of unknown node kind
                ruleSearchState = MakeRuleSearchState(genericRuleChain, context);
                return SearchRuleChain(item, context, bestRule, genericRuleChain, ruleSearchState, filter);
            }
            else if (item is AtomicValue)
            {
                if (atomicValueRuleChain != null)
                {
                    ruleSearchState = MakeRuleSearchState(atomicValueRuleChain, context);
                    bestRule = SearchRuleChain(item, context, bestRule, atomicValueRuleChain, ruleSearchState, filter);
                }

                ruleSearchState = MakeRuleSearchState(genericRuleChain, context);
                bestRule = SearchRuleChain(item, context, bestRule, genericRuleChain, ruleSearchState, filter);
                return bestRule;
            }
            else if (item is IFunctionItem)
            {
                if (functionItemRuleChain != null)
                {
                    ruleSearchState = MakeRuleSearchState(functionItemRuleChain, context);
                    bestRule = SearchRuleChain(item, context, bestRule, functionItemRuleChain, ruleSearchState, filter);
                }

                ruleSearchState = MakeRuleSearchState(genericRuleChain, context);
                bestRule = SearchRuleChain(item, context, bestRule, genericRuleChain, ruleSearchState, filter);
                return bestRule;
            }
            else
            {
                return null;
            }
        }

        protected virtual Rule SearchRuleChain(IItem item, IXPathContext context, Rule bestRule, RuleChain chain, RuleSearchState ruleSearchState, Func<Rule, bool> filter)
        {
            Rule head = chain == null ? null : chain.Head();
            while (!(context is XPathContextMajor))
            {
                context = context.GetCaller();
            }

            while (head != null)
            {
                if (filter == null || filter(head))
                {
                    if (bestRule != null)
                    {
                        int rank = head.CompareRank(bestRule);
                        if (rank < 0)
                        {

                            // if we already have a match, and the precedence or priority of this
                            // rule is lower, quit the search
                            break;
                        }
                        else if (rank == 0)
                        {

                            // this rule has the same precedence and priority as the matching rule already found
                            if (RuleMatches(head, item, (XPathContextMajor)context, ruleSearchState))
                            {
                                ReportAmbiguity(item, bestRule, head, context);

                                // choose whichever one comes last (assuming the error wasn't fatal)
                                bestRule = bestRule.Sequence > head.Sequence ? bestRule : head;
                                break;
                            }
                            else
                            {
                            }
                        }
                        else
                        {

                            // this rule has higher rank than the matching rule already found
                            if (RuleMatches(head, item, (XPathContextMajor)context, ruleSearchState))
                            {
                                bestRule = head;
                            }
                        }
                    }
                    else if (RuleMatches(head, item, (XPathContextMajor)context, ruleSearchState))
                    {
                        bestRule = head;
                        if (RecoveryPolicy == RecoveryPolicy.RECOVER_SILENTLY)
                        {
                            break; // choose the first match; rules within a chain are in order of rank
                        }
                    }
                }

                head = head.Next;
            }

            return bestRule;
        }

        protected virtual void ReportAmbiguity(IItem item, Rule r1, Rule r2, IXPathContext c)
        {

            // Save the effort of constructing the message if it's not going to be reported anyway
            if (RecoveryPolicy == RecoveryPolicy.RECOVER_SILENTLY)
            {
                return;
            }


            // don't report an error if the conflict is between two branches of the same Union pattern
            if (r1.GetAction() == r2.GetAction() && r1.Sequence == r2.Sequence)
            {
                return;
            }

            string path;
            string errorCode = "XTDE0540";
            if (item is NodeInfo)
            {
                path = Navigator.GetPath((NodeInfo)item);
            }
            else
            {
                path = item.ToShortString();
            }

            Patterns.Pattern pat1 = r1.Pattern;
            Patterns.Pattern pat2 = r2.Pattern;
            string message;
            if (r1.GetAction() == r2.GetAction())
            {
                message = "Ambiguous rule match for " + path + ". " + "Matches \"" + ShowPattern(pat1) + "\" on line " + pat1.GetLocation().GetLineNumber() + " of " + pat1.GetLocation().GetSystemId() + ", a rule which appears in the stylesheet more than once, because the containing module was included more than once";
            }
            else
            {
                message = "Ambiguous rule match for " + path + '\n' + "Matches both \"" + ShowPattern(pat1) + "\" on line " + pat1.GetLocation().GetLineNumber() + " of " + pat1.GetLocation().GetSystemId() + "\nand \"" + ShowPattern(pat2) + "\" on line " + pat2.GetLocation().GetLineNumber() + " of " + pat2.GetLocation().GetSystemId();
            }

            switch (RecoveryPolicy)
            {
                case RecoveryPolicy.DO_NOT_RECOVER:
                    throw new XPathException(message, errorCode, GetLocation());
                case RecoveryPolicy.RECOVER_WITH_WARNINGS:
                    c.GetController().Warning(message, errorCode, GetLocation());
                    break;
                case RecoveryPolicy.RECOVER_SILENTLY:
                default:
                    break;
            }
        }

        private static string ShowPattern(Patterns.Pattern p)
        {

            // Complex patterns can be laid out with lots of whitespace, which looks messy in the error message
            return Whitespace.CollapseWhitespace(StringView.Of(p.ToShortString()).Tidy()).ToString();
        }

        public virtual void PrepareStreamability()
        {
        }

        public override void AllocateAllBindingSlots(StylesheetPackage pack)
        {
            if (GetDeclaringComponent().DeclaringPackage == pack && !bindingSlotsAllocated)
            {
                ForceAllocateAllBindingSlots(pack, this, GetDeclaringComponent().ComponentBindings);
                bindingSlotsAllocated = true;
            }
        }

        public static void ForceAllocateAllBindingSlots(StylesheetPackage pack, SimpleMode mode, IList<ComponentBinding> bindings)
        {
            HashSet<TemplateRule> rulesProcessed = new HashSet<TemplateRule>();
            Dictionary<Patterns.Pattern, bool> patternsProcessed = new Dictionary<Patterns.Pattern, bool>();
            try
            {
                mode.ProcessRules((r) =>
                {

                    // A rule can appear twice, for example at different import precedences or
                    // because the match pattern is a union pattern; only allocate slots once
                    Patterns.Pattern pattern = r.Pattern;
                    if (!patternsProcessed.ContainsKey(pattern))
                    {
                        AllocateBindingSlotsRecursive(pack, mode, pattern, bindings);
                        patternsProcessed[pattern] = true;
                    }

                    TemplateRule tr = (TemplateRule)r.GetAction();
                    if (tr.GetBody() != null && !rulesProcessed.Contains(tr))
                    {
                        AllocateBindingSlotsRecursive(pack, mode, tr.GetBody(), bindings);
                        rulesProcessed.Add(tr);
                    }
                });
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }
        }

        public virtual void ComputeStreamability()
        {
        }

        public virtual void InvertStreamableTemplates()
        {
        }

        public override void ExplainTemplateRules(ExpressionPresenter @out)
        {
            IRuleAction action = (r) => r.Export(@out, IsDeclaredStreamable());
            try
            {
                ProcessRules(action, new RuleGroupExplainAction(@out));
            }
            catch (XPathException err)
            {
            }
        }

        public override void ExportTemplateRules(ExpressionPresenter @out)
        {

            // TODO: if two rules share the same template, avoid duplicate output. This can happen with union patterns, and also
            // when a template is present in more than one mode.
            ProcessRules((r) => r.Export(@out, IsDeclaredStreamable()));
        }

        public override void ProcessRules(IRuleAction action)
        {
            ProcessRules(action, null);
        }

        public virtual void ProcessRules(IRuleAction action, IRuleGroupAction group)
        {
            ProcessRuleChain(documentRuleChain, action, SetGroup(group, "document-node()"));
            ProcessRuleChain(unnamedElementRuleChain, action, SetGroup(group, "element()"));
            ProcessRuleChains(namedElementRuleChains, action, SetGroup(group, "namedElements"));
            ProcessRuleChain(unnamedAttributeRuleChain, action, SetGroup(group, "attribute()"));
            ProcessRuleChains(namedAttributeRuleChains, action, SetGroup(group, "namedAttributes"));
            ProcessRuleChain(textRuleChain, action, SetGroup(group, "text()"));
            ProcessRuleChain(commentRuleChain, action, SetGroup(group, "comment()"));
            ProcessRuleChain(processingInstructionRuleChain, action, SetGroup(group, "processing-instruction()"));
            ProcessRuleChain(namespaceRuleChain, action, SetGroup(group, "namespace()"));
            ProcessRuleChain(genericRuleChain, action, SetGroup(group, "node()"));
            ProcessRuleChain(atomicValueRuleChain, action, SetGroup(group, "atomicValue"));
            ProcessRuleChain(functionItemRuleChain, action, SetGroup(group, "function()"));
        }

        protected virtual IRuleGroupAction SetGroup(IRuleGroupAction group, string type)
        {
            if (group != null)
            {
                group.SetLabel(type);
            }

            return group;
        }

        public virtual void ProcessRuleChains(IntHashMap<RuleChain> chains, IRuleAction action, IRuleGroupAction group)
        {
            if (chains.Count > 0)
            {
                if (group != null)
                {
                    group.Start();
                }

                IIntIterator ii = chains.KeyIterator();
                while (ii.MoveNext())
                {
                    int i = ii.Current;
                    if (group != null)
                    {
                        group.Start(i);
                    }

                    RuleChain r = chains[i];
                    ProcessRuleChain(r, action, null);
                    if (group != null)
                    {
                        group.End();
                    }
                }

                if (group != null)
                {
                    group.End();
                }
            }
        }

        public virtual void ProcessRuleChain(RuleChain chain, IRuleAction action)
        {
            Rule r = chain == null ? null : chain.Head();
            while (r != null)
            {
                action.ProcessRule(r);
                r = r.Next;
            }
        }

        public virtual void ProcessRuleChain(RuleChain chain, IRuleAction action, IRuleGroupAction group)
        {
            Rule r = chain == null ? null : chain.Head();
            if (r != null)
            {
                if (group != null)
                {
                    group.Start();
                }

                while (r != null)
                {
                    action.ProcessRule(r);
                    r = r.Next;
                }

                if (group != null)
                {
                    group.End();
                }
            }
        }

        public virtual void OptimizeRules()
        {
        }

        public override void ComputeRankings(int start)
        {

            // Now sort the rules into ranking order
            RuleSorter sorter = new RuleSorter(start);

            // add all the rules in this Mode to the sorter
            ProcessRules(sorter.AddRule);

            // now allocate ranks to all the rules in this Mode
            sorter.AllocateRanks();
            highestRank = start + sorter.NumberOfRules;
        }

        //
        public virtual void AllocateAllPatternSlots()
        {
            IList<int> count = new List<int>(1); // used to allow inner class to have side-effects
            count.Add(0);
            SlotManager slotManager = new SlotManager(); // TODO: allocate this via the Configuration
            try
            {
                ProcessRules((r) =>
                {
                    int slots = r.Pattern.AllocateSlots(slotManager, 0);
                    int max = Math.Max(count[0], slots);
                    count[0] = max;
                });
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }

            stackFrameSlotsNeeded = count[0];
        }

        //
        public override int GetStackFrameSlotsNeeded()
        {
            return stackFrameSlotsNeeded;
        }

        //
        public virtual void SetStackFrameSlotsNeeded(int slots)
        {
            this.stackFrameSlotsNeeded = slots;
        }

        private sealed class RuleGroupExplainAction : IRuleGroupAction
        {
            private string type;
            private readonly ExpressionPresenter presenter;
            public RuleGroupExplainAction(ExpressionPresenter presenter)
            {
                this.presenter = presenter;
            }

            public void Start()
            {
                presenter.StartElement("ruleSet");
                presenter.EmitAttribute("type", type);
            }

            public void SetLabel(string type)
            {
                this.type = type;
            }

            public void Start(int i)
            {
                presenter.StartElement("ruleChain");
                presenter.EmitAttribute("key", presenter.GetNamePool().GetClarkName(i));
            }

            public void End()
            {
                presenter.EndElement();
            }
        }

        private sealed class RuleSorter
        {
            public List<Rule> rules = new List<Rule>(100);
            private readonly int start;

            public int NumberOfRules => rules.Count;
            public RuleSorter(int start)
            {
                this.start = start;
            }

            public void AddRule(Rule rule)
            {
                rules.Add(rule);
            }

            //
            public void AllocateRanks()
            {

                rules.Sort((x, y) => x.CompareComputedRank(y));
                int rank = start;
                for (int i = 0; i < rules.Count; i++)
                {
                    if (i > 0 && rules[i - 1].CompareComputedRank(rules[i]) != 0)
                    {
                        rank++;
                    }

                    rules[i].Rank = rank;
                }
            }
        }

        //
        public interface IRuleGroupAction
        {
            void SetLabel(string s);
            /// <summary>
            /// Start of a generic group
            /// </summary>
            void Start();
            void Start(int i);
            void End();
        }
    }
}

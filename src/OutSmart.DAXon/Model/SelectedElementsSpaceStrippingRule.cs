////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Model
{
    internal sealed class SelectedElementsSpaceStrippingRule : ISpaceStrippingRule
    {
        private Rule anyElementRule;
        private Rule unnamedElementRuleChain;
        private readonly Dictionary<INodeName, Rule> namedElementRules = new Dictionary<INodeName, Rule>(32);
        private int sequence;
        private readonly bool rejectDuplicates; // in XSLT 3.0, duplicate conflicting rules are a static error
        public SelectedElementsSpaceStrippingRule(bool rejectDuplicates)
        {
            this.rejectDuplicates = rejectDuplicates;
        }

        public int IsSpacePreserving(INodeName fingerprint, ISchemaType schemaType)
        {
            Rule rule = GetRule(fingerprint);
            if (rule == null)
            {
                return Stripper.ALWAYS_PRESERVE;
            }

            return rule.GetAction() == Stripper.PRESERVE ? Stripper.ALWAYS_PRESERVE : Stripper.STRIP_DEFAULT;
        }

        public void AddRule(NodeTest test, Stripper.StripRuleTarget action, StylesheetModule module, int lineNumber)
        {

            // for fast lookup, we maintain one list for each element name for patterns that can only
            // match elements of a given name, one list for each node type for patterns that can only
            // match one kind of non-element node, and one generic list.
            // Each list is sorted in precedence/priority order so we find the highest-priority rule first
            int precedence = module.Precedence;
            int minImportPrecedence = module.MinImportPrecedence;
            NodeTestPattern pattern = new NodeTestPattern(test);
            AddRule(pattern, action, precedence, minImportPrecedence);
        }

        public void AddRule(NodeTestPattern pattern, Stripper.StripRuleTarget action, int precedence, int minImportPrecedence)
        {
            NodeTest test = pattern.GetNodeTest();
            double priority = test.DefaultPriority;
            Rule newRule = new Rule(pattern, action, precedence, minImportPrecedence, priority, sequence++, 0);
            int prio = priority == 0 ? 2 : priority == -0.25 ? 1 : 0;
            newRule.Rank = (precedence << 18) + (prio << 16) + sequence;
            if (test is NodeKindTest)
            {
                newRule.SetAlwaysMatches(true);
                anyElementRule = AddRuleToList(newRule, anyElementRule, true);
            }
            else if (test is NameTest)
            {
                newRule.SetAlwaysMatches(true);
                int fp = test.Fingerprint;
                NamePool pool = ((NameTest)test).GetNamePool();
                FingerprintedQName key = new FingerprintedQName(pool.GetUnprefixedQName(fp), pool);
                Rule chain = namedElementRules.GetOrDefault(key);
                namedElementRules[key] = AddRuleToList(newRule, chain, true);
            }
            else
            {
                unnamedElementRuleChain = AddRuleToList(newRule, unnamedElementRuleChain, false);
            }
        }

        private Rule AddRuleToList(Rule newRule, Rule list, bool dropRemainder)
        {
            if (list == null)
            {
                return newRule;
            }

            int precedence = newRule.Precedence;
            Rule rule = list;
            Rule prev = null;
            while (rule != null)
            {
                if (rule.Precedence <= precedence)
                {
                    if (rejectDuplicates && rule.Precedence == precedence && !rule.GetAction().Equals(newRule.GetAction()))
                    {
                        throw new XPathException("There are conflicting xsl:strip-space and xsl:preserve-space declarations for " + rule.Pattern + " at the same import precedence", "XTSE0270");
                    }

                    newRule.Next = dropRemainder ? null : rule;
                    if (prev == null)
                    {
                        return newRule;
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

            return list;
        }

        public Rule GetRule(INodeName nodeName)
        {

            // search the specific list for this node type / node name
            Rule bestRule = namedElementRules.GetOrDefault(nodeName);

            // search the list for *:local and prefix:* node tests
            if (unnamedElementRuleChain != null)
            {
                bestRule = SearchRuleChain(nodeName, bestRule, unnamedElementRuleChain);
            }


            // See if there is a "*" rule matching all elements
            if (anyElementRule != null)
            {
                bestRule = SearchRuleChain(nodeName, bestRule, anyElementRule);
            }

            return bestRule;
        }

        private Rule SearchRuleChain(INodeName nodeName, Rule bestRule, Rule head)
        {
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
                        if (head.IsAlwaysMatches() || ((NodeTest)head.Pattern.GetItemType()).Matches(Types.Type.ELEMENT, nodeName, null))
                        {

                            // reportAmbiguity(bestRule, head);
                            // We no longer report the recoverable error XTRE0270, we always
                            // take the recovery action.
                            // choose whichever one comes last (assuming the error wasn't fatal)
                            bestRule = head;
                            break;
                        }
                        else
                        {
                        }
                    }
                    else
                    {

                        // this rule has higher rank than the matching rule already found
                        if (head.IsAlwaysMatches() || ((NodeTest)head.Pattern.GetItemType()).Matches(Types.Type.ELEMENT, nodeName, null))
                        {
                            bestRule = head;
                        }
                    }
                }
                else if (head.IsAlwaysMatches() || ((NodeTest)head.Pattern.GetItemType()).Matches(Types.Type.ELEMENT, nodeName, null))
                {
                    bestRule = head;
                    break;
                }

                head = head.Next;
            }

            return bestRule;
        }

        public ProxyReceiver MakeStripper(IReceiver next)
        {
            return new Stripper(this, next);
        }

        public void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("strip");
            Rule rule = anyElementRule;
            while (rule != null)
            {
                ExportRule(rule, presenter);
                rule = rule.Next;
            }

            rule = unnamedElementRuleChain;
            while (rule != null)
            {
                ExportRule(rule, presenter);
                rule = rule.Next;
            }

            foreach (Rule r in namedElementRules.Values)
            {
                ExportRule(r, presenter);
            }

            presenter.EndElement();
        }

        private static void ExportRule(Rule rule, ExpressionPresenter presenter)
        {
            string which = rule.GetAction() == Stripper.STRIP ? "s" : "p";
            presenter.StartElement(which);
            presenter.EmitAttribute("test", AlphaCode.FromItemType(rule.Pattern.GetItemType()));
            presenter.EmitAttribute("prec", rule.Precedence + "");
            presenter.EndElement();
        } //    private static void exportRuleJS(Rule rule, StringBuilder fsb) {
        //        if (test instanceof NodeKindTest) {
        //            // elements="*"
        //            fsb.append("if (uri=='" + test.getMatchingNodeName().getURI() +
        //                               "' && local=='" + test.getMatchingNodeName().getLocalPart() +
        //                               "') return " + which + ";" );
    }
}
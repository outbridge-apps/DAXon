////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    internal sealed class KeyFn : SystemFunction, IStatefulSystemFunction
    {
        private KeyDefinitionSet staticKeySet;
        public KeyManager GetKeyManager()
        {
            return GetRetainedStaticContext().GetPackageData().GetKeyManager();
        }

        public INamespaceResolver GetNamespaceResolver()
        {
            return GetRetainedStaticContext();
        }

        public override int GetSpecialProperties(Expression[] arguments)
        {
            int prop = StaticProperty.ORDERED_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED;
            if ((GetArity() == 2) || (arguments[2].GetSpecialProperties() & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0)
            {
                prop |= StaticProperty.CONTEXT_DOCUMENT_NODESET;
            }

            return prop;
        }

        public SystemFunction Copy()
        {
            KeyFn k2 = (KeyFn)SystemFunction.MakeFunction(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), GetArity());
            k2.staticKeySet = staticKeySet;
            return k2;
        }

        public override Expression FixArguments(params Expression[] arguments)
        {
            if (arguments[0] is StringLiteral && staticKeySet == null)
            {
                KeyManager keyManager = GetKeyManager();
                string keyName = ((StringLiteral)arguments[0]).Stringify();
                staticKeySet = GetKeyDefinitionSet(keyManager, keyName);
            }

            return null;
        }

        public PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            if (staticKeySet != null)
            {
                PathMap.PathMapNodeSet result = new PathMap.PathMapNodeSet();
                foreach (KeyDefinition kd in staticKeySet.KeyDefinitions)
                {
                    Patterns.Pattern pat = kd.Match;
                    if (pat is NodeSetPattern)
                    {
                        Expression selector = ((NodeSetPattern)pat).SelectionExpression;
                        PathMap.PathMapNodeSet selected = selector.AddToPathMap(pathMap, pathMapNodeSet);
                        Expression use = kd.Use;
                        PathMap.PathMapNodeSet used = use.AddToPathMap(pathMap, selected);
                        result.AddNodeSet(selected);
                    }
                    else
                    {
                        throw new InvalidOperationException("Can't add key() call to pathmap");
                    }
                }

                return result;
            }
            else
            {
                throw new InvalidOperationException("Can't add dynamic key() call to pathmap");
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo origin;
            if (arguments.Length == 3)
            {
                origin = (NodeInfo)GetOrigin(context, arguments[2]);
            }
            else
            {
                origin = GetContextRoot(context);
            }

            if (origin.Root.GetNodeKind() != Types.Type.DOCUMENT)
            {
                throw new XPathException("In the key() function," + " the node supplied in the third argument (or the context node if absent)" + " must be in a tree whose root is a document node", "XTDE1270", context);
            }

            KeyDefinitionSet selectedKeySet = staticKeySet;
            KeyManager keyManager = GetKeyManager();
            if (selectedKeySet == null)
            {
                selectedKeySet = GetKeyDefinitionSet(keyManager, arguments[0].Head().GetStringValue());
            }

            return Search(keyManager, context, arguments[1], origin, selectedKeySet);
        }

        private static NodeInfo GetContextRoot(IXPathContext context)
        {
            IItem contextItem = context.GetContextItem();
            if (contextItem == null)
            {
                throw new XPathException("Cannot call the key() function when there is no context item", "XTDE1270", context);
            }
            else if (!(contextItem is NodeInfo))
            {
                throw new XPathException("Cannot call the key() function when the context item is not a node", "XTDE1270", context);
            }

            return ((NodeInfo)contextItem).Root;
        }

        private static IItem GetOrigin(IXPathContext context, ISequence argument2)
        {
            IItem arg2;
            try
            {
                arg2 = argument2.Head();
            }
            catch (XPathException e)
            {
                if (e.HasErrorCode("XPDY0050"))
                {
                    throw new XPathException("In the key() function," + " the node supplied in the third argument (or the context node if absent)" + " must be in a tree whose root is a document node", "XTDE1270", context);
                }
                else if (e.HasErrorCode("XPTY0020", "XPTY0019"))
                {
                    throw new XPathException("Cannot call the key() function when the context item is an atomic value", "XTDE1270", context);
                }

                throw;
            }

            return arg2;
        }

        private KeyDefinitionSet GetKeyDefinitionSet(KeyManager keyManager, string keyName)
        {
            KeyDefinitionSet selectedKeySet;
            StructuredQName qName;
            try
            {
                qName = StructuredQName.FromLexicalQName(keyName, false, true, GetNamespaceResolver());
            }
            catch (XPathException err)
            {
                throw new XPathException("Invalid key name: " + err.Message, "XTDE1260");
            }

            selectedKeySet = keyManager.GetKeyDefinitionSet(qName);
            if (selectedKeySet == null)
            {
                throw new XPathException("Key '" + keyName + "' has not been defined", "XTDE1260");
            }

            return selectedKeySet;
        }

        protected static ISequence Search(KeyManager keyManager, IXPathContext context, ISequence sought, NodeInfo origin, KeyDefinitionSet selectedKeySet)
        {
            NodeInfo doc = origin.Root;
            if (selectedKeySet.IsComposite())
            {
                ISequenceIterator soughtKey = sought.Iterate();
                ISequenceIterator all = keyManager.SelectByCompositeKey(selectedKeySet, doc.GetTreeInfo(), soughtKey, context);
                if (origin.Equals(doc))
                {
                    return new LazySequence(all);
                }

                return new LazySequence(ItemMappingIterator.Filter(all, (item) => Navigator.IsAncestorOrSelf(origin, (NodeInfo)item)));
            }
            else
            {

                // Changed by bug 2929 and bug 4656
                ISequenceIterator allResults = null;
                if (sought is AtomicValue)
                {
                    ISequenceIterator results = keyManager.SelectByKey(selectedKeySet, doc.GetTreeInfo(), (AtomicValue)sought, context);
                    if (results is EmptyIterator)
                    {
                        return EmptySequence.GetInstance();
                    }
                    else if (results is SingletonIterator)
                    {
                        NodeInfo result = (NodeInfo)results.Next();
                        if (doc.Equals(origin) || Navigator.IsAncestorOrSelf(origin, result))
                        {
                            return result;
                        }
                        else
                        {
                            return EmptySequence.GetInstance();
                        }
                    }
                    else
                    {
                        if (doc.Equals(origin))
                        {
                            return new LazySequence(results);
                        }
                        else
                        {
                            new LazySequence(ItemMappingIterator.Filter(results, (item) => Navigator.IsAncestorOrSelf(origin, (NodeInfo)item)));
                        }
                    }
                }

                ISequenceIterator keys = sought.Iterate();
                AtomicValue keyValue;
                IList<ISequenceIterator> allKeyIterators = new List<ISequenceIterator>();
                while ((keyValue = (AtomicValue)keys.Next()) != null)
                {
                    ISequenceIterator someResults = keyManager.SelectByKey(selectedKeySet, doc.GetTreeInfo(), keyValue, context);
                    allKeyIterators.Add(someResults);
                }

                if (allKeyIterators.Count == 0)
                {
                    return EmptySequence.GetInstance();
                }
                else if (allKeyIterators.Count == 1)
                {
                    allResults = allKeyIterators[0];
                }
                else
                {
                    // OutSmart.DAXon.Expressions.UnionIterator is the REAL port (upstream expr/UnionIterator);
                    // Tree.Iter.UnionIterator was a hollow wrong-namespace stub (Next() => NIE).
                    allResults = new OutSmart.DAXon.Expressions.UnionIterator(allKeyIterators, LocalOrderComparer.GetInstance());
                }

                if (origin.Equals(doc))
                {
                    return new LazySequence(allResults);
                }

                return new LazySequence(ItemMappingIterator.Filter(allResults, (item) => Navigator.IsAncestorOrSelf(origin, (NodeInfo)item)));
            }
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class PathMap
    {
        private readonly IList<PathMapRoot> pathMapRoots = new List<PathMapRoot>();
        private readonly Dictionary<IBinding, PathMapNodeSet> pathsForVariables = new Dictionary<IBinding, PathMapNodeSet>(); // a map from a variable IBinding to a PathMapNodeSet

        public virtual PathMapRoot[] PathMapRoots => pathMapRoots.ToArray();

        public virtual PathMapRoot ContextDocumentRoot
        {
            get
            {

                PathMapRoot[] roots = PathMapRoots;
                PathMapRoot contextRoot = null;
                foreach (PathMapRoot root in roots)
                {
                    PathMapRoot newRoot = ReduceToDownwardsAxes(root);
                    if (newRoot.GetRootExpression() is RootExpression)
                    {
                        if (contextRoot != null)
                        {
                            throw new InvalidOperationException("More than one context document root found in path map");
                        }
                        else
                        {
                            contextRoot = newRoot;
                        }
                    }
                }


                return contextRoot;
            }
        }

        public virtual PathMapRoot ContextItemRoot
        {
            get
            {

                PathMapRoot[] roots = PathMapRoots;
                PathMapRoot contextRoot = null;
                foreach (PathMapRoot root in roots)
                {
                    if (root.GetRootExpression() is ContextItemExpression)
                    {
                        if (contextRoot != null)
                        {
                            throw new InvalidOperationException("More than one context document root found in path map");
                        }
                        else
                        {
                            contextRoot = root;
                        }
                    }
                }

                return contextRoot;
            }
        }

        public PathMap(Expression exp)
        {
            PathMapNodeSet finalNodes = exp.AddToPathMap(this, null);
            if (finalNodes != null)
            {
                foreach (PathMapNode node in finalNodes)
                {
                    node.SetReturnable(true);
                }
            }
        }

        public virtual PathMapRoot MakeNewRoot(Expression exp)
        {
            foreach (PathMapRoot r in pathMapRoots)
            {
                if (exp.IsEqual(r.GetRootExpression()))
                {
                    return r;
                }
            }

            PathMapRoot root = new PathMapRoot(exp);
            pathMapRoots.Add(root);
            return root;
        }

        public virtual void RegisterPathForVariable(IBinding binding, PathMapNodeSet nodeset)
        {
            pathsForVariables[binding] = nodeset;
        }

        public virtual PathMapNodeSet GetPathForVariable(IBinding binding)
        {
            return pathsForVariables.GetOrDefault(binding);
        }

        public virtual PathMapRoot GetRootForDocument(string requiredUri)
        {

            PathMapRoot[] roots = PathMapRoots;
            PathMapRoot requiredRoot = null;
            foreach (PathMapRoot root in roots)
            {
                PathMapRoot newRoot = ReduceToDownwardsAxes(root);
                Expression exp = newRoot.GetRootExpression();
                string baseUri;
                if (exp.IsCallOn(typeof(Doc)))
                {
                    baseUri = exp.StaticBaseURIString;
                }
                else if (exp.IsCallOn(typeof(DocumentFn)))
                {
                    baseUri = exp.StaticBaseURIString;
                }
                else
                {
                    continue;
                }

                Expression arg = ((SystemFunctionCall)exp).GetArg(0);
                string suppliedUri = null;
                if (arg is Literal)
                {
                    try
                    {
                        string argValue = ((Literal)arg).GroundedValue.UnicodeStringValue.ToString();
                        if (baseUri == null)
                        {
                            if (new URI(argValue).IsAbsolute())
                            {
                                suppliedUri = argValue;
                            }
                            else
                            {
                                suppliedUri = null;
                            }
                        }
                        else
                        {
                            suppliedUri = ResolveURI.MakeAbsolute(argValue, baseUri).ToString();
                        }
                    }
                    catch (URISyntaxException)
                    {
                        suppliedUri = null;
                    }
                    catch (XPathException)
                    {
                        suppliedUri = null;
                    }
                }

                if (requiredUri.Equals(suppliedUri))
                {
                    if (requiredRoot != null)
                    {
                        throw new InvalidOperationException("More than one document root found in path map for " + requiredUri);
                    }
                    else
                    {
                        requiredRoot = newRoot;
                    }
                }
            }


            return requiredRoot;
        }

        public virtual PathMapRoot ReduceToDownwardsAxes(PathMapRoot root)
        {

            // If the path is rooted at an arbitrary context node, we rebase it to be rooted at the
            // document root. This involves changing the root to a RootExpression, and changing the axis
            // for initial steps from child to descendant where necessary
            if (root.isDownwardsOnly)
            {
                return root;
            }

            PathMapRoot newRoot = root;
            if (root.GetRootExpression() is ContextItemExpression)
            {
                RootExpression slash = new RootExpression();

                newRoot = MakeNewRoot(slash);
                for (int i = root.arcs.Count - 1; i >= 0; i--)
                {
                    PathMapArc arc = root.arcs[i];
                    int axis = arc.GetAxis();
                    switch (axis)
                    {
                        case AxisInfo.ATTRIBUTE:
                        case AxisInfo.NAMESPACE:
                            {
                                PathMapNode newTarget = new PathMapNode();
                                newTarget.arcs.Add(arc);
                                newRoot.CreateArc(AxisInfo.DESCENDANT, NodeKindTest.ELEMENT, newTarget);
                                break;
                            }

                        default:
                            {
                                newRoot.CreateArc(AxisInfo.DESCENDANT_OR_SELF, arc.GetNodeTest(), arc.GetTarget());
                                break;
                            }
                    }
                }

                for (int i = 0; i < pathMapRoots.Count; i++)
                {
                    if (pathMapRoots[i] == root)
                    {
                        pathMapRoots.RemoveAt(i);
                        break;
                    }
                }
            }


            // Now process the tree of paths recursively, rewriting all axes in terms of downwards
            // selections, if necessary as downward selections from the root
            IndexedStack<PathMapNode> nodeStack = new IndexedStack<PathMapNode>();
            nodeStack.IPush(newRoot);
            ReduceToDownwardsAxes(newRoot, nodeStack);
            newRoot.isDownwardsOnly = true;
            return newRoot;
        }

        private void ReduceToDownwardsAxes(PathMapRoot root, IndexedStack<PathMapNode> nodeStack)
        {
            ExpressionTool.ProbeTreeDepth();

            //PathMapArc lastArc = (PathMapArc)arcStack.peek();
            PathMapNode node = nodeStack.Peek();
            if (node.HasUnknownDependencies())
            {
                root.SetHasUnknownDependencies();
            }

            for (int i = 0; i < node.arcs.Count; i++)
            {
                nodeStack.IPush((node.arcs[i]).GetTarget());
                ReduceToDownwardsAxes(root, nodeStack);
                nodeStack.Pop();
            }

            for (int i = node.arcs.Count - 1; i >= 0; i--)
            {
                PathMapArc thisArc = node.arcs[i];

                PathMapNode grandParent = (nodeStack.Count < 2 ? null : nodeStack[nodeStack.Count - 2]);
                int lastAxis = -1;
                if (grandParent != null)
                {
                    foreach (PathMapArc arc1 in grandParent.arcs)
                    {
                        PathMapArc arc = (arc1);
                        if (arc.GetTarget() == node)
                        {
                            lastAxis = arc.GetAxis();
                        }
                    }
                }

                switch (thisArc.GetAxis())
                {
                    case AxisInfo.ANCESTOR_OR_SELF:
                    case AxisInfo.DESCENDANT_OR_SELF:
                        if (thisArc.GetNodeTest() == NodeKindTest.DOCUMENT)
                        {

                            // This is typically an absolute path expression appearing within a predicate
                            node.arcs.RemoveAt(i);
                            foreach (PathMapArc arc in thisArc.GetTarget().arcs)
                            {
                                root.arcs.Add(arc);
                            }

                            break;
                        }
                        else
                        {
                            goto case AxisInfo.ANCESTOR; // fall through
                        }

                    case AxisInfo.ANCESTOR:
                    case AxisInfo.FOLLOWING:
                    case AxisInfo.PRECEDING:
                        {

                            // replace the axis by a downwards axis from the root
                            if (thisArc.GetAxis() != AxisInfo.DESCENDANT_OR_SELF)
                            {
                                root.CreateArc(AxisInfo.DESCENDANT_OR_SELF, thisArc.GetNodeTest(), thisArc.GetTarget());
                                node.arcs.RemoveAt(i);
                            }

                            break;
                        }

                    case AxisInfo.ATTRIBUTE:
                    case AxisInfo.CHILD:
                    case AxisInfo.DESCENDANT:
                    case AxisInfo.NAMESPACE:

                        // no action
                        break;
                    case AxisInfo.FOLLOWING_SIBLING:
                    case AxisInfo.PRECEDING_SIBLING:
                        {
                            if (grandParent != null)
                            {
                                grandParent.CreateArc(lastAxis, thisArc.GetNodeTest(), thisArc.GetTarget());
                                node.arcs.RemoveAt(i);
                                break;
                            }
                            else
                            {
                                root.CreateArc(AxisInfo.CHILD, thisArc.GetNodeTest(), thisArc.GetTarget());
                                node.arcs.RemoveAt(i);
                                break;
                            }
                        }

                    case AxisInfo.PARENT:
                        {
                            if (lastAxis == AxisInfo.CHILD || lastAxis == AxisInfo.ATTRIBUTE || lastAxis == AxisInfo.NAMESPACE)
                            {

                                // ignore the parent step - it leads to somewhere we have already been.
                                // But it might become a returned node
                                if (node.IsReturnable())
                                {
                                    grandParent.SetReturnable(true);
                                }


                                // any paths after the parent step need to be attached to the grandparent
                                PathMapNode target = thisArc.GetTarget();
                                for (int a = 0; a < target.arcs.Count; a++)
                                {
                                    grandParent.arcs.Add(target.arcs[a]);
                                }

                                node.arcs.RemoveAt(i);
                            }
                            else if (lastAxis == AxisInfo.DESCENDANT)
                            {
                                if (thisArc.GetTarget().arcs.Count == 0)
                                {
                                    grandParent.CreateArc(AxisInfo.DESCENDANT_OR_SELF, thisArc.GetNodeTest());
                                }
                                else
                                {
                                    grandParent.CreateArc(AxisInfo.DESCENDANT_OR_SELF, thisArc.GetNodeTest(), thisArc.GetTarget());
                                }

                                node.arcs.RemoveAt(i);
                            }
                            else
                            {

                                // don't try to be precise about a/b/../../c
                                if (thisArc.GetTarget().arcs.Count == 0)
                                {
                                    root.CreateArc(AxisInfo.DESCENDANT_OR_SELF, thisArc.GetNodeTest());
                                }
                                else
                                {
                                    root.CreateArc(AxisInfo.DESCENDANT_OR_SELF, thisArc.GetNodeTest(), thisArc.GetTarget());
                                }

                                node.arcs.RemoveAt(i);
                            }

                            break;
                        }

                    case AxisInfo.SELF:
                        {

                            // This step can't take us anywhere we haven't been, so delete it
                            node.arcs.RemoveAt(i);
                            break;
                        }
                }
            }
        }
        public class PathMapNode
        {
            internal IList<PathMapArc> arcs;   // Arcs is the public spelling
            private bool returnable;
            private bool atomized;
            private bool _hasUnknownDependencies;

            public virtual PathMapArc[] Arcs => arcs.ToArray();
            /// <summary>
            /// Create a node in the PathMap (initially with no arcs)
            /// </summary>
            public PathMapNode()
            {
                arcs = new List<PathMapArc>();
            }

            public virtual PathMapNode CreateArc(int axis, NodeTest test)
            {
                foreach (PathMapArc a in arcs)
                {
                    if (a.GetAxis() == axis && a.GetNodeTest().Equals(test))
                    {
                        return a.GetTarget();
                    }
                }

                PathMapNode target = new PathMapNode();
                PathMapArc arc = new PathMapArc(axis, test, target);
                arcs.Add(arc);
                return target;
            }

            public virtual void CreateArc(int axis, NodeTest test, PathMapNode target)
            {
                foreach (PathMapArc a in arcs)
                {
                    if (a.GetAxis() == axis && a.GetNodeTest().Equals(test) && a.GetTarget() == target)
                    {

                        // TODO: if it's a different target, then merge the two targets into one. XMark Q8
                        a.GetTarget().SetReturnable(a.GetTarget().IsReturnable() || target.IsReturnable());
                        if (target.IsAtomized())
                        {
                            a.GetTarget().SetAtomized();
                        }

                        return;
                    }
                }

                PathMapArc arc = new PathMapArc(axis, test, target);
                arcs.Add(arc);
            }

            public virtual void SetReturnable(bool returnable)
            {
                this.returnable = returnable;
            }

            public virtual bool IsReturnable()
            {
                return returnable;
            }

            public virtual bool HasReachableReturnables()
            {
                if (IsReturnable())
                {
                    return true;
                }

                foreach (PathMapArc arc in arcs)
                {
                    if (arc.GetTarget().HasReachableReturnables())
                    {
                        return true;
                    }
                }

                return false;
            }

            public virtual void SetAtomized()
            {
                this.atomized = true;
            }

            public virtual bool IsAtomized()
            {
                return atomized;
            }

            public virtual void SetHasUnknownDependencies()
            {
                _hasUnknownDependencies = true;
            }

            public virtual bool HasUnknownDependencies()
            {
                return _hasUnknownDependencies;
            }

            public virtual bool AllPathsAreWithinStreamableSnapshot()
            {
                if (HasUnknownDependencies() || IsReturnable() || IsAtomized())
                {
                    return false;
                }

                foreach (PathMapArc arc in arcs)
                {
                    int axis = arc.GetAxis();
                    if (axis == AxisInfo.ATTRIBUTE)
                    {
                        PathMapNode next = arc.GetTarget();
                        if (next.IsReturnable())
                        {
                            return false;
                        }

                        if (next.Arcs.Length != 0 && !next.AllPathsAreWithinStreamableSnapshot())
                        {
                            return false;
                        }
                    }
                    else if (axis == AxisInfo.SELF || axis == AxisInfo.ANCESTOR || axis == AxisInfo.ANCESTOR_OR_SELF || axis == AxisInfo.PARENT)
                    {
                        PathMapNode next = arc.GetTarget();
                        if (next.IsAtomized())
                        {
                            return false;
                        }

                        if (!next.AllPathsAreWithinStreamableSnapshot())
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public class PathMapRoot : PathMapNode
        {
            private readonly Expression rootExpression;
            public bool isDownwardsOnly;
            public PathMapRoot(Expression root)
            {
                this.rootExpression = root;
            }

            public virtual Expression GetRootExpression()
            {
                return rootExpression;
            }
        }

        public class PathMapArc
        {
            private readonly PathMapNode target;
            private readonly int axis;
            private readonly NodeTest test;
            public PathMapArc(int axis, NodeTest test, PathMapNode target)
            {
                this.axis = axis;
                this.test = test;
                this.target = target;
            }

            public virtual int GetAxis()
            {
                return axis;
            }

            public virtual NodeTest GetNodeTest()
            {
                return test;
            }

            public virtual PathMapNode GetTarget()
            {
                return target;
            }
        }

        /// <summary>
        /// A (mutable) set of nodes in the path map
        /// </summary>
        public class PathMapNodeSet : HashSet<PathMapNode>
        {
            public PathMapNodeSet()
            {
            }

            public PathMapNodeSet(PathMapNode singleton)
            {
                this.Add(singleton);
            }

            public virtual PathMapNodeSet CreateArc(int axis, NodeTest test)
            {
                PathMapNodeSet targetSet = new PathMapNodeSet();
                foreach (PathMapNode node in this)
                {
                    targetSet.Add(node.CreateArc(axis, test));
                }

                return targetSet;
            }

            public virtual void AddNodeSet(PathMapNodeSet nodes)
            {
                if (nodes != null)
                {
                    foreach (PathMapNode node in nodes)
                    {
                        this.Add(node);
                    }
                }
            }

            public virtual void SetAtomized()
            {
                foreach (PathMapNode node in this)
                {
                    node.SetAtomized();
                }
            }

            public virtual void SetReturnable(bool isReturned)
            {
                foreach (PathMapNode node in this)
                {
                    node.SetReturnable(isReturned);
                }
            }

            public virtual bool HasReachableReturnables()
            {
                foreach (PathMapNode node in this)
                {
                    if (node.HasReachableReturnables())
                    {
                        return true;
                    }
                }

                return false;
            }

            public virtual bool AllPathsAreWithinStreamableSnapshot()
            {
                foreach (PathMapNode node in this)
                {
                    if (!node.AllPathsAreWithinStreamableSnapshot())
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Indicate that all the descendants of the nodes in this nodeset are required
            /// </summary>
            public virtual void AddDescendants()
            {
                foreach (PathMapNode node in this)
                {
                    node.CreateArc(AxisInfo.DESCENDANT, AnyNodeTest.GetInstance());
                }
            }

            /// <summary>
            /// Indicate that all the nodes have unknown dependencies
            /// </summary>
            public virtual void SetHasUnknownDependencies()
            {
                foreach (PathMapNode node in this)
                {
                    node.SetHasUnknownDependencies();
                }
            }
        }
    }
}

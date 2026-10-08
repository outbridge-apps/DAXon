////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Api;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class ExpressionVisitor
    {
        private const int MAX_DEPTH = 500;

        // .NET hardening (no upstream equivalent). The parser's MAX_EXPRESSION_NESTING guard bounds
        // RECURSIVE nesting, but the parser also builds deep LEFT-LEANING trees ITERATIVELY from a
        // shallow parse (1+1+1+..., a[.][.]...), so those escape it: a chain of N operators becomes a
        // tree N deep while the parser recursed only once. Descending such a tree in TypeCheck is
        // O(n^2) (uncached GetItemType re-descends the whole chain per node) and at extreme depth
        // overflows the uncatchable .NET stack. Every child descent in both static phases funnels
        // through Operand.TypeCheck/Optimize, so one counter here bounds the whole static-analysis
        // pipeline. Raise a static XPST0003 at the same bound as the parser, far below the stack wall.
        // (MAX_DEPTH above is the separate, milder optimizer-only cap that merely STOPS optimizing.)
        public const int MAX_STATIC_TREE_DEPTH = 3000;
        private int staticTreeDepth = 0;
        private IStaticContext staticContext;
        private bool optimizeForStreaming = false;
        private bool optimizeForPatternMatching = false;
        private readonly Configuration config;
        private Optimizer optimizer;
        private int depth = 0;
        private bool inliningFunctions = false;
        private bool suppressWarnings = false;

        public virtual IStaticContext StaticContext
        {
            get => staticContext; set
            {
                this.staticContext = value;
            }
        }
        public ExpressionVisitor(Configuration config)
        {
            this.config = config;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public static ExpressionVisitor Make(IStaticContext env)
        {
            ExpressionVisitor visitor = new ExpressionVisitor(env.GetConfiguration());
            visitor.StaticContext = env;
            return visitor;
        }

        public virtual void IssueWarning(string message, string errorCode, ILocation locator)
        {
            if (!IsSuppressWarnings())
            {
                staticContext.IssueWarning(message, errorCode, locator);
            }
        }

        public virtual IXPathContext MakeDynamicContext()
        {
            return staticContext.MakeEarlyEvaluationContext();
        }

        public virtual Optimizer ObtainOptimizer()
        {
            if (optimizer == null)
            {
                optimizer = config.ObtainOptimizer(staticContext.GetOptimizerOptions());
            }

            return optimizer;
        }

        public virtual void SetOptimizeForStreaming(bool option)
        {
            optimizeForStreaming = option;
        }

        public virtual bool IsOptimizeForStreaming()
        {
            return optimizeForStreaming;
        }

        public virtual void SetOptimizeForPatternMatching(bool option)
        {
            optimizeForPatternMatching = option;
        }

        public virtual bool IsOptimizeForPatternMatching()
        {
            return optimizeForPatternMatching;
        }

        public virtual string GetTargetEdition()
        {
            return staticContext.GetPackageData().TargetEdition;
        }

        public virtual bool IncrementAndTestDepth()
        {
            return depth++ < MAX_DEPTH;
        }

        /// <summary>
        /// Decrement depth
        /// </summary>
        public virtual void DecrementDepth()
        {
            depth--;
        }

        /// <summary>
        /// Enter one level of static-analysis tree descent (TypeCheck/Optimize). Bounds tree depth so
        /// pathologically deep left-leaning trees raise a clean static error instead of hanging (O(n^2))
        /// or overflowing the uncatchable .NET stack. Self-decrements before throwing, so callers must
        /// pair every successful call with exactly one <see cref="LeaveStaticDescent"/> in a finally.
        /// </summary>
        public virtual void EnterStaticDescent()
        {
            if (++staticTreeDepth > MAX_STATIC_TREE_DEPTH)
            {
                staticTreeDepth--;
                throw TooDeep("exceeds the static-analysis limit of " + MAX_STATIC_TREE_DEPTH);
            }

            // The compile's deadline too: type-checking and optimizing a chain of clauses is quadratic work that no
            // evaluation loop ever sees (20000 let clauses ran minutes past the limit).
            Controller.CheckActiveTimeout();

            // Counter = Java-parity ceiling; the stack-adaptive probe covers threads whose stack
            // cannot hold even that many analysis levels (same discipline as XPathParser).
            try
            {
                StackGuard.Probe();
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                staticTreeDepth--;

                // Described rather than converted: static analysis also runs at evaluation time
                // (xsl:evaluate, fn:transform), where an XPathException would unwind through the
                // engine stack above it. TooDeep stays for the deterministic counter path.
                throw e.Describe("Expression is too deeply nested (insufficient stack on this thread)", "XPST0003", null);
            }
        }

        private static XPathException TooDeep(string reason)
        {
            XPathException err = new XPathException("Expression is too deeply nested (" + reason + ")").AsStaticError().WithErrorCode("XPST0003");
            err.SetIsSyntaxError(true);
            return err;
        }

        /// <summary>
        /// Leave one level of static-analysis tree descent. Pairs with <see cref="EnterStaticDescent"/>.
        /// </summary>
        public virtual void LeaveStaticDescent()
        {
            staticTreeDepth--;
        }

        public virtual bool IsSuppressWarnings()
        {
            return suppressWarnings;
        }

        public virtual void SetSuppressWarnings(bool suppressWarnings)
        {
            this.suppressWarnings = suppressWarnings;
        }

        public virtual bool IsInliningFunctions()
        {
            return inliningFunctions;
        }

        public virtual void SetInliningFunctions(bool inliningFunctions)
        {
            this.inliningFunctions = inliningFunctions;
        }
    }
}
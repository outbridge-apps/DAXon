////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // XPathParser part: range-variable scopes, QName/fingerprint/name-test factories, source
    // locations; nested location/details types.
    public partial class XPathParser
    {
        public virtual ILocalBinding FindOuterRangeVariable(StructuredQName qName)
        {
            return FindOuterRangeVariable(qName, inlineFunctionStack, GetStaticContext());
        }

        public static ILocalBinding FindOuterRangeVariable(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, IStaticContext env)
        {

            // If we didn't find the variable, it might be defined in an outer scope.
            ILocalBinding b2 = FindOuterXPathRangeVariable(qName, inlineFunctionStack);
            if (b2 != null)
            {
                return b2;
            }


            // It's not an in-scope range variable. If this is a free-standing XPath expression, it might be
            // a parameter declared in the static context
            if (env is IndependentContext && !inlineFunctionStack.IsEmpty())
            {
                b2 = FindXPathParameter(qName, inlineFunctionStack, env);
            }


            // It's not an in-scope range variable. If we're in XSLT, it might be an XSLT-defined local variable
            if (env is ExpressionContext && !inlineFunctionStack.IsEmpty())
            {
                b2 = FindOuterXSLTVariable(qName, inlineFunctionStack, env);
            }

            return b2; // if null, it's not an in-scope range variable
        }

        private static ILocalBinding FindOuterXPathRangeVariable(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack)
        {
            for (int s = inlineFunctionStack.Size() - 1; s >= 0; s--)
            {
                InlineFunctionDetails details = inlineFunctionStack[s];
                IndexedStack<ILocalBinding> outerVariables = details.outerVariables;
                for (int v = outerVariables.Size() - 1; v >= 0; v--)
                {
                    ILocalBinding b2 = outerVariables[v];
                    if (b2.GetVariableQName().Equals(qName))
                    {
                        for (int bs = s; bs <= inlineFunctionStack.Count - 1; bs++)
                        {
                            details = inlineFunctionStack[bs];
                            bool found = false;
                            for (int p = 0; p < details.outerVariablesUsed.Count - 1; p++)
                            {
                                if (details.outerVariablesUsed[p] == b2)
                                {

                                    // the inner function already uses the outer variable
                                    b2 = details.implicitParams[p];
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {

                                // Need to add an implicit parameter to the inner function
                                details.outerVariablesUsed.Add(b2);
                                UserFunctionParameter ufp = new UserFunctionParameter();
                                ufp.SetVariableQName(qName);
                                ufp.SetRequiredType(b2.GetRequiredType());
                                details.implicitParams.Add(ufp);
                                b2 = ufp;
                            }
                        }

                        return b2;
                    }
                }

                ILocalBinding b3 = BindParametersInNestedFunctions(qName, inlineFunctionStack, s);
                if (b3 != null)
                {
                    return b3;
                }
            }

            return null;
        }

        private static ILocalBinding FindXPathParameter(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, IStaticContext env)
        {
            if (env is IndependentContext)
            {
                XPathVariable var = ((IndependentContext)env).GetExternalVariable(qName);
                if (var != null)
                {
                    InlineFunctionDetails details = inlineFunctionStack[0];
                    ILocalBinding innermostBinding;
                    bool found = false;
                    for (int p = 0; p < details.outerVariablesUsed.Count; p++)
                    {
                        if (details.outerVariablesUsed[p].GetVariableQName().Equals(qName))
                        {

                            // the inner function already uses the outer variable
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {

                        // Need to add an implicit parameter to the inner function
                        details.outerVariablesUsed.Add(var);
                        UserFunctionParameter ufp = new UserFunctionParameter();
                        ufp.SetVariableQName(qName);
                        ufp.SetRequiredType(var.GetRequiredType());
                        details.implicitParams.Add(ufp);
                    }


                    // Now do the same for all inner inline functions, but this time binding to the
                    // relevant parameter of the next containing function
                    innermostBinding = BindParametersInNestedFunctions(qName, inlineFunctionStack, 0);
                    return innermostBinding;
                }
            }

            return null;
        }

        private static ILocalBinding FindOuterXSLTVariable(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, IStaticContext env)
        {
            StructuredQName attName = ((ExpressionContext)env).AttributeName;
            SourceBinding decl = ((ExpressionContext)env).GetStyleElement().BindLocalVariable(qName, attName);
            if (decl != null)
            {
                InlineFunctionDetails details = inlineFunctionStack[0];
                ILocalBinding innermostBinding;
                bool found = false;
                for (int p = 0; p < details.outerVariablesUsed.Count; p++)
                {
                    if (details.outerVariablesUsed[p].GetVariableQName().Equals(qName))
                    {

                        // the inner function already uses the outer variable
                        found = true;
                        break;
                    }
                }

                if (!found)
                {

                    // Need to add an implicit parameter to the inner function
                    details.outerVariablesUsed.Add(new ParserExtension.TemporaryXSLTVariableBinding(decl));
                    UserFunctionParameter ufp = new UserFunctionParameter();
                    ufp.SetVariableQName(qName);
                    ufp.SetRequiredType(decl.GetInferredType(true));
                    details.implicitParams.Add(ufp);
                }


                // Now do the same for all inner inline functions, but this time binding to the
                // relevant parameter of the next containing function
                innermostBinding = BindParametersInNestedFunctions(qName, inlineFunctionStack, 0);
                return innermostBinding;
            }

            return null;
        }

        private static ILocalBinding BindParametersInNestedFunctions(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, int start)
        {
            InlineFunctionDetails details = inlineFunctionStack[start];
            IList<UserFunctionParameter> @params = details.implicitParams;
            foreach (UserFunctionParameter param in @params)
            {
                if (param.GetVariableQName().Equals(qName))
                {

                    // The variable reference corresponds to a parameter of an outer inline function
                    // We potentially need to add implicit parameters to any inner inline functions, and
                    // bind the variable reference to the innermost of these implicit parameters
                    ILocalBinding b2 = param;
                    for (int bs = start + 1; bs <= inlineFunctionStack.Count - 1; bs++)
                    {
                        details = inlineFunctionStack[bs];
                        bool found = false;
                        for (int p = 0; p < details.outerVariablesUsed.Count - 1; p++)
                        {
                            if (details.outerVariablesUsed[p] == param)
                            {

                                // the inner function already uses the outer variable
                                b2 = details.implicitParams[p];
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {

                            // Need to add an implicit parameter to the inner function
                            details.outerVariablesUsed.Add(param);
                            UserFunctionParameter ufp = new UserFunctionParameter();
                            ufp.SetVariableQName(qName);
                            ufp.SetRequiredType(param.GetRequiredType());
                            details.implicitParams.Add(ufp);
                            b2 = ufp;
                        }
                    }

                    if (b2 != null)
                    {
                        return b2;
                    }
                }
            }

            return null;
        }

        public virtual Expression ParseFocusFunction(AnnotationList annotations)
        {
            CheckLanguageVersion40();

            //Tokenizer t = getTokenizer();
            int offset = t.currentTokenStartOffset;
            InlineFunctionDetails details = new InlineFunctionDetails();
            details.outerVariables = new IndexedStack<ILocalBinding>();
            foreach (ILocalBinding lb in RangeVariables)
            {
                details.outerVariables.IPush(lb);
            }

            details.outerVariablesUsed = new List<ILocalBinding>(4);
            details.implicitParams = new List<UserFunctionParameter>(4);
            inlineFunctionStack.IPush(details);
            RangeVariables = new IndexedStack<ILocalBinding>();
            NextToken();
            IList<UserFunctionParameter> @params = new List<UserFunctionParameter>(1);
            Values.SequenceType resultType = Values.SequenceType.ANY_SEQUENCE;
            StructuredQName argQName = new StructuredQName("saxon", NamespaceUri.SAXON, "dot");
            UserFunctionParameter arg = new UserFunctionParameter();
            arg.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
            arg.SetVariableQName(argQName);
            arg.SetSlotNumber(0);
            @params.Add(arg);
            Expression body;
            if (t.currentToken == Token.RCURLY)
            {
                t.LookAhead(); // must be done manually after an RCURLY
                NextToken();
                body = Literal.MakeEmptySequence();
            }
            else
            {
                body = ParseExpression();
                Expect(Token.RCURLY);
                t.LookAhead(); // must be done manually after an RCURLY
                NextToken();
                body.SetRetainedStaticContext(GetStaticContext().MakeRetainedStaticContext());
                LocalVariableReference @ref = new LocalVariableReference(arg);
                body = new ForEach(@ref, body);
            }

            Expression result = MakeInlineFunctionValue(this, AnnotationList.EMPTY, details, @params, resultType, body);
            SetLocation(result, offset);

            // restore the previous stack of range variables
            RangeVariables = details.outerVariables;
            inlineFunctionStack.Pop();
            return result;
        }
        public static bool IsReservedFunctionName(string name, int version)
        {
            int x = Array.BinarySearch(version >= 40 ? reservedFunctionNames40 : reservedFunctionNames31, name, StringComparer.Ordinal);
            return x >= 0;
        }

        public virtual void DeclareRangeVariable(ILocalBinding declaration)
        {
            rangeVariables.IPush(declaration);
        }

        /// <summary>
        /// Note when the most recently declared range variable has gone out of scope
        /// </summary>
        public virtual void UndeclareRangeVariable()
        {
            rangeVariables.Pop();
        }

        protected virtual ILocalBinding FindRangeVariable(StructuredQName qName)
        {
            for (int v = rangeVariables.Size() - 1; v >= 0; v--)
            {
                ILocalBinding b = rangeVariables[v];
                if (b.GetVariableQName().Equals(qName))
                {
                    return b;
                }
            }

            return FindOuterRangeVariable(qName);
        }

        public virtual void SetRangeVariableStack(IndexedStack<ILocalBinding> stack)
        {
            rangeVariables = stack;
        }

        public int MakeFingerprint(string qname, bool useDefault)
        {
            if (scanOnly)
            {
                return StandardNames.XML_SPACE;
            }

            try
            {
                NamespaceUri defaultNS = useDefault ? env.GetDefaultElementNamespace() : NamespaceUri.NULL;
                StructuredQName sq = qNameParser.Parse(qname, defaultNS);
                return env.GetConfiguration().GetNamePool().AllocateFingerprint(sq.GetNamespaceUri(), sq.GetLocalPart());
            }
            catch (XPathException e)
            {
                Grumble(e.Message, e.ErrorCodeQName);
                return -1;
            }
        }

        public StructuredQName MakeStructuredQNameSilently(string qname, NamespaceUri defaultUri)
        {
            if (scanOnly)
            {
                return NamespaceUri.SAXON.QName("dummy");
            }

            return qNameParser.Parse(qname, defaultUri);
        }

        public StructuredQName MakeStructuredQName(string qname, NamespaceUri defaultUri)
        {
            try
            {
                return MakeStructuredQNameSilently(qname, defaultUri);
            }
            catch (XPathException err)
            {
                Grumble(err.Message, err.ErrorCodeQName);
                return NamespaceUri.NULL.QName("error"); // Not executed; here to keep the compiler happy
            }
        }

        public INodeName MakeNodeName(string qname, bool useDefault)
        {
            StructuredQName sq = MakeStructuredQNameSilently(qname, useDefault ? env.GetDefaultElementNamespace() : NamespaceUri.NULL);
            string prefix = sq.GetPrefix();
            NamespaceUri uri = sq.GetNamespaceUri();
            string local = sq.GetLocalPart();
            if (uri.IsEmpty())
            {
                int fp = env.GetConfiguration().GetNamePool().AllocateFingerprint(NamespaceUri.NULL, local);
                return new NoNamespaceName(local, fp);
            }
            else
            {
                int fp = env.GetConfiguration().GetNamePool().AllocateFingerprint(uri, local);
                return new FingerprintedQName(prefix, uri, local, fp);
            }
        }

        public virtual NodeTest MakeNameTest(int nodeKind, string qname, bool useDefault)
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            NamespaceUri defaultNS = NamespaceUri.NULL;
            if (useDefault && nodeKind == Types.Type.ELEMENT && !qname.StartsWith("Q{", StringComparison.Ordinal) && !qname.Contains(":"))
            {
                UnprefixedElementMatchingPolicy policy = env.GetUnprefixedElementMatchingPolicy();
                switch (policy)
                {
                    case UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE:
                        defaultNS = env.GetDefaultElementNamespace();
                        break;
                    case UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE_OR_NONE:
                        defaultNS = env.GetDefaultElementNamespace();
                        StructuredQName q = MakeStructuredQName(qname, defaultNS);
                        int fp1 = pool.AllocateFingerprint(q.GetNamespaceUri(), q.GetLocalPart());
                        NameTest test1 = new NameTest(nodeKind, fp1, pool);
                        int fp2 = pool.AllocateFingerprint(NamespaceUri.NULL, q.GetLocalPart());
                        NameTest test2 = new NameTest(nodeKind, fp2, pool);
                        return new CombinedNodeTest(test1, Token.UNION, test2);
                    case UnprefixedElementMatchingPolicy.ANY_NAMESPACE:
                        if (!NameChecker.IsValidNCName(StringTool.CodePoints(qname)))
                        {
                            Grumble("Invalid name '" + qname + "'");
                        }

                        return new LocalNameTest(pool, nodeKind, qname);
                }
            }

            StructuredQName qName = MakeStructuredQName(qname, defaultNS);
            int fp = pool.AllocateFingerprint(qName.GetNamespaceUri(), qName.GetLocalPart());
            return new NameTest(nodeKind, fp, pool);
        }

        public virtual IQNameTest MakeQNameTest(int nodeKind, string qname)
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            StructuredQName q = MakeStructuredQName(qname, NamespaceUri.NULL);
            int fp = pool.AllocateFingerprint(q.GetNamespaceUri(), q.GetLocalPart());
            return new NameTest(nodeKind, fp, pool);
        }

        public virtual NamespaceTest MakeNamespaceTest(int nodeKind, string prefix)
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            if (scanOnly)
            {

                // return an arbitrary namespace if we're only doing a syntax check
                return new NamespaceTest(pool, nodeKind, NamespaceUri.SAXON);
            }

            if (prefix.StartsWith("Q{", StringComparison.Ordinal))
            {
                string uri = prefix.Substring(2, prefix.Length - 4) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                return new NamespaceTest(pool, nodeKind, NamespaceUri.Of(uri));
            }

            try
            {
                StructuredQName sq = qNameParser.Parse(prefix + ":dummy", NamespaceUri.NULL);
                return new NamespaceTest(pool, nodeKind, sq.GetNamespaceUri());
            }
            catch (XPathException err)
            {
                Grumble(err.Message, err.ErrorCodeQName);
                return null;
            }
        }

        public virtual LocalNameTest MakeLocalNameTest(int nodeKind, string localName)
        {
            if (!NameChecker.IsValidNCName(StringTool.CodePoints(localName)))
            {
                Grumble("Local name [" + localName + "] contains invalid characters");
            }

            return new LocalNameTest(env.GetConfiguration().GetNamePool(), nodeKind, localName);
        }

        protected virtual void SetLocation(Expression exp)
        {
            SetLocation(exp, t.currentTokenStartOffset);
        }

        public virtual void SetLocation(Expression exp, int offset)
        {
            if (exp != null)
            {
                if (exp.GetLocation() == null || exp.GetLocation() == Loc.NONE)
                {
                    exp.SetLocation(MakeLocation(offset));
                }
            }
        }

        public virtual ILocation MakeLocation(int offset)
        {
            int line = t.GetLineNumber(offset);
            int column = t.GetColumnNumber(offset);
            return MakeNestedLocation(env.GetContainingLocation(), line, column, null);
        }

        public virtual void SetLocation(Clause clause, int offset)
        {
            int line = t.GetLineNumber(offset);
            int column = t.GetColumnNumber(offset);
            ILocation loc = MakeNestedLocation(env.GetContainingLocation(), line, column, null);
            clause.Location = loc;
            clause.SetPackageData(env.GetPackageData());
        }
        public virtual ILocation MakeLocation()
        {
            if (t.GetLineNumber() == mostRecentLocation.GetLineNumber() && t.GetColumnNumber() == mostRecentLocation.GetColumnNumber() && ((env.GetSystemId() == null && mostRecentLocation.GetSystemId() == null) || env.GetSystemId().Equals(mostRecentLocation.GetSystemId())))
            {
                return mostRecentLocation;
            }
            else
            {
                int line = t.GetLineNumber();
                int column = t.GetColumnNumber();
                mostRecentLocation = MakeNestedLocation(env.GetContainingLocation(), line, column, null);
                return mostRecentLocation;
            }
        }

        public virtual ILocation MakeNestedLocation(ILocation containingLoc, int line, int column, string nearbyText)
        {
            if (containingLoc is Loc && containingLoc.GetLineNumber() <= 1 && containingLoc.GetColumnNumber() == -1 && nearbyText == null)
            {

                // No extra information available about the container
                return new Loc(env.GetSystemId(), line + 1, column + 1);
            }
            else
            {
                return new NestedLocation(containingLoc, line, column, nearbyText);
            }
        }

        public virtual Expression MakeTracer(Expression exp, StructuredQName qName)
        {
            exp.SetRetainedStaticContextLocally(env.MakeRetainedStaticContext());
            return exp; //        if (codeInjector != null) {
        }

        protected virtual bool IsKeyword(string s)
        {
            return t.currentToken == Token.NAME && t.currentTokenValue.Equals(s);
        }

        public virtual void SetScanOnly(bool scanOnly)
        {
            this.scanOnly = scanOnly;
        }

        public virtual void SetAllowAbsentExpression(bool allowEmpty)
        {
            this.allowAbsentExpression = allowEmpty;
        }

        public virtual bool IsAllowAbsentExpression()
        {
            return this.allowAbsentExpression;
        }
        public enum ParsedLanguage
        {
            XPATH,
            XSLT_PATTERN,
            SEQUENCE_TYPE,
            XQUERY,
            EXTENDED_ITEM_TYPE
        }

        public class InlineFunctionDetails
        {
            public IndexedStack<ILocalBinding> outerVariables; // Local variables defined in the immediate outer scope (the father scope)
            public IList<ILocalBinding> outerVariablesUsed; // Local variables from the outer scope that are actually used
            public IList<UserFunctionParameter> implicitParams; // Parameters corresponding (1:1) with the above
        }

        public interface IAccelerator
        {
            Expression Parse(Tokenizer t, IStaticContext env, string expression, int start, int terminator);
        }

        internal sealed class NestedLocation : ILocation
        {
            private readonly ILocation containingLocation;
            private readonly int localLineNumber;
            private readonly int localColumnNumber;
            private readonly string nearbyText;

            public int LocalLineNumber => localLineNumber;

            public string NearbyText => nearbyText;
            public NestedLocation(ILocation containingLocation, int localLineNumber, int localColumnNumber, string nearbyText)
            {
                this.containingLocation = containingLocation.SaveLocation();
                this.localLineNumber = localLineNumber;
                this.localColumnNumber = localColumnNumber;
                this.nearbyText = nearbyText;
            }

            public ILocation GetContainingLocation()
            {
                return containingLocation;
            }

            public int GetColumnNumber()
            {
                return localColumnNumber;
            }

            public string GetSystemId()
            {
                return containingLocation.GetSystemId();
            }

            public string GetPublicId()
            {
                return containingLocation.GetPublicId();
            }

            public int GetLineNumber()
            {
                return containingLocation.GetLineNumber() + localLineNumber;
            }

            public ILocation SaveLocation()
            {
                return this;
            }
        }
    }
}

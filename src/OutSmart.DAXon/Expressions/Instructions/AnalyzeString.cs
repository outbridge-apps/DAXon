////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An xsl:analyze-string element in the stylesheet. New at XSLT 2.0
    /// </summary>
    internal sealed class AnalyzeString : Instruction, IContextOriginator
    {
        private static readonly OperandRole ACTION = new OperandRole(OperandRole.USES_NEW_FOCUS | OperandRole.HIGHER_ORDER, OperandUsage.NAVIGATION);
        private static readonly OperandRole SELECT = new OperandRole(OperandRole.SETS_NEW_FOCUS, OperandUsage.ABSORPTION, SequenceType.SINGLE_STRING);
        private readonly Operand selectOp;
        private readonly Operand regexOp;
        private readonly Operand flagsOp;
        private Operand matchingOp;
        private Operand nonMatchingOp;
        private IRegularExpression pattern;

        public Expression Select
        {
            get => selectOp.GetChildExpression(); set
            {
                selectOp.SetChildExpression(value);
            }
        }

        public Expression Regex
        {
            get => regexOp.GetChildExpression(); set
            {
                regexOp.SetChildExpression(value);
            }
        }

        public Expression Flags
        {
            get => flagsOp.GetChildExpression(); set
            {
                flagsOp.SetChildExpression(value);
            }
        }

        public Expression Matching
        {
            get => matchingOp == null ? null : matchingOp.GetChildExpression(); set
            {
                if (matchingOp != null)
                {
                    matchingOp.SetChildExpression(value);
                }
                else
                {
                    matchingOp = new Operand(this, value, ACTION);
                }
            }
        }

        public Expression NonMatching
        {
            get => nonMatchingOp == null ? null : nonMatchingOp.GetChildExpression(); set
            {
                if (nonMatchingOp != null)
                {
                    nonMatchingOp.SetChildExpression(value);
                }
                else
                {
                    nonMatchingOp = new Operand(this, value, ACTION);
                }
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_ANALYZE_STRING;

        public override int ImplementationMethod => Expression.PROCESS_METHOD | Expression.ITERATE_METHOD;

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public IRegularExpression PatternExpression => pattern;

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override string ExpressionName => "analyzeString";
        public AnalyzeString(Expression select, Expression regex, Expression flags, Expression matching, Expression nonMatching, IRegularExpression pattern)
        {
            selectOp = new Operand(this, select, SELECT);
            regexOp = new Operand(this, regex, OperandRole.SINGLE_ATOMIC);
            flagsOp = new Operand(this, flags, OperandRole.SINGLE_ATOMIC);
            if (matching != null)
            {
                matchingOp = new Operand(this, matching, ACTION);
            }

            if (nonMatching != null)
            {
                nonMatchingOp = new Operand(this, nonMatching, ACTION);
            }

            this.pattern = pattern;
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(selectOp, regexOp, flagsOp, matchingOp, nonMatchingOp);
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            selectOp.TypeCheck(visitor, contextInfo);
            regexOp.TypeCheck(visitor, contextInfo);
            flagsOp.TypeCheck(visitor, contextInfo);
            if (matchingOp != null)
            {
                matchingOp.TypeCheck(visitor, config.MakeContextItemStaticInfo(BuiltInAtomicType.STRING, false));
            }

            if (nonMatchingOp != null)
            {
                nonMatchingOp.TypeCheck(visitor, config.MakeContextItemStaticInfo(BuiltInAtomicType.STRING, false));
            }

            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "analyze-string/select", 0);
            SequenceType required = SequenceType.OPTIONAL_STRING;

            // see bug 7976
            Select = tc.StaticTypeCheck(Select, required, role, visitor);
            role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "analyze-string/regex", 0);
            Regex = tc.StaticTypeCheck(Regex, SequenceType.SINGLE_STRING, role, visitor);
            role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "analyze-string/flags", 0);
            Flags = tc.StaticTypeCheck(Flags, SequenceType.SINGLE_STRING, role, visitor);
            return this;
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            selectOp.Optimize(visitor, contextInfo);
            regexOp.Optimize(visitor, contextInfo);
            flagsOp.Optimize(visitor, contextInfo);
            if (matchingOp != null)
            {
                matchingOp.Optimize(visitor, config.MakeContextItemStaticInfo(BuiltInAtomicType.STRING, false));
            }

            if (nonMatchingOp != null)
            {
                nonMatchingOp.Optimize(visitor, config.MakeContextItemStaticInfo(BuiltInAtomicType.STRING, false));
            }

            IList<string> warnings = new List<string>();
            PrecomputeRegex(config, warnings);
            foreach (string w in warnings)
            {
                visitor.StaticContext.IssueWarning(w, DAXonErrorCode.SXWN9022, GetLocation());
            }

            return this;
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public void PrecomputeRegex(Configuration config, IList<string> warnings)
        {
            if (pattern == null && Regex is StringLiteral && Flags is StringLiteral)
            {
                try
                {
                    string regex = ((StringLiteral)this.Regex).Stringify();
                    string flagstr = ((StringLiteral)Flags).Stringify();
                    string hostLang = "XP30";
                    pattern = config.CompileRegularExpression(StringView.Tidy(regex), flagstr, hostLang, warnings);
                }
                catch (XPathException err)
                {
                    if (err.HasErrorCode("XTDE1150"))
                    {
                        throw;
                    }

                    if (err.HasErrorCode("FORX0001"))
                    {
                        InvalidRegex("Error in regular expression flags: " + err, err.ErrorCodeQName);
                    }
                    else
                    {
                        InvalidRegex("Error in regular expression: " + err, err.ErrorCodeQName);
                    }
                }
            }
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        private void InvalidRegex(string message, StructuredQName errorCode)
        {
            pattern = null;
            throw new XPathException(message).WithErrorCode(errorCode).WithLocation(GetLocation());
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override Expression Copy(RebindingMap rm)
        {
            AnalyzeString a2 = new AnalyzeString(Copy(Select, rm), Copy(Regex, rm), Copy(Flags, rm), Copy(Matching, rm), Copy(NonMatching, rm), pattern);
            ExpressionTool.CopyLocationInfo(this, a2);
            return a2;
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        private Expression Copy(Expression exp, RebindingMap rebindings)
        {
            return exp == null ? null : exp.Copy(rebindings);
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            if (Matching != null)
            {
                Matching.CheckPermittedContents(parentType, false);
            }

            if (NonMatching != null)
            {
                NonMatching.CheckPermittedContents(parentType, false);
            }
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override ItemType GetItemType()
        {
            if (Matching != null)
            {
                if (NonMatching != null)
                {
                    TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                    return Types.Type.GetCommonSuperType(Matching.GetItemType(), NonMatching.GetItemType(), th);
                }
                else
                {
                    return Matching.GetItemType();
                }
            }
            else
            {
                if (NonMatching != null)
                {
                    return NonMatching.GetItemType();
                }
                else
                {
                    return ErrorType.GetInstance();
                }
            }
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override int ComputeDependencies()
        {

            // some of the dependencies in the "action" part and in the grouping and sort keys aren't relevant,
            // because they don't depend on values set outside the for-each-group expression
            int dependencies = 0;
            dependencies |= Select.Dependencies;
            dependencies |= Regex.Dependencies;
            dependencies |= Flags.Dependencies;
            if (Matching != null)
            {
                dependencies |= Matching.Dependencies & ~(StaticProperty.DEPENDS_ON_FOCUS | StaticProperty.DEPENDS_ON_REGEX_GROUP);
            }

            if (NonMatching != null)
            {
                dependencies |= NonMatching.Dependencies & ~(StaticProperty.DEPENDS_ON_FOCUS | StaticProperty.DEPENDS_ON_REGEX_GROUP);
            }

            return dependencies;
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("analyzeString", this);
            @out.SetChildRole("select");
            Select.Export(@out);
            @out.SetChildRole("regex");
            Regex.Export(@out);
            @out.SetChildRole("flags");
            Flags.Export(@out);
            if (Matching != null)
            {
                @out.SetChildRole("matching");
                Matching.Export(@out);
            }

            if (NonMatching != null)
            {
                @out.SetChildRole("nonMatching");
                NonMatching.Export(@out);
            }

            @out.EndElement();
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        public override Elaborator GetElaborator()
        {
            return new AnalyzeStringElaborator();
        }

        /// <returns>the compiled regular expression, if it was known statically</returns>
        private delegate IRegularExpression IRegexEvaluator(IXPathContext context); /*Java SAM interface -> delegate (lambda call sites)*/

        /// <returns>the compiled regular expression, if it was known statically</returns>
        internal sealed class AnalyzeStringElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                AnalyzeString expr = (AnalyzeString)GetExpression();
                IUnicodeStringEvaluator input = expr.Select.MakeElaborator().ElaborateForUnicodeString(true);
                IPullEvaluator matching = expr.Matching == null ? null : expr.Matching.MakeElaborator().ElaborateForPull();
                IPullEvaluator nonMatching = expr.NonMatching == null ? null : expr.NonMatching.MakeElaborator().ElaborateForPull();
                IRegexEvaluator regexSupplier = GetRegexSupplier(expr);
                return (context) =>
                {
                    IRegularExpression re = regexSupplier(context);
                    UnicodeString @in = input.Eval(context);
                    IRegexIterator iter = re.Analyze(@in);
                    XPathContextMajor c2 = context.NewContext();
                    c2.Origin = expr;
                    c2.TrackFocus(iter);
                    c2.SetCurrentRegexIterator(iter);
                    return new ContextMappingIterator((cxt) =>
                    {
                        if (iter.IsMatching())
                        {
                            if (matching != null)
                            {
                                return matching.Iterate(c2);
                            }
                        }
                        else
                        {
                            if (nonMatching != null)
                            {
                                return nonMatching.Iterate(c2);
                            }
                        }

                        return EmptyIterator.GetInstance();
                    }, c2);
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                AnalyzeString expr = (AnalyzeString)GetExpression();
                IUnicodeStringEvaluator input = expr.Select.MakeElaborator().ElaborateForUnicodeString(true);
                IPushEvaluator matching = expr.Matching == null ? null : expr.Matching.MakeElaborator().ElaborateForPush();
                IPushEvaluator nonMatching = expr.NonMatching == null ? null : expr.NonMatching.MakeElaborator().ElaborateForPush();
                IRegexEvaluator regexSupplier = GetRegexSupplier(expr);
                return (@out, context) =>
                {
                    IRegularExpression re = regexSupplier(context);
                    UnicodeString @in = input.Eval(context);
                    IRegexIterator iter = re.Analyze(@in);
                    XPathContextMajor c2 = context.NewContext();
                    c2.Origin = expr;
                    IFocusIterator focus = c2.TrackFocus(iter);
                    c2.SetCurrentRegexIterator(iter);
                    while (focus.Next() != null)
                    {
                        if (iter.IsMatching())
                        {
                            if (matching != null)
                            {
                                DispatchTailCall(matching.ProcessLeavingTail(@out, c2));
                            }
                        }
                        else
                        {
                            if (nonMatching != null)
                            {
                                DispatchTailCall(nonMatching.ProcessLeavingTail(@out, c2));
                            }
                        }
                    }

                    return null;
                };
            }

            private IRegexEvaluator GetRegexSupplier(AnalyzeString expr)
            {
                IRegularExpression pattern = expr.PatternExpression;
                IRegexEvaluator regexSupplier;
                if (expr.pattern != null)
                {

                    // regex and flags were known statically
                    regexSupplier = (context) => pattern;
                }
                else
                {

                    // regex or flags is dynamic
                    IStringEvaluator flagsEval = expr.Flags.MakeElaborator().ElaborateForString(true);
                    IUnicodeStringEvaluator regexEval = expr.Regex.MakeElaborator().ElaborateForUnicodeString(false);
                    regexSupplier = (context) =>
                    {
                        string flagsStr = flagsEval.Eval(context);
                        UnicodeString regexStr = regexEval.Eval(context);
                        return context.GetConfiguration().CompileRegularExpression(regexStr, flagsStr, "XP31", null);
                    };
                }

                return regexSupplier;
            }
        }
    }
}

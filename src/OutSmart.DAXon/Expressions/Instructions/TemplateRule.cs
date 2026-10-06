////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class TemplateRule : IRuleTarget, ILocation, IExpressionOwner, ITraceableComponent
    {
        protected Mode mode;
        protected Expression body;
        protected volatile IPushEvaluator bodyEvaluator;   // volatile: published once, read hot (round 11)
        protected Patterns.Pattern matchPattern;
        private Values.SequenceType requiredType;
        private bool declaredStreamable;
        private Types.ItemType requiredContextItemType = AnyItemType.GetInstance();
        private bool absentFocus;
        private SlotManager stackFrameMap;
        private PackageData packageData;
        private string systemId;
        private int lineNumber;
        private int columnNumber;
        private readonly IList<Rule> rules = new List<Rule>();

        private volatile IPushEvaluator atomicBodyEvaluator = null;

        public virtual int ComponentKind => StandardNames.XSL_TEMPLATE;

        public virtual Patterns.Pattern MatchPattern
        {
            get => matchPattern; set
            {
                if (matchPattern != value)
                {
                    foreach (Rule r in rules)
                    {
                        r.Pattern = value;
                    }
                }

                matchPattern = value;
            }
        }

        public virtual SlotManager StackFrameMap
        {
            get => stackFrameMap; set
            {
                stackFrameMap = value;
            }
        }

        public virtual Values.SequenceType RequiredType
        {
            get
            {
                if (requiredType == null)
                {
                    return Values.SequenceType.ANY_SEQUENCE;
                }
                else
                {
                    return requiredType;
                }
            }
            set
            {
                requiredType = value;
            }
        }

        public virtual IList<Rule> Rules => rules;

        public virtual int ContainerGranularity => 0;

        public virtual IList<LocalParam> LocalParams
        {
            get
            {
                IList<LocalParam> result = new List<LocalParam>();
                GatherLocalParams(GetBody(), result);
                return result;
            }
        }

        public virtual string TracingTag => "xsl:template";
        public TemplateRule()
        {
        }

        public virtual void SetMode(Mode m)
        {
            this.mode = m;
        }

        public virtual Mode GetMode()
        {
            return mode;
        }

        public virtual Expression GetBody()
        {
            return body;
        }

        public virtual Expression GetChildExpression()
        {
            return body;
        }

        public virtual ILocation GetLocation()
        {
            return this;
        }

        public virtual void GatherProperties(Action<string, object> consumer)
        {
            consumer("match",MatchPattern.ToShortString());
        }

        public virtual void SetContextItemRequirements(Types.ItemType type, bool absentFocus)
        {
            requiredContextItemType = type;
            this.absentFocus = absentFocus;
        }

        public virtual void SetBody(Expression body)
        {
            this.body = body;
        }

        public virtual void RegisterRule(Rule rule)
        {
            rules.Add(rule);
        }

        public virtual PackageData GetPackageData()
        {
            return packageData;
        }

        public virtual void SetPackageData(PackageData data)
        {
            this.packageData = data;
        }

        public virtual string GetPublicId()
        {
            return null;
        }

        public virtual string GetSystemId()
        {
            return systemId;
        }

        public virtual void SetSystemId(string id)
        {
            this.systemId = id;
        }

        public virtual int GetLineNumber()
        {
            return lineNumber;
        }

        public virtual void SetLineNumber(int line)
        {
            this.lineNumber = line;
        }

        public virtual void SetColumnNumber(int col)
        {
            this.columnNumber = col;
        }

        public virtual int GetColumnNumber()
        {
            return columnNumber;
        }

        public virtual ILocation SaveLocation()
        {
            return this;
        }

        public virtual Types.ItemType GetRequiredContextItemType()
        {
            return requiredContextItemType;
        }

        public virtual bool IsAbsentFocus()
        {
            return absentFocus;
        }

        private static void GatherLocalParams(Expression exp, IList<LocalParam> result)
        {
            if (exp is LocalParam)
            {
                result.Add((LocalParam)exp);
            }
            else
            {
                foreach (Operand o in exp.Operands())
                {
                    GatherLocalParams(o.GetChildExpression(), result);
                }
            }
        }

        public virtual void PrepareInitializer(Compilation compilation, ComponentDeclaration decl)
        {
        }

        public virtual void Initialize()
        {
        }

        public virtual void Apply(Outputter output, XPathContextMajor context)
        {
            ITailCall tc = ApplyLeavingTail(output, context);
            while (tc != null)
            {
                tc = tc.ProcessLeavingTail();
            }
        }

        public virtual ITailCall ApplyLeavingTail(Outputter output, IXPathContext context)
        {

            // Only the templates that declare a required context item type pay for the type-hierarchy
            // lookup + the Matches check; the common case (no declared type) skips both, per template
            // instantiation (millions on a dispatch pass).
            if (requiredContextItemType != AnyItemType.GetInstance())
            {
                TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
                if (!requiredContextItemType.Matches(context.GetContextItem(), th))
                {
                    RoleDiagnostic role = new RoleDiagnostic(RoleDiagnostic.MISC, "context item for the template rule", 0);
                    string message = role.ComposeErrorMessage(requiredContextItemType, context.GetContextItem(), th);
                    throw new XPathException(message, "XTTE0590").WithLocation(this).AsTypeError();
                }
            }

            if (absentFocus)
            {
                context = context.NewMinorContext();
                context.SetCurrentIterator(null);
            }

            try
            {
                EnsureBodyEvaluatorExists();
                return bodyEvaluator.ProcessLeavingTail(output, context);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                // A deeper recursion level tripped the stack guard (see StackGuard.Probe).
                // Filtered: one such catch per recursion level.
                throw e.Describe("Too many nested apply-templates calls. The stylesheet may be looping.", DAXonErrorCode.SXLM0001, this);
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException().MaybeWithLocation(this).MaybeWithContext(context);
            }
            catch (XPathException e) when (!(e is XPathException.StackOverflow))
            {
                // StackOverflow flies through decorating rethrows untouched — see the
                // DocumentInstr elaborator note (deep-unwind stack discipline, round AQ).
                throw e.MaybeWithLocation(this).MaybeWithContext(context);
            }
            catch (Exception e2) when (!(e2 is XPathException) && !(e2 is RecursionDepthError) && !(e2 is OutOfMemoryException))
            {
                // RecursionDepthError excluded: it is not an internal error, and wrapping it here
                // would re-enter dispatch once per recursion level - the very cost the guard exists
                // to avoid. OutOfMemoryException neither: the host must see the runtime's own signal.
                string message = "Internal error evaluating template rule " + (GetLineNumber() > 0 ? " at line " + GetLineNumber() : "") + (GetSystemId() != null ? " in module " + GetSystemId() : "");
                throw new InvalidOperationException(message, e2);
            }
        }

        private void EnsureBodyEvaluatorExists()
        {
            if (bodyEvaluator == null)
            {
                bodyEvaluator = atomicBodyEvaluator = (atomicBodyEvaluator == null ? body.MakeElaborator().ElaborateForPush() : atomicBodyEvaluator);
            }
        }
        public virtual void Export(ExpressionPresenter presenter)
        {

            // NOT USED - see Rule.export
            throw new NotSupportedException();
        }

        public virtual void SetDeclaredStreamable(bool streamable)
        {
        }

        public virtual bool IsDeclaredStreamable()
        {

            // Overridden in Saxon-EE
            return false;
        }

        public virtual void ExplainProperties(ExpressionPresenter presenter)
        {
            if (GetRequiredContextItemType() != AnyItemType.GetInstance())
            {
                Values.SequenceType st = Values.SequenceType.MakeSequenceType(GetRequiredContextItemType(), StaticProperty.EXACTLY_ONE);
                presenter.EmitAttribute("cxt", st.ToAlphaCode());
            }

            string flags = "";
            if (!absentFocus)
            {
                flags += "s";
            }

            presenter.EmitAttribute("flags", flags);
            if (RequiredType != Values.SequenceType.ANY_SEQUENCE)
            {
                presenter.EmitAttribute("as", RequiredType.ToAlphaCode());
            }

            presenter.EmitAttribute("line", GetLineNumber() + "");
            presenter.EmitAttribute("module", GetSystemId());
            if (IsDeclaredStreamable())
            {
                presenter.EmitAttribute("streamable", "1");
            }
        }

        protected virtual void CopyTo(TemplateRule tr)
        {
            if (body != null)
            {
                tr.body = body.Copy(new RebindingMap());
            }

            if (matchPattern != null)
            {
                tr.matchPattern = (Patterns.Pattern)matchPattern.Copy(new RebindingMap());
            }

            tr.requiredType = requiredType;
            tr.declaredStreamable = declaredStreamable; // ? this can vary from one mode to another
            tr.requiredContextItemType = requiredContextItemType;
            tr.absentFocus = absentFocus;
            tr.stackFrameMap = stackFrameMap;
            tr.packageData = packageData;
            tr.systemId = systemId;
            tr.lineNumber = lineNumber;
        }

        public virtual void SetChildExpression(Expression expr)
        {
            SetBody(expr);
        }

        public virtual StructuredQName GetObjectName()
        {
            return null;
        }
    }
}
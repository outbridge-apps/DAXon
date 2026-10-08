////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class NamedTemplate : Actor, ITraceableComponent
    {
        private StructuredQName templateName;
        private SequenceType requiredType;
        private ItemType requiredContextItemType = AnyItemType.GetInstance();
        private bool mayOmitContextItem = true;
        private bool absentFocus;
        private IList<LocalParamInfo> localParamDetails = new List<LocalParamInfo>(4);
        private IPushEvaluator bodyEvaluator;

        public override string TracingTag => "xsl:template";

        public virtual StructuredQName TemplateName
        {
            get => templateName; set
            {
                this.templateName = value;
            }
        }

        public virtual SequenceType RequiredType
        {
            get
            {
                if (requiredType == null)
                {
                    return SequenceType.ANY_SEQUENCE;
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

        public virtual IList<LocalParamInfo> LocalParamDetails
        {
            get => localParamDetails; set
            {
                localParamDetails = value;
            }
        }
        public NamedTemplate(StructuredQName templateName, Configuration config)
        {
            TemplateName = templateName;
        }

        public virtual void SetContextItemRequirements(ItemType type, bool mayBeOmitted, bool absentFocus)
        {
            requiredContextItemType = type;
            mayOmitContextItem = mayBeOmitted;
            this.absentFocus = absentFocus;
        }

        public override SymbolicName GetSymbolicName()
        {
            if (TemplateName == null)
            {
                return null;
            }
            else
            {
                return new SymbolicName(StandardNames.XSL_TEMPLATE, TemplateName);
            }
        }

        public void GatherProperties(Action<string, object> consumer)
        {
            consumer("name",TemplateName);
        }

        public override void SetBody(Expression body)
        {
            base.SetBody(body); //bodyIsTailCallReturner = (body instanceof TailCallReturner);
        }

        public StructuredQName GetObjectName()
        {
            return templateName;
        }

        public virtual ItemType GetRequiredContextItemType()
        {
            return requiredContextItemType;
        }

        public virtual bool IsMayOmitContextItem()
        {
            return mayOmitContextItem;
        }

        public virtual bool IsAbsentFocus()
        {
            return absentFocus;
        }

        public virtual LocalParamInfo GetLocalParamInfo(StructuredQName id)
        {
            IList<LocalParamInfo> @params = LocalParamDetails;
            foreach (LocalParamInfo lp in @params)
            {
                if (lp.name.Equals(id))
                {
                    return lp;
                }
            }

            return null;
        }

        public virtual ITailCall Expand(Outputter output, IXPathContext context)
        {
            // Every xsl:call-template invocation path funnels through here — one probe bounds
            // named-template recursion depth (RecursionDepthError -> SXLM0001).
            StackGuard.Probe();
            IItem contextItem = context.GetContextItem();
            if (contextItem == null)
            {
                if (!mayOmitContextItem)
                {
                    throw new XPathException("The template requires a context item, but none has been supplied", "XTTE3090").WithLocation(GetLocation()).AsTypeError();
                }
            }
            else
            {
                TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
                if (requiredContextItemType != AnyItemType.GetInstance() && !requiredContextItemType.Matches(contextItem, th))
                {
                    RoleDiagnostic role = new RoleDiagnostic(RoleDiagnostic.MISC, "context item for the named template", 0);
                    string message = role.ComposeErrorMessage(requiredContextItemType, contextItem, th);
                    throw new XPathException(message, "XTTE0590").WithLocation(GetLocation()).AsTypeError();
                }

                if (absentFocus)
                {
                    context = context.NewMinorContext();
                    context.SetCurrentIterator(null);
                }
            }

            lock (syncLock)
            {
                if (bodyEvaluator == null)
                {
                    bodyEvaluator = GetBody().MakeElaborator().ElaborateForPush();
                }
            }

            try
            {
                return bodyEvaluator.ProcessLeavingTail(output, context);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                // A deeper recursion level tripped the stack guard; describe at the nearest body
                // so every invocation path reports SXLM0001 (call sites without their own catch
                // included). Filtered: one such catch per recursion level.
                throw e.Describe("Too many nested template or function calls. The stylesheet may be looping.", DAXonErrorCode.SXLM0001, GetLocation());
            }
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("template");
            presenter.EmitAttribute("name", TemplateName);
            ExplainProperties(presenter);
            presenter.EmitAttribute("slots", "" + GetStackFrameMap().NumberOfVariables);
            if (GetBody() != null)
            {
                presenter.SetChildRole("body");
                GetBody().Export(presenter);
            }

            presenter.EndElement();
        }

        public virtual void ExplainProperties(ExpressionPresenter presenter)
        {
            if (GetRequiredContextItemType() != AnyItemType.GetInstance())
            {
                SequenceType st = SequenceType.MakeSequenceType(GetRequiredContextItemType(), StaticProperty.EXACTLY_ONE);
                presenter.EmitAttribute("cxt", st.ToAlphaCode());
            }

            string flags = "";
            if (mayOmitContextItem)
            {
                flags = "o";
            }

            if (!absentFocus)
            {
                flags += "s";
            }

            presenter.EmitAttribute("flags", flags);
            if (RequiredType != SequenceType.ANY_SEQUENCE)
            {
                presenter.EmitAttribute("as", RequiredType.ToAlphaCode());
            }

            presenter.EmitAttribute("line", GetLineNumber() + "");
            presenter.EmitAttribute("module", GetSystemId());
        }

        public class LocalParamInfo
        {
            public StructuredQName name;
            public SequenceType requiredType;
            public bool isRequired;
            public bool isTunnel;
        }
    }
}
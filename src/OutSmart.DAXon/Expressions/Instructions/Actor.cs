////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public abstract class Actor : IExpressionOwner, ILocation
    {
        protected readonly object syncLock = new object();
        protected Expression body;
        private string systemId;
        private int lineNumber;
        private int columnNumber;
        private SlotManager stackFrameMap;
        private PackageData packageData;
        private Component declaringComponent;
        private Visibility declaredVisibility = Visibility.UNDEFINED;
        private RetainedStaticContext retainedStaticContext;
        private IPushEvaluator bodyEvaluator;
        public virtual StructuredQName ComponentName => GetSymbolicName().ComponentName;

        public virtual string TracingTag => StandardNames.GetLocalName(GetSymbolicName().ComponentKind);

        public virtual Component DeclaringComponent
        {
            get => declaringComponent; set
            {
                declaringComponent = value;
            }
        }

        public virtual Visibility DeclaredVisibility
        {
            get => declaredVisibility; set
            {
                this.declaredVisibility = value;
            }
        }
        public Actor()
        {
        }

        public abstract SymbolicName GetSymbolicName();

        public virtual void SetPackageData(PackageData packageData)
        {
            this.packageData = packageData;
        }

        public virtual PackageData GetPackageData()
        {
            return packageData;
        }

        public virtual Component MakeDeclaringComponent(Visibility visibility, StylesheetPackage declaringPackage)
        {
            if (declaringComponent == null)
            {
                declaringComponent = Component.MakeComponent(this, visibility, VisibilityProvenance.DEFAULTED, declaringPackage, declaringPackage);
            }

            return declaringComponent;
        }

        public virtual Component ObtainDeclaringComponent(StyleElement declaration)
        {
            if (declaringComponent == null)
            {
                StylesheetPackage declaringPackage = declaration.ContainingPackage;
                Visibility defaultVisibility = declaration is XSLGlobalParam ? Visibility.PUBLIC : Visibility.PRIVATE;
                Visibility declaredVisibility = declaration.DeclaredVisibility;
                Visibility actualVisibility = declaredVisibility == Visibility.UNDEFINED ? defaultVisibility : declaredVisibility;
                VisibilityProvenance provenance = declaredVisibility == Visibility.UNDEFINED ? VisibilityProvenance.DEFAULTED : VisibilityProvenance.EXPLICIT;
                declaringComponent = Component.MakeComponent(this, actualVisibility, provenance, declaringPackage, declaringPackage);
            }

            return declaringComponent;
        }

        public virtual void AllocateAllBindingSlots(StylesheetPackage pack)
        {
            if (GetBody() != null && DeclaringComponent.DeclaringPackage == pack && packageData.IsXSLT())
            {
                AllocateBindingSlotsRecursive(pack, this, GetBody(), DeclaringComponent.ComponentBindings);
            }
        }

        public static void AllocateBindingSlotsRecursive(StylesheetPackage pack, Actor p, Expression exp, IList<ComponentBinding> bindings)
        {
            if (exp is IComponentInvocation)
            {
                p.ProcessComponentReference(pack, (IComponentInvocation)exp, bindings);
            }

            foreach (Operand o in exp.Operands())
            {
                AllocateBindingSlotsRecursive(pack, p, o.GetChildExpression(), bindings);
            }
        }

        private void ProcessComponentReference(StylesheetPackage pack, IComponentInvocation invocation, IList<ComponentBinding> bindings)
        {
            SymbolicName name = invocation.GetSymbolicName();
            if (name == null)
            {

                // there is no target component, e.g. with apply-templates mode="#current"
                return;
            }

            Component target = pack.GetComponent(name);
            if (target == null && name.ComponentName.HasURI(NamespaceUri.XSLT) && name.ComponentName.GetLocalPart().Equals("original"))
            {
                target = pack.GetOverriddenComponent(GetSymbolicName());
            }

            if (target == null)
            {
                throw new InvalidOperationException("Target of component reference " + name + " is undefined");
            }

            if (invocation.BindingSlot >= 0)
            {
                throw new InvalidOperationException("**** Component reference " + name + " is already bound");
            }

            ComponentBinding cb = new ComponentBinding(name, target);

            lock (bindings)
            {

                // bug 6348
                int slot = bindings.Count;
                bindings.Add(cb);
                invocation.BindingSlot = slot;
            }
        }

        public virtual void SetBody(Expression body)
        {
            this.body = body;
            if (body != null)
            {
                body.ParentExpression = null;
            }
        }

        public Expression GetBody()
        {
            return body;
        }

        public Expression GetChildExpression()
        {
            return GetBody();
        }

        public virtual void SetStackFrameMap(SlotManager map)
        {
            stackFrameMap = map;
        }

        public virtual SlotManager GetStackFrameMap()
        {
            return stackFrameMap;
        }

        public virtual void SetLineNumber(int lineNumber)
        {
            this.lineNumber = lineNumber;
        }

        public virtual void SetColumnNumber(int col)
        {
            this.columnNumber = col;
        }

        public virtual void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public virtual ILocation GetLocation()
        {
            return this;
        }

        public virtual int GetLineNumber()
        {
            return lineNumber;
        }

        public virtual string GetSystemId()
        {
            return systemId;
        }

        public virtual int GetColumnNumber()
        {
            return columnNumber;
        }

        public virtual string GetPublicId()
        {
            return null;
        }

        public virtual ILocation SaveLocation()
        {
            return this;
        }

        public virtual void SetRetainedStaticContext(RetainedStaticContext rsc)
        {
            this.retainedStaticContext = rsc;
        }

        public virtual RetainedStaticContext GetRetainedStaticContext()
        {
            return retainedStaticContext;
        }

        public virtual object GetProperty(string name)
        {
            return null;
        }

        public abstract void Export(ExpressionPresenter presenter);
        public virtual bool IsExportable()
        {
            return true;
        }

        public virtual void SetChildExpression(Expression expr)
        {
            SetBody(expr);
        }

        protected virtual ITailCall Process(Outputter @out, IXPathContext context)
        {
            lock (syncLock)
            {
                if (bodyEvaluator == null)
                {
                    bodyEvaluator = body.MakeElaborator().ElaborateForPush();
                }
            }

            return bodyEvaluator.ProcessLeavingTail(@out, context);
        }
    }
}
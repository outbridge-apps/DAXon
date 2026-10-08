////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class UserFunctionReference : Expression, IComponentInvocation, IUserFunctionResolvable, ICallable
    {
        private readonly SymbolicName.F functionName;
        private UserFunction nominalTarget;
        private int bindingSlot = -1;
        private int optimizeCounter;
        private int typeCheckCounter;

        public int BindingSlot
        {
            get => bindingSlot; set
            {
                bindingSlot = value;
            }
        }

        public Component FixedTarget => nominalTarget.DeclaringComponent;

        public UserFunction NominalTarget => nominalTarget;

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "UserFunctionReference";
        public UserFunctionReference(UserFunction target)
        {
            this.nominalTarget = target;
            this.functionName = (SymbolicName.F)(target.GetSymbolicName());
        }

        public UserFunctionReference(UserFunction target, SymbolicName.F name)
        {

            // The name of the reference might be a reduced-arity version of the function name, if it has optional params
            this.nominalTarget = target;
            this.functionName = name;
        }

        public void SetFunction(UserFunction function)
        {
            if (!function.GetSymbolicName().ComponentName.Equals(functionName.ComponentName))
            {
                throw new ArgumentException("Function name does not match");
            }

            if (function.GetMinimumArity() > functionName.GetArity() || function.GetArity() < functionName.GetArity())
            {
                throw new ArgumentException("Function arity does not match");
            }

            this.nominalTarget = function;
        }

        public override Expression Simplify()
        {

            // if this is an inline function, simplify the body of that function now
            if (nominalTarget.GetFunctionName().HasURI(NamespaceUri.ANONYMOUS) && typeCheckCounter == 0)
            {

                // Prevent recursive simplification
                typeCheckCounter++;
                nominalTarget.SetBody(nominalTarget.GetBody().Simplify());
                typeCheckCounter--;
            }

            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {

            // if this is an inline function, typecheck that function now
            if (nominalTarget.GetFunctionName().HasURI(NamespaceUri.ANONYMOUS) && typeCheckCounter == 0)
            {

                // Prevent recursive type-checking: test case -s:misc-HigherOrderFunctions -t:xqhof2
                typeCheckCounter++;
                nominalTarget.TypeCheck(visitor);
                typeCheckCounter--;
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {

            // if this is an inline function, optimize that function now
            if (nominalTarget.GetFunctionName().HasURI(NamespaceUri.ANONYMOUS) && optimizeCounter == 0)
            {

                // Prevent recursive optimization: test case -s:misc-HigherOrderFunctions -t:xqhof2 ; and bug #5054
                optimizeCounter++;
                Expression o;
                o = nominalTarget.GetBody().Optimize(visitor, ContextItemStaticInfo.ABSENT);
                nominalTarget.SetBody(o);
                SlotManager slotManager = visitor.GetConfiguration().MakeSlotManager();
                for (int i = 0; i < GetArity(); i++)
                {
                    UserFunctionParameter param = nominalTarget.GetParameterDefinitions()[i];
                    slotManager.AllocateSlotNumber(param.GetVariableQName(), param);
                }

                ExpressionTool.AllocateSlots(o, GetArity(), slotManager);
                nominalTarget.SetStackFrameMap(slotManager);
                optimizeCounter--;
            }

            return this;
        }

        public SymbolicName GetSymbolicName()
        {
            return functionName;
        }

        public StructuredQName GetFunctionName()
        {
            return nominalTarget.GetFunctionName();
        }

        public int GetArity()
        {
            return functionName.GetArity();
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override ItemType GetItemType()
        {
            return nominalTarget.FunctionItemType;
        }

        protected override int ComputeSpecialProperties()
        {
            return StaticProperty.COMPUTED_FUNCTION;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.FUNCTION;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            UserFunctionReference @ref = new UserFunctionReference(nominalTarget, functionName);
            @ref.optimizeCounter = optimizeCounter;
            @ref.typeCheckCounter = typeCheckCounter;
            return @ref;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return (IFunctionItem)MakeElaborator().ElaborateForItem().Eval(context);
        }

        public IFunctionItem Call(IXPathContext context)
        {
            return (IFunctionItem)EvaluateItem(context);
        }

        public override string ToString()
        {
            return GetFunctionName().EQName + "#" + GetArity();
        }

        public override string ToShortString()
        {
            return GetFunctionName().DisplayName + "#" + GetArity();
        }

        public override void Export(ExpressionPresenter @out)
        {
            ExpressionPresenter.ExportOptions options = @out.GetOptions();
            if (nominalTarget.DeclaringComponent == null)
            {

                // This happens for an inline function declared within a static expression, e.g. one
                // that is bound to a static global variable. There is no separate component registered for
                // such a function, so we expand it inline
                @out.StartElement("inlineFn");
                nominalTarget.Export(@out);
                @out.EndElement();
            }
            else
            {
                StylesheetPackage rootPackage = options.rootPackage;
                StylesheetPackage containingPackage = nominalTarget.DeclaringComponent.ContainingPackage;
                if (rootPackage != null && !(rootPackage == containingPackage || rootPackage.Contains(containingPackage)))
                {
                    throw new XPathException("Cannot export a package containing a reference to a user-defined function (" + ToShortString() + ") that is not present in the package being exported");
                }

                @out.StartElement("ufRef");
                @out.EmitAttribute("name", nominalTarget.GetFunctionName());
                @out.EmitAttribute("arity", nominalTarget.GetArity() + "");
                @out.EmitAttribute("bSlot", "" + BindingSlot);
                @out.EndElement();
            }
        }

        public override Elaborator GetElaborator()
        {
            return new UserFunctionReferenceElaborator();
        }
        ISequence ICallable.Call(IXPathContext arg0, ISequence[] arg1) => Call(arg0);

        private sealed class UserFunctionReferenceElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                UserFunctionReference expr = (UserFunctionReference)GetExpression();
                if (expr.bindingSlot == -1)
                {
                    return (context) => new BoundUserFunction(expr.nominalTarget, expr.GetArity(), expr.nominalTarget.DeclaringComponent, expr, context.GetController());
                }
                else
                {
                    return (context) =>
                    {
                        Component targetComponent = context.GetTargetComponent(expr.bindingSlot);
                        return new BoundUserFunction((UserFunction)targetComponent.GetActor(), expr.GetArity(), targetComponent, expr, context.GetController());
                    };
                }
            }
        }

        internal sealed class BoundUserFunction : AbstractFunction, IContextOriginator
        {
            private readonly IExportAgent agent;
            private readonly IFunctionItem function;
            private readonly int arity;
            private readonly Component component;
            private readonly Controller controller; // retained in case a function is returned from a query or stylesheet

            public IFunctionItem TargetFunction => function;

            internal Component BoundComponent => component;

            public override IFunctionItemType FunctionItemType
            {
                get
                {
                    if (function is UserFunction)
                    {
                        return ((UserFunction)function).GetFunctionItemType(GetArity());
                    }

                    return function.FunctionItemType;
                }
            }

            public override string Description => function.Description;
            public BoundUserFunction(IFunctionItem function, int arity, Component component, IExportAgent agent, Controller controller)
            {
                this.agent = agent;
                this.function = function;
                this.arity = arity;
                this.component = component;
                this.controller = controller;
            }

            public override IXPathContext MakeNewContext(IXPathContext oldContext, IContextOriginator originator)
            {
                if (controller.GetConfiguration() != oldContext.GetConfiguration())
                {
                    throw new InvalidOperationException("A function created under one Configuration cannot be called under a different Configuration");
                }

                XPathContextMajor c2;
                c2 = controller.NewXPathContext();
                c2.TemporaryOutputState = StandardNames.XSL_FUNCTION;
                c2.CurrentOutputUri = null;
                c2.SetCurrentComponent(component);
                c2.SetResourceResolver(oldContext.GetResourceResolver());
                c2.Origin = originator;
                c2.SetCaller(oldContext);
                return function.MakeNewContext(c2, originator);
            }

            public override ISequence Call(IXPathContext context, ISequence[] args)
            {
                IXPathContext c2 = function.MakeNewContext(context, this);
                if (c2 is XPathContextMajor && component != null)
                {
                    ((XPathContextMajor)c2).SetCurrentComponent(component);
                }

                if (function.GetArity() > args.Length)
                {
                    IFunctionDefinition fd = (IFunctionDefinition)function;
                    ISequence[] extendedArgs = ArrayTools.CopyOf(args, fd.NumberOfParameters);
                    for (int i = args.Length; i < extendedArgs.Length; i++)
                    {
                        extendedArgs[i] = fd.GetDefaultValueExpression(i).Copy(new RebindingMap()).MakeElaborator().Lazily(true, false).Evaluate(context);
                    }

                    args = extendedArgs;
                }

                return function.Call(c2, args);
            }

            public override AnnotationList GetAnnotations()
            {
                return function.GetAnnotations();
            }

            public override StructuredQName GetFunctionName()
            {
                return function.GetFunctionName();
            }

            public override int GetArity()
            {
                return arity;
            }

            public override void Export(ExpressionPresenter @out)
            {
                agent.Export(@out);
            }
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// A compiled global variable in a stylesheet or query. <br>
    /// </summary>
    public class GlobalVariable : Actor, IBinding, IDeclaration, ITraceableComponent, IContextOriginator
    {
        protected IList<IBindingReference> references = new List<IBindingReference>(10);
        private StructuredQName variableQName;
        private Values.SequenceType requiredType;
        private bool _indexed;
        private bool _isPrivate = false;
        private bool _isAssignable = false;
        private GlobalVariable originalVariable;
        private int binderySlotNumber;
        private bool _isRequiredParam;
        private bool _isStatic;

        public override string TracingTag => "xsl:variable";

        public virtual GlobalVariable OriginalVariable
        {
            get => originalVariable; set
            {
                originalVariable = value;
            }
        }

        public virtual GlobalVariable UltimateOriginalVariable
        {
            get
            {
                if (originalVariable == null)
                {
                    return this;
                }
                else
                {
                    return originalVariable.UltimateOriginalVariable;
                }
            }
        }

        public virtual int BinderySlotNumber
        {
            get => binderySlotNumber; set
            {
                if (!IsUnused())
                {
                    binderySlotNumber = value;
                }
            }
        }

        public IntegerValue[] IntegerBoundsForVariable => GetBody() == null ? null : GetBody().IntegerBounds;

        public virtual int LocalSlotNumber => 0;

        public virtual string Description
        {
            get
            {
                if (variableQName.HasURI(NamespaceUri.SAXON_GENERATED_VARIABLE))
                {
                    return "optimizer-generated global variable select=\"" + GetBody().ToShortString() + '"';
                }
                else
                {
                    return "global variable " + GetVariableQName().DisplayName;
                }
            }
        }

        protected virtual string Flags
        {
            get
            {
                string flags = "";
                if (_isAssignable)
                {
                    flags += "a";
                }

                if (_indexed)
                {
                    flags += "x";
                }

                if (_isRequiredParam)
                {
                    flags += "r";
                }

                if (_isStatic)
                {
                    flags += "s";
                }

                return flags;
            }
        }
        public GlobalVariable()
        {
        }

        public virtual void Init(Expression select, StructuredQName qName)
        {
            variableQName = qName;
            SetBody(select);
        }

        public override SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_VARIABLE, variableQName);
        }

        public void GatherProperties(Action<string, object> consumer)
        {
            consumer("name",GetVariableQName());
        }

        public virtual void SetStatic(bool declaredStatic)
        {
            _isStatic = declaredStatic;
        }

        public virtual bool IsStatic()
        {
            return this._isStatic;
        }

        public virtual void SetRequiredType(Values.SequenceType required)
        {
            requiredType = required;
        }

        public Values.SequenceType GetRequiredType()
        {
            return requiredType;
        }

        private Configuration GetConfiguration()
        {
            return GetPackageData().GetConfiguration();
        }

        public virtual void SetUnused(bool unused)
        {
            this.binderySlotNumber = -9234;
        }

        public virtual bool IsUnused()
        {
            return this.binderySlotNumber == -9234;
        }

        public virtual bool IsPrivate()
        {
            return _isPrivate;
        }

        public virtual void SetPrivate(bool b)
        {
            _isPrivate = b;
        }

        public virtual void SetAssignable(bool assignable)
        {
            _isAssignable = assignable;
        }

        public bool IsAssignable()
        {
            return _isAssignable;
        }

        public StructuredQName GetObjectName()
        {
            return GetVariableQName();
        }

        public override object GetProperty(string name)
        {
            return null;
        }

        public virtual HostLanguage GetHostLanguage()
        {
            return GetPackageData().GetHostLanguage();
        }

        public virtual void SetIndexedVariable()
        {
            _indexed = true;
        }

        public virtual bool IsIndexedVariable()
        {
            return _indexed;
        }

        public virtual void SetContainsLocals(SlotManager map)
        {
            SetStackFrameMap(map);
        }

        public bool IsGlobal()
        {
            return true;
        }

        public virtual void RegisterReference(IBindingReference @ref)
        {
            references.Add(@ref);
        }

        public virtual IEnumerator<IBindingReference> IterateReferences()
        {
            return references.GetEnumerator();
        }

        public virtual int CountReferences()
        {
            return references.Count;
        }

        public virtual void SetRequiredParam(bool requiredParam)
        {
            this._isRequiredParam = requiredParam;
        }

        public virtual bool IsRequiredParam()
        {
            return this._isRequiredParam;
        }

        public virtual void Compile(Executable exec, int slot)
        {
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            BinderySlotNumber = slot;
            if (this is GlobalParam)
            {
                SetRequiredParam(GetBody() == null);
            }

            Values.SequenceType type = GetRequiredType();
            foreach (IBindingReference @ref in references)
            {
                @ref.Fixup(this);
                IGroundedValue constantValue = null;
                int properties = 0;
                Expression select = GetBody();
                if (select is Literal && !(this is GlobalParam))
                {

                    // we can't rely on the constant value because it hasn't yet been type-checked,
                    // which could change it (eg by numeric promotion). Rather than attempt all the type-checking
                    // now, we do a quick check. See test bug64
                    Affinity relation = th.Relationship(select.GetItemType(), type.PrimaryType);
                    if (relation == Affinity.SAME_TYPE || relation == Affinity.SUBSUMED_BY)
                    {
                        constantValue = ((Literal)select).GroundedValue;
                        type = Values.SequenceType.MakeSequenceType(SequenceTool.GetItemType(constantValue, th), SequenceTool.GetCardinality(constantValue));
                    }
                }

                if (select != null)
                {
                    properties = select.GetSpecialProperties();
                }

                properties |= StaticProperty.NO_NODES_NEWLY_CREATED;

                // a variable reference is non-creative even if its initializer is creative
                @ref.SetStaticType(type, constantValue, properties);
            }


            if (IsRequiredParam())
            {
                exec.RegisterGlobalParameter((GlobalParam)this);
            }
        }

        public virtual void TypeCheck(ExpressionVisitor visitor)
        {
            Expression value = GetBody();
            if (value != null)
            {
                value.CheckForUpdatingSubexpressions();
                if (value.IsUpdatingExpression())
                {
                    throw new XPathException("Initializing expression for global variable must not be an updating expression", "XUST0001");
                }

                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, GetVariableQName().DisplayName, 0);
                ContextItemStaticInfo cit = GetConfiguration().MakeContextItemStaticInfo(AnyItemType.GetInstance(), true);
                Expression value2 = TypeChecker.StrictTypeCheck(value.Simplify().TypeCheck(visitor, cit), GetRequiredType(), role, visitor.StaticContext);
                value2 = value2.Optimize(visitor, cit);
                SetBody(value2);

                // the value expression may declare local variables
                SlotManager map = GetConfiguration().MakeSlotManager();
                int slots = ExpressionTool.AllocateSlots(value2, 0, map);
                if (slots > 0)
                {
                    SetContainsLocals(map);
                }

                if (GetRequiredType() == Values.SequenceType.ANY_SEQUENCE && !(this is GlobalParam))
                {

                    // no type was declared; try to deduce a type from the value. Use the OPTIMIZED body
                    // (value2), not the raw parse (value): nodeset-shape special properties (ORDERED/PEER/
                    // SINGLE_DOCUMENT) are only computed after Simplify+TypeCheck+Optimize, so reading them
                    // from the un-optimized `value` returned 0 — which left every `$var/child` under an
                    // order-insensitive consumer carrying a redundant DocumentSorter that Java-HE elides.
                    try
                    {
                        Types.ItemType itemType = value2.GetItemType();
                        int cardinality = value2.GetCardinality();
                        SetRequiredType(Values.SequenceType.MakeSequenceType(itemType, cardinality));
                        IGroundedValue constantValue = null;
                        if (value2 is Literal)
                        {
                            constantValue = ((Literal)value2).GroundedValue;
                        }

                        foreach (IBindingReference reference in references)
                        {
                            if (reference is VariableReference)
                            {
                                ((VariableReference)reference).RefineVariableType(itemType, cardinality, constantValue, value2.GetSpecialProperties());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                    }
                }
            }
        }

        public virtual void LookForCycles(IndexedStack<object> referees, XQueryFunctionLibrary globalFunctionLibrary)
        {
            if (referees.Contains(this))
            {
                int s = referees.IndexOf(this);
                referees.IPush(this);
                StringBuilder messageBuilder = new StringBuilder("Circular definition of global variable: $" + GetVariableQName().DisplayName);
                for (int i = s; i < referees.Count - 1; i++)
                {
                    if (i != s)
                    {
                        messageBuilder.Append(", which");
                    }

                    if (referees[i + 1] is GlobalVariable)
                    {
                        GlobalVariable next = (GlobalVariable)referees[i + 1];
                        messageBuilder.Append(" uses $").Append(next.GetVariableQName().DisplayName);
                    }
                    else if (referees[i + 1] is XQueryFunction)
                    {
                        XQueryFunction next = (XQueryFunction)referees[i + 1];
                        messageBuilder.Append(" calls ").Append(next.GetFunctionName().DisplayName).Append('#').Append(next.NumberOfParameters).Append("()");
                    }
                }

                string message = messageBuilder.ToString();
                message += '.';
                string errorCode;
                if (GetPackageData().IsXSLT())
                {
                    errorCode = "XTDE0640";
                }
                else if (s == 0 && referees.Count == 2)
                {

                    // Simple self-reference, treated specially in XQuery
                    errorCode = "XPST0008";
                }
                else
                {
                    errorCode = "XQDY0054";
                }

                throw new XPathException(message, errorCode).AsStaticError().WithLocation(GetLocation());
            }

            Expression select = GetBody();
            if (select != null)
            {
                referees.IPush(this);
                IList<IBinding> list = new List<IBinding>(10);
                ExpressionTool.GatherReferencedVariables(select, list);
                foreach (IBinding b in list)
                {
                    if (b is GlobalVariable)
                    {
                        ((GlobalVariable)b).LookForCycles(referees, globalFunctionLibrary);
                    }
                }

                IList<SymbolicName> flist = new List<SymbolicName>();
                ExpressionTool.GatherCalledFunctionNames(select, flist);
                foreach (SymbolicName s in flist)
                {
                    XQueryFunction f = globalFunctionLibrary.GetDeclarationByKey(s);
                    if (!referees.Contains(f))
                    {

                        // recursive function calls are allowed
                        LookForFunctionCycles(f, referees, globalFunctionLibrary);
                    }
                }

                referees.Pop();
            }
        }

        private static void LookForFunctionCycles(XQueryFunction f, IndexedStack<object> referees, XQueryFunctionLibrary globalFunctionLibrary)
        {
            ExpressionTool.ProbeTreeDepth();
            Expression body = f.Body;
            referees.IPush(f);
            IList<IBinding> list = new List<IBinding>(10);
            ExpressionTool.GatherReferencedVariables(body, list);
            foreach (IBinding b in list)
            {
                if (b is GlobalVariable)
                {
                    ((GlobalVariable)b).LookForCycles(referees, globalFunctionLibrary);
                }
            }

            IList<SymbolicName> flist = new List<SymbolicName>();
            ExpressionTool.GatherCalledFunctionNames(body, flist);
            foreach (SymbolicName s in flist)
            {
                XQueryFunction qf = globalFunctionLibrary.GetDeclarationByKey(s);
                if (!referees.Contains(qf))
                {

                    // recursive function calls are allowed
                    LookForFunctionCycles(qf, referees, globalFunctionLibrary);
                }
            }

            referees.Pop();
        }

        public virtual IGroundedValue GetSelectValue(IXPathContext context, Component target)
        {
            Expression select = GetBody();
            if (select == null)
            {
                throw new InvalidOperationException("*** No select expression for global variable $" + GetVariableQName().DisplayName + "!!");
            }
            else if (select is Literal)
            {

                // fast path for constant global variables
                return ((Literal)select).GroundedValue;
            }
            else
            {
                try
                {
                    Controller controller = context.GetController();
                    Executable exec = controller.GetExecutable();
                    bool hasAccessToGlobalContext = true;
                    if (exec is PreparedStylesheet)
                    {
                        hasAccessToGlobalContext = target == null || target.DeclaringPackage == ((PreparedStylesheet)exec).GetTopLevelPackage();
                    }

                    XPathContextMajor c2 = context.NewCleanContext();
                    c2.Origin = this;
                    if (hasAccessToGlobalContext)
                    {
                        ManualIterator mi = new ManualIterator(context.GetController().GlobalContextItem);
                        c2.SetCurrentIterator(mi);
                    }
                    else
                    {
                        c2.SetCurrentIterator(null);
                    }

                    if (GetStackFrameMap() != null)
                    {
                        c2.OpenStackFrame(GetStackFrameMap());
                    }

                    c2.SetCurrentComponent(target);
                    int savedOutputState = c2.TemporaryOutputState;
                    c2.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                    c2.CurrentOutputUri = null;
                    IGroundedValue result;
                    if (_indexed)
                    {
                        result = c2.GetConfiguration().MakeSequenceExtent(select, FilterExpression.FILTERED, c2);
                    }
                    else
                    {
                        result = ExpressionTool.EagerEvaluate(select, c2);
                    }

                    c2.TemporaryOutputState = savedOutputState;
                    return result;
                }
                catch (UncheckedXPathException unxe)
                {
                    XPathException xe = unxe.GetXPathException();
                    if (!GetVariableQName().HasURI(NamespaceUri.SAXON_GENERATED_VARIABLE))
                    {
                        xe.SetIsGlobalError(true);
                    }

                    throw xe;
                }
                catch (XPathException e)
                {
                    if (!GetVariableQName().HasURI(NamespaceUri.SAXON_GENERATED_VARIABLE))
                    {
                        e.SetIsGlobalError(true);
                    }

                    throw;
                }
            }
        }

        public virtual IGroundedValue EvaluateVariable(IXPathContext context)
        {
            Controller controller = context.GetController();
            Bindery b = controller.GetBindery(GetPackageData());
            IGroundedValue v = b.GetGlobalVariable(BinderySlotNumber);
            if (v != null)
            {
                return v;
            }
            else
            {
                Component target = context.GetCurrentComponent(); // Bug #6236
                if (target == null)
                {
                    target = DeclaringComponent;
                }

                return ActuallyEvaluate(context, target);
            }
        }

        public virtual IGroundedValue EvaluateVariable(IXPathContext context, Component target)
        {
            Controller controller = context.GetController();
            Bindery b = controller.GetBindery(GetPackageData());
            if (b == null)
            {

                // This is to handle those paths that haven't properly adjusted to multiple binderies...
                throw new InvalidOperationException(); //b = controller.getTopLevelBindery();
            }

            IGroundedValue v = b.GetGlobalVariable(BinderySlotNumber);
            if (v != null)
            {
                if (v is Bindery.FailureValue)
                {
                    throw ((Bindery.FailureValue)v).GetObject();
                }

                return v;
            }
            else
            {
                return ActuallyEvaluate(context, target);
            }
        }

        protected virtual IGroundedValue ActuallyEvaluate(IXPathContext context, Component target)
        {
            Controller controller = context.GetController();
            Bindery b = controller.GetBindery(GetPackageData());
            try
            {

                // This is the first reference to a global variable; try to evaluate it now.
                // But first check for circular dependencies.
                CheckCircularity(this, context);
                int slot = BinderySlotNumber;
                IGroundedValue value = GetSelectValue(context, target);
                if (_indexed)
                {
                    value = controller.GetConfiguration().ObtainOptimizer().MakeIndexedValue(value.Iterate());
                }

                lock (b.syncLock)
                {

                    // This lock doesn't prevent two different threads evaluating the value in parallel. It does
                    // ensure that when this happens, all threads end up using the same value for the variable.
                    IGroundedValue temp = b.GetGlobalVariable(slot);
                    if (temp == null)
                    {

                        // check once again, things might have changed
                        b.SetGlobalVariableValue(slot, value);
                    }
                    else
                    {

                        // Discard the value we have calculated, and use the value computed in another thread
                        value = temp;
                    }
                }

                return value;
            }
            catch (XPathException err)
            {
                if (err is XPathException.Circularity)
                {
                    err.SetErrorCode(GetPackageData().IsXSLT() ? "XTDE0640" : "XQDY0054");
                    err.XPathContext = context;
                    err.SetIsGlobalError(true);

                    // Detect it more quickly the next time (in a pattern, the error is recoverable)
                    b.SetGlobalVariable(this, new FailureValue(err));
                    err.SetLocation(GetLocation());
                    throw;
                }
                else
                {
                    throw;
                }
            }
            catch (OutSmart.DAXon.Types.Circularity circ)
            {
                // Controller.RegisterGlobalVariableDependency throws the port's Types.Circularity — a plain
                // Exception, NOT the XPathException.Circularity the catch above expects — so a circular global
                // variable escaped uncaught as a bare "ERR". Convert it to the proper dynamic error
                // (XQDY0054 in XQuery, XTDE0640 in XSLT), matching the XPathException.Circularity branch
                // (K2-InternalVariablesWithout-1c).
                XPathException err = new XPathException(circ.Message, GetPackageData().IsXSLT() ? "XTDE0640" : "XQDY0054");
                err.XPathContext = context;
                err.SetIsGlobalError(true);
                b.SetGlobalVariable(this, new FailureValue(err));
                err.SetLocation(GetLocation());
                throw err;
            }
        }

        protected static void CheckCircularity(GlobalVariable var, IXPathContext context)
        {
            Controller controller = context.GetController();
            if (!(context is XPathContextMajor))
            {
                context = GetMajorCaller(context);
            }

            while (context != null)
            {
                do
                {
                    IContextOriginator origin = ((XPathContextMajor)context).Origin;
                    if (origin is GlobalVariable)
                    {
                        controller.RegisterGlobalVariableDependency((GlobalVariable)origin, var);
                        return;
                    }

                    context = GetMajorCaller(context);
                }
                while (context != null);
            }
        }

        private static XPathContextMajor GetMajorCaller(IXPathContext context)
        {
            IXPathContext caller = context.GetCaller();
            while (!(caller == null || caller is XPathContextMajor))
            {
                caller = caller.GetCaller();
            }

            return (XPathContextMajor)caller;
        }

        public virtual void SetVariableQName(StructuredQName s)
        {
            variableQName = s;
        }

        public StructuredQName GetVariableQName()
        {
            return variableQName;
        }

        public void AddReference(VariableReference @ref, bool isLoopingReference)
        {
        }

        public override void Export(ExpressionPresenter presenter)
        {
            bool asParam = this is GlobalParam && !IsStatic(); // bug 4035: export static params as variables
            presenter.StartElement(asParam ? "globalParam" : "globalVariable");
            presenter.EmitAttribute("name", GetVariableQName());
            presenter.EmitAttribute("as", GetRequiredType().ToAlphaCode());
            presenter.EmitAttribute("line", GetLineNumber() + "");
            presenter.EmitAttribute("module", GetSystemId());
            if (GetStackFrameMap() != null)
            {
                presenter.EmitAttribute("slots", GetStackFrameMap().NumberOfVariables + "");
            }

            if (DeclaringComponent != null)
            {
                Visibility vis = DeclaringComponent.GetVisibility();
                if (vis != Visibility.UNDEFINED)
                {
                    presenter.EmitAttribute("visibility", vis.ToString());
                }
            }

            string flags = Flags;
            if (!(flags.Length == 0))
            {
                presenter.EmitAttribute("flags", flags);
            }

            if (GetBody() != null)
            {
                GetBody().Export(presenter);
            }

            presenter.EndElement();
        }
        Values.SequenceType IBinding.GetRequiredType() => GetRequiredType();
        ISequence IBinding.EvaluateVariable(IXPathContext arg0) => EvaluateVariable(arg0);
    }
}

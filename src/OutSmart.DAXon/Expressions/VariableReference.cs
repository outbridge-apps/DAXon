////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.Streamability;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    public abstract class VariableReference : Expression, IBindingReference
    {
        protected IBinding binding = null; // This will be null until fixup() is called; it will also be null
        protected Values.SequenceType staticType = null;
        protected IGroundedValue constantValue = null;
        private StructuredQName variableName = null;
        private bool flattened = false;
        private bool inLoop = false;
        private bool filtered = false;
        // Nodeset-shape special properties (ORDERED/PEER/SUBTREE/SINGLE_DOCUMENT/…) inferred from the
        // binding's select expression via SetStaticType. Persisted separately from the resettable
        // `staticProperties` cache so ResetLocalStaticProperties() (during optimize) does not drop them;
        // without this, `$v/child` under sum/count/distinct kept a redundant DocumentSorter that Java-HE
        // elides (the binding's ORDERED/PEER property was lost after the first reset). CONTEXT_DOCUMENT is
        // excluded (context at the point of use may differ from the point of definition).
        protected int refinedSpecialProps = 0;
        private bool computingBindingProps = false; // re-entrancy guard for the lazy binding-body property read

        public virtual StructuredQName VariableName
        {
            get => variableName; set
            {
                variableName = value;
            }
        }
        public override int NetCost => 0;

        public override IntegerValue[] IntegerBounds
        {
            get
            {
                if (binding != null)
                {
                    return binding.IntegerBoundsForVariable;
                }
                else
                {
                    return null;
                }
            }
        }

        public override int IntrinsicDependencies
        {
            get
            {
                int d = 0;
                if (binding == null)
                {

                    // assume the worst
                    d |= StaticProperty.DEPENDS_ON_LOCAL_VARIABLES | StaticProperty.DEPENDS_ON_ASSIGNABLE_GLOBALS | StaticProperty.DEPENDS_ON_RUNTIME_ENVIRONMENT;
                }
                else if (binding.IsGlobal())
                {
                    if (binding.IsAssignable())
                    {
                        d |= StaticProperty.DEPENDS_ON_ASSIGNABLE_GLOBALS;
                    }

                    if (binding is GlobalParam)
                    {
                        d |= StaticProperty.DEPENDS_ON_RUNTIME_ENVIRONMENT;
                    }
                }
                else
                {
                    d |= StaticProperty.DEPENDS_ON_LOCAL_VARIABLES;
                }

                return d;
            }
        }

        public override int ImplementationMethod => (Cardinality.AllowsMany(GetCardinality()) ? 0 : EVALUATE_METHOD) | ITERATE_METHOD | PROCESS_METHOD;

        public override Expression ScopingExpression
        {
            get
            {
                if (binding is Expression)
                {
                    if (binding is LocalParam && ((LocalParam)binding).ParentExpression is LocalParamBlock)
                    {
                        LocalParamBlock block = (LocalParamBlock)((LocalParam)binding).ParentExpression;
                        return block.ParentExpression;
                    }
                    else
                    {
                        return (Expression)binding;
                    }
                }

                Expression parent = ParentExpression;
                while (parent != null)
                {
                    if (parent.HasVariableBinding(binding))
                    {
                        return parent;
                    }

                    parent = parent.ParentExpression;
                }

                return null;
            }
        }

        public virtual string DisplayName
        {
            get
            {
                if (binding != null)
                {
                    return binding.GetVariableQName().DisplayName;
                }
                else
                {
                    return variableName.DisplayName;
                }
            }
        }

        public virtual string EQName
        {
            get
            {
                if (binding != null)
                {
                    StructuredQName q = binding.GetVariableQName();
                    if (q.HasURI(NamespaceUri.NULL))
                    {
                        return q.GetLocalPart();
                    }
                    else
                    {
                        return q.EQName;
                    }
                }
                else
                {
                    return variableName.EQName;
                }
            }
        }

        public override string ExpressionName => "varRef";

        public override string StreamerName => "VariableReference";
        public VariableReference(StructuredQName name)
        {
            variableName = name;
        }

        public VariableReference(IBinding binding)
        {

            variableName = binding.GetVariableQName();
            Fixup(binding);
        }

        public abstract override Expression Copy(RebindingMap rebindings);

        protected virtual void CopyFrom(VariableReference @ref)
        {
            binding = @ref.binding;
            staticType = @ref.staticType;
            constantValue = @ref.constantValue;
            variableName = @ref.variableName;
            flattened = @ref.flattened;
            inLoop = @ref.inLoop;
            filtered = @ref.filtered;
            refinedSpecialProps = @ref.refinedSpecialProps;
            ExpressionTool.CopyLocationInfo(@ref, this);
        }

        public virtual void SetStaticType(Values.SequenceType type, IGroundedValue value, int properties)
        {

            if (type == null)
            {
                type = Values.SequenceType.ANY_SEQUENCE;
            }

            staticType = type;
            constantValue = value;

            // Although the variable may be a context document node-set at the point it is defined,
            // the context at the point of use may be different, so this property cannot be transferred.
            int dependencies = Dependencies;
            staticProperties = (properties & ~StaticProperty.CONTEXT_DOCUMENT_NODESET & ~StaticProperty.ALL_NODES_NEWLY_CREATED) | StaticProperty.NO_NODES_NEWLY_CREATED | type.GetCardinality() | dependencies;
            // Persist the nodeset-shape bits so they survive ResetLocalStaticProperties() (see field comment).
            refinedSpecialProps = staticProperties & StaticProperty.NODESET_PROPERTIES;
        }

        public override void SetFlattened(bool flattened)
        {
            this.flattened = flattened;
        }

        public virtual bool IsFlattened()
        {
            return flattened;
        }

        public override void SetFiltered(bool filtered)
        {
            this.filtered = filtered;
        }

        public virtual bool IsFiltered()
        {
            return filtered;
        }

        public virtual bool IsInLoop()
        {
            return inLoop;
        }

        public virtual void SetInLoop(bool inLoop)
        {
            this.inLoop = inLoop;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            if (constantValue != null)
            {
                binding = null;
                return Literal.MakeLiteral(constantValue, this);
            }


            //  following code removed because it causes error181 to blow the stack - need to check for circularities well
            if (binding != null)
            {
                RecomputeInLoop();
                binding.AddReference(this, inLoop);
            }

            return this;
        }

        public virtual void RecomputeInLoop()
        {
            inLoop = ExpressionTool.IsLoopingReference(this, binding);
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            if (binding is LetExpression && ((LetExpression)binding).Sequence is Literal && !((LetExpression)binding).indexedVariable)
            {
                Expression val = ((LetExpression)binding).Sequence;
                Optimizer.Trace(visitor.GetConfiguration(), "Replaced variable " + DisplayName + " by its value", val);
                binding = null;
                return val.Copy(new RebindingMap());
            }

            if (constantValue != null)
            {
                binding = null;
                Expression result = Literal.MakeLiteral(constantValue, this);
                ExpressionTool.CopyLocationInfo(this, result);
                Optimizer.Trace(visitor.GetConfiguration(), "Replaced variable " + DisplayName + " by its value", result);
                return result;
            }

            if (binding is GlobalParam && ((GlobalParam)binding).IsStatic())
            {
                Expression select = ((GlobalParam)binding).GetBody();
                if (select is Literal)
                {
                    binding = null;
                    Optimizer.Trace(visitor.GetConfiguration(), "Replaced static parameter " + DisplayName + " by its value", select);
                    return select.Copy(new RebindingMap());
                }
            }

            return this;
        }

        public void Fixup(IBinding newBinding)
        {
            bool indexed = binding is ILocalBinding && ((ILocalBinding)binding).IsIndexedVariable();
            this.binding = newBinding;
            if (indexed && newBinding is ILocalBinding)
            {
                ((ILocalBinding)newBinding).SetIndexedVariable();
            }

            ResetLocalStaticProperties();
        }

        public virtual void RefineVariableType(Types.ItemType type, int cardinality, IGroundedValue constantValue, int properties)
        {
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            Types.ItemType oldItemType = GetItemType();
            Types.ItemType newItemType = oldItemType;
            if (th.IsSubType(type, oldItemType))
            {
                newItemType = type;
            }

            if (oldItemType is NodeTest && type is IAtomicType)
            {

                // happens when all references are flattened
                newItemType = type;
            }

            int newcard = cardinality & GetCardinality();
            if (newcard == 0)
            {

                // this will probably lead to a type error later
                newcard = GetCardinality();
            }

            Values.SequenceType seqType = Values.SequenceType.MakeSequenceType(newItemType, newcard);
            SetStaticType(seqType, constantValue, properties);
        }

        public override Types.ItemType GetItemType()
        {
            if (staticType == null || staticType.PrimaryType == AnyItemType.GetInstance())
            {
                if (binding != null)
                {
                    Values.SequenceType st = binding.GetRequiredType();
                    if (st != null)
                    {
                        return st.PrimaryType;
                    }
                }

                return AnyItemType.GetInstance();
            }
            else
            {
                return staticType.PrimaryType;
            }
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            if (binding != null)
            {
                if (binding.IsGlobal() || binding is LocalParam || (binding is LetExpression && ((LetExpression)binding).IsInstruction()) || binding is LocalVariableBinding)
                {
                    Values.SequenceType st = binding.GetRequiredType();
                    if (st != null)
                    {
                        return st.PrimaryType.GetUType();
                    }
                    else
                    {
                        return UType.ANY;
                    }
                }
                else if (binding is Assignation)
                {
                    return ((Assignation)binding).Sequence.GetStaticUType(contextItemType);
                }
            }

            return UType.ANY;
        }

        protected override int ComputeCardinality()
        {
            if (staticType == null)
            {
                if (binding == null)
                {
                    return StaticProperty.ALLOWS_ZERO_OR_MORE;
                }
                else if (binding is LetExpression)
                {
                    return binding.GetRequiredType().GetCardinality();
                }
                else if (binding is Assignation)
                {
                    return StaticProperty.EXACTLY_ONE;
                }
                else if (binding.GetRequiredType() == null)
                {
                    return StaticProperty.ALLOWS_ZERO_OR_MORE;
                }
                else
                {
                    return binding.GetRequiredType().GetCardinality();
                }
            }
            else
            {
                return staticType.GetCardinality();
            }
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            if (binding == null || !binding.IsAssignable())
            {

                // if the variable reference is assignable, we mustn't move it, or any expression that contains it,
                // out of a loop. The way to achieve this is to treat it as a "creative" expression, because the
                // optimizer recognizes such expressions and handles them with care...
                p |= StaticProperty.NO_NODES_NEWLY_CREATED;
            }

            if (binding is Assignation)
            {
                Expression exp = ((Assignation)binding).Sequence;
                if (exp != null)
                {
                    p |= exp.GetSpecialProperties() & StaticProperty.NOT_UNTYPED_ATOMIC;
                }
            }

            if (staticType != null && !Cardinality.AllowsMany(staticType.GetCardinality()) && staticType.PrimaryType is NodeTest)
            {
                p |= StaticProperty.SINGLE_DOCUMENT_NODESET;
            }

            // Re-apply the binding-inferred nodeset-shape properties (ORDERED/PEER/…) that SetStaticType
            // captured; otherwise they are lost on the first ResetLocalStaticProperties() and the optimizer
            // can no longer prove `$v/child` is sorted+peer, leaving a redundant DocumentSorter in place.
            p |= refinedSpecialProps;

            // XSLT global variables: the reference is bound at parse time with properties=0 (the select is
            // not yet optimized) and the port's push-time refinement does not always run, so `refinedSpecialProps`
            // can stay 0. Read the binding body's nodeset-shape properties lazily instead — by the time this is
            // consulted (optimize of `$v/child` under sum/count/…), the body IS optimized and carries ORDERED/PEER,
            // letting the redundant DocumentSorter be removed exactly as Java-HE does. CONTEXT_DOCUMENT is excluded
            // (the context at the point of use differs). Guarded against re-entrancy (self/circular bindings).
            if (refinedSpecialProps == 0 && !computingBindingProps && !(binding is null) && !binding.IsAssignable()
                && binding is OutSmart.DAXon.Expressions.Instructions.GlobalVariable gv)
            {
                computingBindingProps = true;
                try
                {
                    Expression body = gv.GetBody();
                    if (body != null && !ReferenceEquals(body, this))
                    {
                        p |= body.GetSpecialProperties() & StaticProperty.NODESET_PROPERTIES & ~StaticProperty.CONTEXT_DOCUMENT_NODESET;
                    }
                }
                finally
                {
                    computingBindingProps = false;
                }
            }

            return p & ~StaticProperty.ALL_NODES_NEWLY_CREATED;
        }

        public override bool SupportsLazyEvaluation()
        {
            return false;
        }

        public override bool Equals(object other)
        {
            return other is VariableReference && binding == ((VariableReference)other).binding && binding != null;
        }

        protected override int ComputeHashCode()
        {
            return binding == null ? 73619830 : binding.GetHashCode();
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            return pathMap.GetPathForVariable(GetBinding());
        }

        public override ISequenceIterator Iterate(IXPathContext c)
        {
            try
            {
                ISequence actual = EvaluateVariable(c);
                return actual.Iterate();
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(GetLocation());
            }
            catch (NullReferenceException err)
            {

                string msg = "Internal error: no value for variable $" + DisplayName + " at line " + GetLocation().GetLineNumber() + (GetLocation().GetSystemId() == null ? "" : " of " + GetLocation().GetSystemId());
                new StandardDiagnostics().LogStackTrace(c, c.GetConfiguration().Logger, 2);
                throw new InvalidOperationException(msg);
            }
            catch (InvalidOperationException err)
            {

                string msg = err.Message + ". Variable reference $" + DisplayName + " at line " + GetLocation().GetLineNumber() + (GetLocation().GetSystemId() == null ? "" : " of " + GetLocation().GetSystemId());
                new StandardDiagnostics().LogStackTrace(c, c.GetConfiguration().Logger, 2);
                throw new InvalidOperationException(msg);
            }
        }

        public override IItem EvaluateItem(IXPathContext c)
        {
            try
            {
                ISequence actual = EvaluateVariable(c);
                return actual.Head();
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(GetLocation()).MaybeWithContext(c);
            }
        }

        public override void Process(Outputter output, IXPathContext c)
        {
            try
            {
                ISequenceIterator iter = EvaluateVariable(c).Iterate();
                ILocation loc = GetLocation();
                SequenceTool.Supply(iter, (item) => output.Append(item, loc, ReceiverOption.ALL_NAMESPACES));
            }
            catch (UncheckedXPathException uxe)
            {
                throw uxe.GetXPathException().MaybeWithLocation(GetLocation()).MaybeWithContext(c);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(GetLocation()).MaybeWithContext(c);
            }
        }

        public virtual ISequence EvaluateVariable(IXPathContext c)
        {
            try
            {
                return binding.EvaluateVariable(c);
            }
            catch (NullReferenceException err)
            {
                if (binding == null)
                {
                    throw new InvalidOperationException("Variable $" + variableName.DisplayName + " has not been fixed up");
                }
                else
                {
                    throw;
                }
            }
        }

        public virtual IBinding GetBinding()
        {
            return binding;
        }

        public override string ToString()
        {
            string d = EQName;
            return "$" + (d == null ? "$" : d);
        }

        public override string ToShortString()
        {
            return "$" + DisplayName;
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("varRef", this);
            destination.EmitAttribute("name", variableName);
            if (binding is ILocalBinding)
            {
                destination.EmitAttribute("slot", "" + ((ILocalBinding)binding).LocalSlotNumber);
            }

            destination.EndElement();
        }
    }
}

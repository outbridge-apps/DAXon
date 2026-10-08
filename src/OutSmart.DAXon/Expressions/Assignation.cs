////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    public abstract class Assignation : Expression, ILocalBinding
    {
        private static readonly OperandRole REPEATED_ACTION_ROLE = new OperandRole(OperandRole.HIGHER_ORDER, OperandUsage.TRANSMISSION);
        private readonly Operand sequenceOp;
        private readonly Operand actionOp;
        protected int slotNumber = -999; // slot number for range variable
        protected StructuredQName variableName;
        protected SequenceType requiredType;
        public bool indexedVariable = false;
        protected bool hasLoopingReference = false;
        protected IList<VariableReference> references = null;

        public virtual Operand SequenceOp => sequenceOp;

        public virtual Operand ActionOp => actionOp;

        public IntegerValue[] IntegerBoundsForVariable => Sequence.IntegerBounds;

        public int LocalSlotNumber => slotNumber;

        public virtual Expression Sequence
        {
            get => sequenceOp.GetChildExpression(); set
            {
                sequenceOp.SetChildExpression(value);
            }
        }

        public virtual int RequiredSlots => 1;

        public override double Cost => Sequence.Cost + 5 * GetAction().Cost;

        public virtual string VariableName
        {
            get
            {
                if (variableName == null)
                {
                    return "zz:var" + ComputeHashCode();
                }
                else
                {
                    return variableName.DisplayName;
                }
            }
        }

        public virtual string VariableEQName
        {
            get
            {
                if (variableName == null)
                {
                    return "Q{http://ns.saxonica.com/anonymous-var}var" + ComputeHashCode();
                }
                else if (variableName.HasURI(NamespaceUri.NULL))
                {
                    return variableName.GetLocalPart();
                }
                else
                {
                    return variableName.EQName;
                }
            }
        }

        public virtual int NominalReferenceCount
        {
            get
            {
                if (indexedVariable)
                {
                    return FilterExpression.FILTERED;
                }
                else if (references == null || hasLoopingReference)
                {
                    return 10;
                }
                else
                {
                    return references.Count;
                }
            }
        }
        public Assignation()
        {
            sequenceOp = new Operand(this, null, OperandRole.NAVIGATE);
            actionOp = new Operand(this, null, this is LetExpression ? OperandRole.SAME_FOCUS_ACTION : REPEATED_ACTION_ROLE);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(sequenceOp, actionOp);
        }

        public virtual void SetRequiredType(SequenceType requiredType)
        {
            this.requiredType = requiredType;
        }

        public virtual void SetVariableQName(StructuredQName variableName)
        {
            this.variableName = variableName;
        }

        public StructuredQName GetVariableQName()
        {
            return variableName;
        }

        public override StructuredQName GetObjectName()
        {
            return variableName;
        }

        public SequenceType GetRequiredType()
        {
            return requiredType;
        }

        public override int ComputeDependencies()
        {
            int d = base.ComputeDependencies() | StaticProperty.DEPENDS_ON_OWN_RANGE_VARIABLES;

            // Unset the DEPENDS_ON_LOCAL_VARIABLES bit if the only dependencies are to
            // variables declared within the expression itself (typically, the variable
            // bound by this Assignation)
            if (!ExpressionTool.ContainsLocalVariableReference(this))
            {
                d &= ~StaticProperty.DEPENDS_ON_LOCAL_VARIABLES;
            }

            return d;
        }

        public ISequence EvaluateVariable(IXPathContext context)
        {
            ISequence actual = context.EvaluateLocalVariable(slotNumber);
            if (!(actual is IGroundedValue))
            {
                actual = actual.Materialize();
                context.SetLocalVariable(slotNumber, actual);
            }

            return actual;
        }

        public virtual void SetAction(Expression action)
        {
            actionOp.SetChildExpression(action);
        }

        public bool IsGlobal()
        {
            return false;
        }

        public bool IsAssignable()
        {
            return false;
        }

        public override void CheckForUpdatingSubexpressions()
        {
            Sequence.CheckForUpdatingSubexpressions();
            if (Sequence.IsUpdatingExpression())
            {
                XPathException err = new XPathException("An updating expression cannot be used to initialize a variable", "XUST0001");
                err.SetLocator(Sequence.GetLocation());
                throw err;
            }

            GetAction().CheckForUpdatingSubexpressions();
        }

        public override bool IsUpdatingExpression()
        {
            return GetAction().IsUpdatingExpression();
        }

        public virtual Expression GetAction()
        {
            return actionOp.GetChildExpression();
        }

        public virtual void SetSlotNumber(int nr)
        {
            slotNumber = nr;
        }

        public override bool HasVariableBinding(IBinding binding)
        {
            return this == binding;
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            SetAction(GetAction().Unordered(retainAllNodes, forStreaming));
            return this;
        }

        public override void SuppressValidation(int validationMode)
        {
            GetAction().SuppressValidation(validationMode);
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet varPath = Sequence.AddToPathMap(pathMap, pathMapNodeSet);
            pathMap.RegisterPathForVariable(this, varPath);
            return GetAction().AddToPathMap(pathMap, pathMapNodeSet);
        }

        public virtual void RefineTypeInformation(ItemType type, int cardinality, IGroundedValue constantValue, int properties, Assignation currentExpression)
        {
            ExpressionTool.ProcessExpressionTree(currentExpression.GetAction(), null, (exp, result) =>
            {
                if (exp is VariableReference && ((VariableReference)exp).GetBinding() == currentExpression)
                {
                    ((VariableReference)exp).RefineVariableType(type, cardinality, constantValue, properties);
                }

                return false;
            });
        }

        public void AddReference(VariableReference @ref, bool isLoopingReference)
        {
            hasLoopingReference |= isLoopingReference;
            if (references == null)
            {
                references = new List<VariableReference>();
            }

            foreach (VariableReference vr in references)
            {
                if (vr == @ref)
                {
                    return;
                }
            }

            references.Add(@ref);
        }

        protected virtual bool RemoveDeadReferences()
        {
            bool inLoop = false;
            if (references != null)
            {
                for (int i = references.Count - 1; i >= 0; i--)
                {

                    // Check whether the reference still has this Assignation as an ancestor in the expression tree
                    bool found = false;
                    inLoop |= references[i].IsInLoop();
                    Expression parent = references[i].ParentExpression;
                    while (parent != null)
                    {
                        if (parent == this)
                        {
                            found = true;
                            break;
                        }
                        else
                        {
                            parent = parent.ParentExpression;
                        }
                    }

                    if (!found)
                    {
                        references.RemoveAt(i);
                    }
                }
            }

            return inLoop;
        }

        protected virtual void VerifyReferences()
        {
            RebuildReferenceList(false);
        }

        public virtual void RebuildReferenceList(bool force)
        {
            int[] results = new int[]
            {
                0,
                force ? int.MaxValue : 500
            };
            IList<VariableReference> references = new List<VariableReference>();
            CountReferences(this, GetAction(), references, results);
            this.references = results[1] <= 0 ? null : references;
        }

        private static void CountReferences(IBinding binding, Expression exp, IList<VariableReference> references, int[] results)
        {
            ExpressionTool.ProbeTreeDepth();

            // results[0] = nominal reference count
            // results[1] = quota nodes visited
            if (exp is LocalVariableReference)
            {
                LocalVariableReference @ref = (LocalVariableReference)exp;
                if (@ref.GetBinding() == binding)
                {
                    @ref.RecomputeInLoop();
                    results[0] += @ref.IsInLoop() ? 10 : 1;
                    references.Add((LocalVariableReference)exp);
                }
            }
            else if ((exp.Dependencies & StaticProperty.DEPENDS_ON_LOCAL_VARIABLES) != 0)
            {
                if (--results[1] <= 0)
                {

                    // abandon the search
                    results[0] = 100;
                    results[1] = 0;
                }
                else
                {
                    foreach (Operand o in exp.Operands())
                    {
                        CountReferences(binding, o.GetChildExpression(), references, results);
                    }
                }
            }
        }

        public bool IsIndexedVariable()
        {
            return indexedVariable;
        }

        public virtual bool ReplaceVariable(Expression seq)
        {
            bool done = ExpressionTool.InlineVariableReferences(GetAction(), this, seq);
            if (done && IsIndexedVariable() && seq is VariableReference)
            {
                IBinding newBinding = ((VariableReference)seq).GetBinding();
                if (newBinding is Assignation)
                {
                    ((Assignation)newBinding).SetIndexedVariable();
                }
            }

            return done;
        }

        public void SetIndexedVariable()
        {
            indexedVariable = true;
        }
    }
}
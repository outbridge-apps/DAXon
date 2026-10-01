////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Expressions.Elaboration;
using System;

namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// A SingletonAtomizer combines the functions of an Atomizer and a CardinalityChecker: it is used to
    /// atomize a sequence of nodes, checking that the result of the atomization contains zero or one atomic
    /// values. Note that the input may be a sequence of nodes or atomic values, even though the result must
    /// contain at most one atomic value.
    /// </summary>
    internal sealed class SingletonAtomizer : UnaryExpression
    {
        private readonly bool allowEmpty;
        private readonly Func<RoleDiagnostic> roleSupplier;

        public override int ImplementationMethod => EVALUATE_METHOD;

        /// <summary>
        /// Get the (partial) name of a class that supports streaming of this kind of expression
        /// </summary>
        public override string StreamerName => "SingletonAtomizer";

        /// <summary>
        /// Get the RoleLocator (used to construct error messages)
        /// </summary>
        /// <returns>the roleDiagnostic locator</returns>
        public RoleDiagnostic Role => roleSupplier();

        /// <summary>
        /// Give a string representation of the expression name for use in diagnostics
        /// </summary>
        /// <returns>the expression name, as a string</returns>
        public override string ExpressionName => "atomizeSingleton";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sequence">the sequence to be atomized</param>
        /// <param name="role">contains information about where the expression appears, for use in any error message</param>
        /// <param name="allowEmpty">true if the result sequence is allowed to be empty.</param>
        public SingletonAtomizer(Expression sequence, Func<RoleDiagnostic> role, bool allowEmpty) : base(sequence)
        {
            this.allowEmpty = allowEmpty;
            this.roleSupplier = role;
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SINGLE_ATOMIC;
        }

        /// <summary>
        /// Simplify an expression
        /// </summary>
        public override Expression Simplify()
        {
            Expression operand = BaseExpression.Simplify();
            if (operand is Literal && ((Literal)operand).GroundedValue is AtomicValue)
            {
                return operand;
            }

            BaseExpression = operand;
            return this;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            Expression operand = BaseExpression;
            ExpressionTool.ResetStaticProperties(this);
            if (Literal.IsEmptySequence(operand))
            {
                if (!allowEmpty)
                {
                    RoleDiagnostic role = roleSupplier();
                    TypeError("An empty sequence is not allowed as the " + role.GetMessage(), role.ErrorCode, null);
                }

                return operand;
            }

            ItemType operandType = operand.GetItemType();
            if (operandType.IsPlainType())
            {
                return operand;
            }

            if (!operandType.IsAtomizable(visitor.GetConfiguration().GetTypeHierarchy()))
            {
                XPathException err;
                if (operandType is MapType)
                {
                    err = new XPathException("Cannot atomize a map (" + ToShortString() + ")").WithErrorCode("FOTY0013");
                }
                else if (operandType is IFunctionItemType)
                {
                    err = new XPathException("Cannot atomize a function item").WithErrorCode("FOTY0013");
                }
                else
                {
                    err = new XPathException("Cannot atomize an element that is defined in the schema to have element-only content").WithErrorCode("FOTY0012");
                }

                throw err.AsTypeError().WithLocation(GetLocation()).WithFailingExpression(ParentExpression);
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression exp = base.Optimize(visitor, contextInfo);
            if (exp == this)
            {
                // Since it's an error for the result to have more than one item, there's no point sorting the input
                BaseExpression = BaseExpression.Unordered(true, false);
                if (BaseExpression.GetItemType().IsPlainType() && !Cardinality.AllowsMany(BaseExpression.GetCardinality()))
                {
                    return BaseExpression;
                }

                return this;
            }
            else
            {
                return exp;
            }
        }

        /// <summary>
        /// Determine the special properties of this expression
        /// </summary>
        /// <returns>StaticProperty.NO_NODES_NEWLY_CREATED.</returns>
        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        /// <summary>
        /// Copy an expression. This makes a deep copy.
        /// </summary>
        /// <param name="rebindings">variables that need to be re-bound</param>
        /// <returns>the copy of the original expression</returns>
        public override Expression Copy(RebindingMap rebindings)
        {
            Expression e2 = new SingletonAtomizer(BaseExpression.Copy(rebindings), roleSupplier, allowEmpty);
            ExpressionTool.CopyLocationInfo(this, e2);
            return e2;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet result = BaseExpression.AddToPathMap(pathMap, pathMapNodeSet);
            if (result != null)
            {
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                ItemType operandItemType = BaseExpression.GetItemType();
                if (th.Relationship(NodeKindTest.ELEMENT, operandItemType) != Affinity.DISJOINT || th.Relationship(NodeKindTest.DOCUMENT, operandItemType) != Affinity.DISJOINT)
                {
                    result.SetAtomized();
                }
            }

            return null;
        }

        /// <summary>
        /// Evaluate as an Item. This should only be called if a singleton or empty sequence is required;
        /// it throws a type error if the underlying sequence is multi-valued.
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            int found = 0;
            AtomicValue result = null;
            ISequenceIterator iter = BaseExpression.Iterate(context);
            IItem item;
            while ((item = iter.Next()) != null)
            {
                IAtomicSequence seq;
                try
                {
                    seq = item.Atomize();
                }
                catch (TerminationException)
                {
                    throw;
                }
                catch (Error.UserDefinedXPathException)
                {
                    throw;
                }
                catch (XPathException e)
                {
                    if (roleSupplier == null)
                    {
                        throw;
                    }
                    else
                    {
                        RoleDiagnostic role = roleSupplier();
                        string message = e.Message + ". Failed while atomizing the " + role.GetMessage();
                        throw new XPathException(message).WithErrorCode(e.ErrorCodeQName).WithLocation(e.GetLocator()).WithXPathContext(context);
                    }
                }

                found += seq.GetLength();
                if (found > 1)
                {
                    RoleDiagnostic role = roleSupplier();
                    TypeError("A sequence of more than one item is not allowed as the " + role.GetMessage() + CardinalityChecker.DepictSequenceStart(BaseExpression.Iterate(context), 3), role.ErrorCode, context);
                }

                if (found == 1)
                {
                    result = (AtomicValue)seq.Head();
                }
            }

            if (found == 0 && !allowEmpty)
            {
                RoleDiagnostic role = roleSupplier();
                TypeError("An empty sequence is not allowed as the " + role.GetMessage(), role.ErrorCode, null);
            }

            return result;
        }

        public override Elaborator GetElaborator()
        {
            return new SingletonAtomizerElaborator();
        }

        // Java-parity elaborator (SingletonAtomizer$SingletonAtomizerElaborator). Composes the base pull
        // evaluator ONCE; the interpreted EvaluateItem calls BaseExpression.Iterate per evaluation, which
        // re-runs MakeElaborator (lock) + the whole ElaborateForPull closure rebuild on every HOF-lambda call.
        internal sealed class SingletonAtomizerElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SingletonAtomizer exp = (SingletonAtomizer)GetExpression();
                IPullEvaluator baseEval = exp.BaseExpression.MakeElaborator().ElaborateForPull();
                IItemEvaluator generic = (context) =>
                {
                    int found = 0;
                    AtomicValue result = null;
                    ISequenceIterator iter = baseEval.Iterate(context);
                    IItem item;
                    while ((item = iter.Next()) != null)
                    {
                        IAtomicSequence seq;
                        try
                        {
                            seq = item.Atomize();
                        }
                        catch (TerminationException)
                        {
                            throw;
                        }
                        catch (Error.UserDefinedXPathException)
                        {
                            throw;
                        }
                        catch (XPathException e)
                        {
                            if (exp.roleSupplier == null)
                            {
                                throw;
                            }
                            else
                            {
                                RoleDiagnostic role = exp.roleSupplier();
                                string message = e.Message + ". Failed while atomizing the " + role.GetMessage();
                                throw new XPathException(message).WithErrorCode(e.ErrorCodeQName).WithLocation(e.GetLocator()).WithXPathContext(context);
                            }
                        }

                        found += seq.GetLength();
                        if (found > 1)
                        {
                            RoleDiagnostic role = exp.roleSupplier();
                            exp.TypeError("A sequence of more than one item is not allowed as the " + role.GetMessage() + CardinalityChecker.DepictSequenceStart(exp.BaseExpression.Iterate(context), 3), role.ErrorCode, context);
                        }

                        if (found == 1)
                        {
                            result = (AtomicValue)seq.Head();
                        }
                    }

                    if (found == 0 && !exp.allowEmpty)
                    {
                        RoleDiagnostic role = exp.roleSupplier();
                        exp.TypeError("An empty sequence is not allowed as the " + role.GetMessage(), role.ErrorCode, null);
                    }

                    return result;
                };

                // Fused single-child atomize for the `fn(childName)` shape: the one matching
                // child's untypedAtomic value straight off the Tiny arrays. Every undecidable
                // case (typed/foreign tree, 2+ matches, disallowed empty) re-runs the generic
                // evaluator above, so errors and messages stay byte-identical.
                if (Elaboration.FusedChildAtomizer.MatchAxis(exp.BaseExpression, out int ffp))
                {
                    bool allowEmpty = exp.allowEmpty;
                    return (context) =>
                    {
                        AtomicValue fast = Elaboration.FusedChildAtomizer.ReadSingleChildUntyped(context, ffp, allowEmpty, out bool off);
                        return off ? generic.Eval(context) : fast;
                    };
                }

                // fn(`$var/childName`): same fused read with the parent taken from the variable.
                // Anything but a single Tiny parent node in the variable goes off-path.
                if (exp.BaseExpression is SlashExpression slash
                    && slash.GetSelectExpression() is VariableReference varRef
                    && Elaboration.FusedChildAtomizer.MatchAxis(slash.GetStep(), out int vfp))
                {
                    bool allowEmpty2 = exp.allowEmpty;
                    return (context) =>
                    {
                        if (varRef.EvaluateVariable(context) is IItem parent)
                        {
                            AtomicValue fast = Elaboration.FusedChildAtomizer.ReadSingleChildUntypedOf(parent, vfp, allowEmpty2, out bool off);
                            if (!off)
                            {
                                return fast;
                            }
                        }

                        return generic.Eval(context);
                    };
                }

                // fn($var): the slot value read directly — an atomic value is its own atomization,
                // and a single untyped Tiny node atomizes straight off the Tiny arrays without
                // throwing, so no per-call SingletonIterator + pull pipeline. Closures, sequences
                // and typed/foreign nodes replay the generic evaluator unchanged.
                if (exp.BaseExpression is VariableReference bareVar)
                {
                    return (context) =>
                    {
                        ISequence v = bareVar.EvaluateVariable(context);
                        if (v is AtomicValue av)
                        {
                            return av;
                        }

                        if (v is Trees.Tiny.TinyNodeImpl tn && tn.tree.TypeArray == null
                            && tn.Atomize() is AtomicValue single)
                        {
                            return single;
                        }

                        return generic.Eval(context);
                    };
                }

                return generic;
            }
        }

        /// <summary>
        /// Determine the data type of the items returned by the expression, if possible
        /// </summary>
        /// <returns>a value such as Types.STRING, Types.BOOLEAN, Types.NUMBER. For this class, the
        /// result is always an atomic type, but it might be more specific.</returns>
        public override ItemType GetItemType()
        {
            bool isSchemaAware = true;
            try
            {
                isSchemaAware = GetPackageData().IsSchemaAware();
            }
            catch (NullReferenceException)
            {
                // ultra-cautious code in case expression container has not been set
                if (!GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION))
                {
                    isSchemaAware = false;
                }
            }

            ItemType @in = BaseExpression.GetItemType();
            if (@in.IsPlainType())
            {
                return @in;
            }
            else if (@in is NodeTest)
            {
                UType kinds = @in.GetUType();
                if (!isSchemaAware)
                {
                    // Some node-kinds always have a typed value that's a string
                    if (Atomizer.STRING_KINDS.Subsumes(kinds))
                    {
                        return BuiltInAtomicType.STRING;
                    }

                    // Some node-kinds are always untyped atomic; some are untypedAtomic provided that the configuration
                    // is untyped
                    if (Atomizer.UNTYPED_IF_UNTYPED_KINDS.Subsumes(kinds))
                    {
                        return BuiltInAtomicType.UNTYPED_ATOMIC;
                    }
                }
                else
                {
                    if (Atomizer.UNTYPED_KINDS.Subsumes(kinds))
                    {
                        return BuiltInAtomicType.UNTYPED_ATOMIC;
                    }
                }

                return @in.GetAtomizedItemType();
            }
            else if (@in is JavaExternalObjectType)
            {
                return @in.GetAtomizedItemType();
            }

            return BuiltInAtomicType.ANY_ATOMIC;
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        protected override int ComputeCardinality()
        {
            if (allowEmpty)
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
            else
            {
                return StaticProperty.EXACTLY_ONE;
            }
        }

        /// <summary>
        /// Diagnostic print of expression structure. The abstract expression tree
        /// is written to the supplied output destination.
        /// </summary>
        /// <param name="out">the destination for the report</param>
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("atomSing", this);
            if (allowEmpty)
            {
                @out.EmitAttribute("card", "?");
            }

            @out.EmitAttribute("diag", Role.Save());
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override string ToShortString()
        {
            return BaseExpression.ToShortString();
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class Atomizer : UnaryExpression
    {

        /// <summary>
        /// Node kinds whose typed value is always a string
        /// </summary>
        public static readonly UType STRING_KINDS = UType.NAMESPACE.Union(UType.COMMENT).Union(UType.PI);
        /// <summary>
        /// Node kinds whose typed value is always untypedAtomic
        /// </summary>
        public static readonly UType UNTYPED_KINDS = UType.TEXT.Union(UType.DOCUMENT);
        /// <summary>
        /// Node kinds whose typed value is untypedAtomic if the configuration is untyped
        /// </summary>
        public static readonly UType UNTYPED_IF_UNTYPED_KINDS = UType.TEXT.Union(UType.ELEMENT).Union(UType.DOCUMENT).Union(UType.ATTRIBUTE);
        private bool untyped; //set to true if it is known that the nodes being atomized will be untyped
        private bool singleValued; // set to true if all atomized nodes will atomize to a single atomic value
        private ItemType operandItemType;
        private readonly Func<RoleDiagnostic> roleSupplier;

        public override int ImplementationMethod => ITERATE_METHOD | WATCH_METHOD;

        public ItemType OperandItemType
        {
            get
            {
                if (operandItemType == null)
                {
                    operandItemType = BaseExpression.GetItemType();
                }

                return operandItemType;
            }
        }

        public override string StreamerName => "Atomizer";

        public override string ExpressionName => "data";
        public Atomizer(Expression sequence, Func<RoleDiagnostic> role) : base(sequence)
        {
            this.roleSupplier = role;
            sequence.SetFlattened(true);
        }

        public static Expression MakeAtomizer(Expression sequence, Func<RoleDiagnostic> roleSupplier)
        {
            if (sequence is Literal && ((Literal)sequence).GroundedValue is IAtomicSequence)
            {
                return sequence;
            }
            else
            {
                return new Atomizer(sequence, roleSupplier);
            }
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.ATOMIC_SEQUENCE;
        }

        public override Expression Simplify()
        {

            untyped = !GetPackageData().IsSchemaAware();
            ComputeSingleValued(GetConfiguration().GetTypeHierarchy());
            Expression operand = BaseExpression.Simplify();
            if (operand is Literal)
            {
                IGroundedValue val = ((Literal)operand).GroundedValue;
                if (val is AtomicValue)
                {
                    return operand;
                }

                ISequenceIterator iter = val.Iterate();
                IItem i;
                while ((i = iter.Next()) != null)
                {
                    if (i is NodeInfo)
                    {
                        return this;
                    }

                    if (i is IFunctionItem)
                    {
                        if (((IFunctionItem)i).IsArray())
                        {
                            return this;
                        }
                        else if (((IFunctionItem)i).IsMap())
                        {
                            throw new XPathException(ExpandMessage("Cannot atomize a map (" + i.ToShortString() + ")")).WithErrorCode("FOTY0013").AsTypeError().WithLocation(GetLocation());
                        }
                        else
                        {
                            throw new XPathException(ExpandMessage("Cannot atomize a function item")).WithErrorCode("FOTY0013").AsTypeError().WithLocation(GetLocation());
                        }
                    }
                }


                // if all items in the sequence are atomic (they generally will be, since this is
                // done at compile time), then return the sequence
                return operand;
            }
            else if (operand is ValueOf && !ReceiverOption.Contains(((ValueOf)operand).GetOptions(), ReceiverOption.DISABLE_ESCAPING))
            {

                // XSLT users tend to use ValueOf unnecessarily
                return ((ValueOf)operand).ConvertToCastAsString();
            }

            BaseExpression = operand;
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            untyped = untyped | !visitor.StaticContext.GetPackageData().IsSchemaAware();

            // If the configuration allows typed data, check whether the content type of these particular nodes is untyped
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            ComputeSingleValued(th);
            ResetLocalStaticProperties();
            ItemType operandType = OperandItemType;
            if (th.IsSubType(operandType, BuiltInAtomicType.ANY_ATOMIC))
            {
                return BaseExpression;
            }

            if (!operandType.IsAtomizable(th))
            {
                XPathException err;
                if (operandType is IFunctionItemType)
                {
                    string thing = operandType is MapType ? "map" : "function item";
                    err = new XPathException(ExpandMessage("Cannot atomize a " + thing)).WithErrorCode("FOTY0013");
                }
                else
                {
                    err = new XPathException(ExpandMessage("Cannot atomize an element that is defined in the schema to have element-only content")).WithErrorCode("FOTY0012");
                }

                throw err.AsTypeError().WithLocation(GetLocation());
            }

            BaseExpression.SetFlattened(true);
            return this;
        }

        private void ComputeSingleValued(TypeHierarchy th)
        {
            ItemType operandType = OperandItemType;
            if (th.Relationship(operandType, ArrayItemType.ANY_ARRAY_TYPE) != Affinity.DISJOINT)
            {
                singleValued = false;
            }
            else
            {
                singleValued = untyped;
                if (!singleValued)
                {
                    ItemType nodeType = BaseExpression.GetItemType();
                    if (nodeType is NodeTest)
                    {
                        if (!nodeType.GetUType().Overlaps(UType.ELEMENT.Union(UType.ATTRIBUTE)))
                        {
                            singleValued = true;
                        }
                        else
                        {
                            ISchemaType st = ((NodeTest)nodeType).ContentType;
                            if (IsSingleValuedSchemaType(st))
                            {

                                // Bug 5803
                                singleValued = true;
                            }
                        }
                    }
                }
            }
        }

        private bool IsSingleValuedSchemaType(ISchemaType st)
        {
            if (st == Untyped.INSTANCE)
            {
                return true;
            }

            if (st.IsSimpleType())
            {
                ISimpleType sim = (ISimpleType)st;
                if (sim.IsAtomicType())
                {
                    return true;
                }
                else if (sim.IsListType())
                {
                    return false;
                }
                else if (sim.IsUnionType())
                {
                    return ((IUnionType)sim).IsPlainType();
                }
                else
                {
                    return false; // can't happen? - fail safe
                }
            }

            if (st.IsComplexType())
            {
                if (st == AnyType.INSTANCE)
                {
                    return false;
                }

                if (((IComplexType)st).IsSimpleContent())
                {
                    return IsSingleValuedSchemaType(((IComplexType)st).SimpleContentType);
                }
            }

            return false; // play safe
        }

        private string ExpandMessage(string message)
        {
            if (roleSupplier == null)
            {
                return message;
            }
            else
            {
                return message + ". Found while atomizing the " + roleSupplier().GetMessage() + " in {" + ToShortString() + "} on line " + GetLocation().GetLineNumber();
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression exp = base.Optimize(visitor, contextInfo);
            if (exp == this)
            {
                TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
                Expression operand = BaseExpression;
                if (th.IsSubType(operand.GetItemType(), BuiltInAtomicType.ANY_ATOMIC))
                {
                    return operand;
                }

                if (operand is ValueOf && !ReceiverOption.Contains(((ValueOf)operand).GetOptions(), ReceiverOption.DISABLE_ESCAPING))
                {

                    // XSLT users tend to use ValueOf unnecessarily
                    Expression cast = ((ValueOf)operand).ConvertToCastAsString();
                    return cast.Optimize(visitor, contextInfo);
                }

                if (operand is LetExpression || operand is ForExpression)
                {

                    // replace data(let $x := y return z) by (let $x := y return data(z))
                    Expression action = ((Assignation)operand).GetAction();
                    ((Assignation)operand).SetAction(new Atomizer(action, roleSupplier));
                    return operand.Optimize(visitor, contextInfo);
                }

                if (operand is Choose)
                {

                    // replace data(if x then y else z) by (if x then data(y) else data(z)
                    ((Choose)operand).AtomizeActions();
                    return operand.Optimize(visitor, contextInfo);
                }

                if (operand is Block)
                {

                    // replace data((x,y,z)) by (data(x), data(y), data(z)) as some of the atomizers
                    // may prove to be redundant. (Also, it helps streaming)
                    Operand[] children = ((Block)operand).GetOperanda();
                    Expression[] atomizedChildren = new Expression[children.Length];
                    for (int i = 0; i < children.Length; i++)
                    {
                        atomizedChildren[i] = new Atomizer(children[i].GetChildExpression(), roleSupplier);
                    }

                    Block newBlock = new Block(atomizedChildren);
                    return newBlock.TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                }

                if (untyped && operand is AxisExpression && ((AxisExpression)operand).Axis == AxisInfo.ATTRIBUTE && ((AxisExpression)operand).GetNodeTest() is NameTest && !((AxisExpression)operand).IsContextPossiblyUndefined())
                {
                    StructuredQName name = ((AxisExpression)operand).GetNodeTest().MatchingNodeName;
                    FingerprintedQName qName = new FingerprintedQName(name, visitor.GetConfiguration().GetNamePool());
                    AttributeGetter ag = new AttributeGetter(qName);
                    ExpressionTool.CopyLocationInfo(this, ag);
                    return ag;
                }

                if (untyped && operand is SimpleStepExpression && ((SimpleStepExpression)operand).GetAxisExpression().Axis == AxisInfo.ATTRIBUTE && ((SimpleStepExpression)operand).GetAxisExpression().GetNodeTest() is NameTest)
                {
                    StructuredQName name = ((SimpleStepExpression)operand).GetAxisExpression().GetNodeTest().MatchingNodeName;
                    FingerprintedQName qName = new FingerprintedQName(name, visitor.GetConfiguration().GetNamePool());
                    AttributeGetter ag = new AttributeGetter(qName);
                    ExpressionTool.CopyLocationInfo(this, ag);
                    return new SlashExpression(((SimpleStepExpression)operand).Start, ag);
                }
            }

            return exp;
        }

        public bool IsUntyped()
        {
            return untyped;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            p &= ~StaticProperty.NODESET_PROPERTIES;

            //        if (!untyped) {
            //            p |= StaticProperty.NOT_UNTYPED_ATOMIC;
            //        }
            return p | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        public override void ResetLocalStaticProperties()
        {
            base.ResetLocalStaticProperties();
            operandItemType = null;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Atomizer copy = new Atomizer(BaseExpression.Copy(rebindings), roleSupplier);
            copy.untyped = untyped;
            copy.singleValued = singleValued;
            ExpressionTool.CopyLocationInfo(this, copy);
            return copy;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            try
            {
                ISequenceIterator @base = BaseExpression.Iterate(context);
                return GetAtomizingIterator(@base, untyped && operandItemType is NodeTest);
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
                    string message = ExpandMessage(e.Message);
                    throw new XPathException(message).WithErrorCode(e.ErrorCodeQName).WithLocation(e.GetLocator()).WithXPathContext(context).MaybeWithLocation(GetLocation());
                }
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return (AtomicValue)MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override ItemType GetItemType()
        {
            operandItemType = BaseExpression.GetItemType();
            return GetAtomizedItemType(BaseExpression, untyped);
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.ANY_ATOMIC.Intersection(GetItemType().GetUType());
        }

        public static ItemType GetAtomizedItemType(Expression operand, bool alwaysUntyped)
        {
            ItemType @in = operand.GetItemType();
            if (@in.IsPlainType())
            {
                return @in;
            }
            else if (@in is NodeTest)
            {
                UType kinds = @in.GetUType();
                if (alwaysUntyped)
                {

                    // Some node-kinds always have a typed value that's a string
                    if (STRING_KINDS.Subsumes(kinds))
                    {
                        return BuiltInAtomicType.STRING;
                    }


                    // Some node-kinds are always untyped atomic; some are untypedAtomic provided that the configuration
                    // is untyped
                    if (UNTYPED_IF_UNTYPED_KINDS.Subsumes(kinds))
                    {
                        return BuiltInAtomicType.UNTYPED_ATOMIC;
                    }
                }
                else
                {
                    if (UNTYPED_KINDS.Subsumes(kinds))
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
            else if (@in is ArrayItemType)
            {
                IPlainType ait = ((ArrayItemType)@in).MemberType.PrimaryType.GetAtomizedItemType() as IPlainType;
                // Mixed/nested array member type (e.g. integer | array) -> xs:anyAtomicType, not ErrorType,
                // so callers like fn:sum accept it and defer deep-flattening to run time.
                return ait ?? BuiltInAtomicType.ANY_ATOMIC;
            }
            else if (@in is IFunctionItemType)
            {
                return ErrorType.GetInstance();
            }

            return BuiltInAtomicType.ANY_ATOMIC;
        }
        protected override int ComputeCardinality()
        {
            ItemType @in = OperandItemType;
            Expression operand = BaseExpression;
            if (singleValued)
            {
                return operand.GetCardinality();
            }
            else if (untyped && @in is NodeTest)
            {
                return operand.GetCardinality();
            }
            else if (Cardinality.AllowsMany(operand.GetCardinality()))
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
            else if (@in.IsPlainType())
            {
                return operand.GetCardinality();
            }
            else if (@in is NodeTest)
            {
                ISchemaType schemaType = ((NodeTest)@in).ContentType;
                if (schemaType.IsAtomicType())
                {

                    // can return at most one atomic value per node
                    return operand.GetCardinality();
                }
            }

            return StaticProperty.ALLOWS_ZERO_OR_MORE;
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

        public static ISequenceIterator GetAtomizingIterator(ISequenceIterator @base, bool oneToOne)
        {
            if (SequenceTool.SupportsGetLength(@base))
            {
                int count = SequenceTool.GetLength(@base);
                if (count == 0)
                {
                    return EmptyIterator.GetInstance();
                }
                else if (count == 1)
                {
                    IItem first = @base.Next();
                    if (first == null)
                        throw new NullReferenceException();
                    return first.Atomize().Iterate();
                }
            }
            else if (@base is IAtomizedValueIterator)
            {
                return new AxisAtomizingIterator((IAtomizedValueIterator)@base);
            }

            if (oneToOne)
            {
                return new UntypedAtomizingIterator(@base);
            }
            else
            {
                return new AtomizingIterator(@base);
            }
        }

        public static IAtomicSequence Atomize(ISequence sequence)
        {
            if (sequence is IAtomicSequence)
            {
                return (IAtomicSequence)sequence;
            }
            else if (sequence is EmptySequence)
            {
                return EmptyAtomicSequence.GetInstance();
            }
            else
            {
                ISequenceIterator iter = GetAtomizingIterator(sequence.Iterate(), false);
                return new AtomicArray(iter);
            }
        }

        public override string ToString()
        {
            return "data(" + BaseExpression.ToString() + ")";
        }

        public override string ToShortString()
        {
            return BaseExpression.ToShortString();
        }

        protected override void EmitExtraAttributes(ExpressionPresenter @out)
        {
            if (roleSupplier != null)
            {
                @out.EmitAttribute("diag", roleSupplier().Save());
            }
        }

        public override Elaborator GetElaborator()
        {
            return new AtomizerElaborator();
        }

        /// <summary>
        /// Elaborator for an Atomizer
        /// </summary>
        internal sealed class AtomizerElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                Atomizer expr = (Atomizer)GetExpression();
                bool oneToOne = expr.IsUntyped() && expr.BaseExpression.GetItemType() is NodeTest;

                // Fused `atomize($nodes/childName)`: matching children are read straight off the
                // Tiny arrays per parent — no per-parent axis iterator, no child node wrapper.
                // Gates: the one-to-one form (so a typed-tree parent atomizes 1:1 through the same
                // UntypedAtomizingIterator), a bare child::NAME step, and a node-only select.
                if (oneToOne
                    && expr.BaseExpression is SlashExpression slash
                    && Elaboration.FusedChildAtomizer.MatchAxis(slash.GetStep(), out int childFp)
                    && slash.GetSelectExpression().GetItemType() is NodeTest)
                {
                    NodeTest childTest = ((AxisExpression)slash.GetStep()).GetNodeTest();
                    IPullEvaluator selectEval = slash.GetSelectExpression().MakeElaborator().ElaborateForPull();
                    return (context) =>
                    {
                        try
                        {
                            ISequenceIterator parents = selectEval.Iterate(context);
                            return new Elaboration.FusedChildAtomizer.ChildSequenceAtomizeIterator(parents, childFp, childTest);
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
                            if (expr.roleSupplier == null)
                            {
                                throw;
                            }
                            else
                            {
                                string message = expr.ExpandMessage(e.Message);
                                throw new XPathException(message).WithErrorCode(e.ErrorCodeQName).WithLocation(e.GetLocator()).WithXPathContext(context).MaybeWithLocation(expr.GetLocation());
                            }
                        }
                        catch (UncheckedXPathException uxe)
                        {
                            XPathException e = uxe.GetXPathException();
                            if (expr.roleSupplier == null)
                            {
                                throw e;
                            }
                            else
                            {
                                string message = expr.ExpandMessage(e.Message);
                                throw new XPathException(message).WithErrorCode(e.ErrorCodeQName).WithLocation(e.GetLocator()).WithXPathContext(context).MaybeWithLocation(expr.GetLocation());
                            }
                        }
                    };
                }

                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                IPullEvaluator generic = (context) =>
                {
                    try
                    {
                        ISequenceIterator @base = baseEval.Iterate(context);
                        return GetAtomizingIterator(@base, oneToOne);
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
                        if (expr.roleSupplier == null)
                        {
                            throw;
                        }
                        else
                        {
                            string message = expr.ExpandMessage(e.Message);
                            throw new XPathException(message).WithErrorCode(e.ErrorCodeQName).WithLocation(e.GetLocator()).WithXPathContext(context).MaybeWithLocation(expr.GetLocation());
                        }
                    }
                    catch (UncheckedXPathException uxe)
                    {
                        XPathException e = uxe.GetXPathException();
                        if (expr.roleSupplier == null)
                        {
                            throw e;
                        }
                        else
                        {
                            string message = expr.ExpandMessage(e.Message);
                            throw new XPathException(message).WithErrorCode(e.ErrorCodeQName).WithLocation(e.GetLocator()).WithXPathContext(context).MaybeWithLocation(expr.GetLocation());
                        }
                    }
                };

                // atomize(descendant::text()[parent leaf]) — the compiled body of
                // distinct-values(//*[not(*)]/text()) and friends: untypedAtomic values are built
                // straight from the text buffer, with no text-node wrappers at all.
                if (oneToOne
                    && expr.BaseExpression is FilterExpression fe
                    && Elaboration.FusedLeafFilter.MatchLeafTexts(fe))
                {
                    return (context) => context.GetContextItem() is Trees.Tiny.TinyParentNodeImpl tiny && tiny.tree.TypeArray == null
                        ? (ISequenceIterator)new Elaboration.FusedLeafFilter.LeafTextIterator(tiny, true)
                        : generic(context);
                }

                return generic;
            }

            public override IItemEvaluator ElaborateForItem()
            {
                Atomizer expr = (Atomizer)GetExpression();
                IItemEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                bool nullable = Cardinality.AllowsZero(expr.BaseExpression.GetCardinality());
                if (nullable)
                {
                    return (context) =>
                    {
                        IItem it = baseEval.Eval(context);
                        if (it == null)
                        {
                            return null;
                        }

                        return it.Atomize().Head();
                    };
                }
                else
                {
                    return (context) => baseEval.Eval(context).Atomize().Head();
                }
            }
        }
    }
}

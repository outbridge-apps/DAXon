////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class LookupExpression : BinaryExpression
    {
        private bool isClassified = false;
        protected bool isArrayLookup = false;
        protected bool isMapLookup = false;
        protected bool isSingleContainer = false;
        protected bool isSingleEntry = false;

        public override string ExpressionName => "lookupExp";

        public override double Cost => GetLhsExpression().Cost * GetRhsExpression().Cost;

        public override int ImplementationMethod => ITERATE_METHOD;
        public LookupExpression(Expression start, Expression step) : base(start, Token.QMARK, step)
        {
        }

        protected override OperandRole GetOperandRole(int arg)
        {
            return arg == 0 ? OperandRole.INSPECT : OperandRole.ABSORB;
        }

        public override ItemType GetItemType()
        {
            if (isClassified)
            {
                if (isArrayLookup)
                {
                    ItemType arrayType = GetLhsExpression().GetItemType();
                    if (arrayType is ArrayItemType)
                    {
                        return ((ArrayItemType)arrayType).MemberType.PrimaryType;
                    }
                }
                else if (isMapLookup)
                {
                    ItemType mapType = GetLhsExpression().GetItemType();
                    if (mapType is RecordTest && GetRhsExpression() is StringLiteral)
                    {
                        string fieldName = ((StringLiteral)GetRhsExpression()).Stringify();
                        SequenceType fieldType = ((RecordTest)mapType).GetFieldType(fieldName);
                        if (fieldType == null)
                        {
                            if (((RecordTest)mapType).IsExtensible())
                            {
                                return AnyItemType.GetInstance();
                            }
                            else
                            {
                                return ErrorType.GetInstance();
                            }
                        }
                        else
                        {
                            return fieldType.PrimaryType;
                        }
                    }
                    else if (mapType is MapType)
                    {
                        return ((MapType)mapType).ValueType.PrimaryType;
                    }
                }
            }

            return AnyItemType.GetInstance();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return GetItemType().GetUType();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            if (Literal.IsEmptySequence(GetLhsExpression()))
            {
                return GetLhsExpression();
            }


            // Running typeCheck on the first operand can lose static type information if it's declared
            // with a tuple type. So check this first.
            ItemType originalType = GetLhsExpression().GetItemType();

            // Check the first operand
            Lhs.TypeCheck(visitor, contextInfo);
            ItemType containerType = GetLhsExpression().GetItemType();
            isArrayLookup = containerType is ArrayItemType;
            bool isTupleLookup = containerType is IRecordType || originalType is IRecordType;
            isMapLookup = containerType is MapType || isTupleLookup;
            // ErrorType is the bottom type: IsSubType(ErrorType, anything) is true, so a statically-empty
            // LHS (e.g. an impossible typeswitch case branch narrowed to ErrorType) would spuriously enter
            // the external-object branch and hit the Saxon-PE licence gate. An ErrorType LHS produces no
            // item at run time, so there is no external-object lookup to license — treat it as an ordinary
            // (vacuous) lookup. Genuine external objects (AnyExternalObjectType/JavaExternalObjectType) still
            // gate, as isSubExt stays true for them.
            if (!(containerType is ErrorType) && th.IsSubType(containerType, AnyExternalObjectType.THE_INSTANCE))
            {
                config.CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "use of lookup expressions on external objects", -1);
                return config.MakeObjectLookupExpression(GetLhsExpression(), GetRhsExpression()).TypeCheck(visitor, contextInfo);
            }

            isSingleContainer = GetLhsExpression().GetCardinality() == StaticProperty.EXACTLY_ONE;
            if (!isArrayLookup && !isMapLookup)
            {

                // TODO: improve error handling here
                if (th.Relationship(containerType, MapType.ANY_MAP_TYPE) == Affinity.DISJOINT && th.Relationship(containerType, ArrayItemType.GetInstance()) == Affinity.DISJOINT && th.Relationship(containerType, AnyExternalObjectType.THE_INSTANCE) == Affinity.DISJOINT)
                {
                    if (Cardinality.AllowsZero(GetLhsExpression().GetCardinality()))
                    {
                        visitor.IssueWarning("The left-hand operand of '?' must be a map or an array; the expression can succeed only if the operand is an empty sequence " + containerType, DAXonErrorCode.SXWN9026, GetLocation());
                    }
                    else
                    {
                        throw new XPathException("The left-hand operand of '?' must be a map or an array; " + "the supplied expression is of type " + containerType, "XPTY0004").WithLocation(GetLocation()).AsTypeError().WithFailingExpression(this);
                    }
                }
            }


            // Now check the second operand
            Rhs.TypeCheck(visitor, contextInfo);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, "?", 1);
            TypeChecker tc = config.GetTypeChecker(false);
            SequenceType req = BuiltInAtomicType.ANY_ATOMIC.ZeroOrMore();
            if (isArrayLookup)
            {
                req = BuiltInAtomicType.INTEGER.ZeroOrMore();
            }

            SetRhsExpression(tc.StaticTypeCheck(GetRhsExpression(), req, role, visitor));
            isSingleEntry = GetRhsExpression().GetCardinality() == StaticProperty.EXACTLY_ONE;
            if (isTupleLookup && GetRhsExpression() is StringLiteral)
            {
                IRecordType tt = (IRecordType)(containerType is IRecordType ? containerType : originalType);
                if (!tt.IsExtensible())
                {
                    string fieldName = ((StringLiteral)GetRhsExpression()).Stringify();
                    if (tt.GetFieldType(fieldName) == null)
                    {
                        throw new XPathException("Field '" + fieldName + "' is not defined in the record type", "XPTY0004").AsTypeError().WithLocation(GetLocation());
                    }
                }
            }

            isClassified = true;
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.Optimize(visitor, contextInfo);
            Rhs.Optimize(visitor, contextInfo);
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            LookupExpression exp = new LookupExpression(GetLhsExpression().Copy(rebindings), GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            exp.isArrayLookup = isArrayLookup;
            exp.isMapLookup = isMapLookup;
            exp.isSingleEntry = isSingleEntry;
            exp.isSingleContainer = isSingleContainer;
            return exp;
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        protected override int ComputeCardinality()
        {
            if (isSingleContainer && isSingleEntry)
            {
                if (isArrayLookup)
                {
                    ItemType arrayType = GetLhsExpression().GetItemType();
                    if (arrayType is ArrayItemType)
                    {
                        return ((ArrayItemType)arrayType).MemberType.GetCardinality();
                    }
                }
                else if (isMapLookup)
                {
                    ItemType mapType = GetLhsExpression().GetItemType();
                    if (mapType is RecordTest && GetRhsExpression() is StringLiteral)
                    {
                        string fieldName = ((StringLiteral)GetRhsExpression()).Stringify();
                        SequenceType fieldType = ((RecordTest)mapType).GetFieldType(fieldName);
                        if (fieldType == null)
                        {
                            return ((RecordTest)mapType).IsExtensible() ? StaticProperty.ALLOWS_ZERO_OR_MORE : StaticProperty.ALLOWS_ZERO;
                        }
                        else
                        {
                            return fieldType.GetCardinality();
                        }
                    }
                    else if (mapType is MapType)
                    {
                        return (Cardinality.Union(((MapType)mapType).ValueType.GetCardinality(), StaticProperty.ALLOWS_ZERO));
                    }
                }
            }

            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            if (!(other is LookupExpression))
            {
                return false;
            }

            LookupExpression p = (LookupExpression)other;
            return GetLhsExpression().IsEqual(p.GetLhsExpression()) && GetRhsExpression().IsEqual(p.GetRhsExpression());
        }

        protected override int ComputeHashCode()
        {
            return "LookupExpression".GetHashCode() ^ GetLhsExpression().GetHashCode() ^ GetRhsExpression().GetHashCode();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        //
        //
        //
        //
        //    }
        private static ISequenceIterator OptionalGroundedValueIterator(IGroundedValue value)
        {
            if (value == null)
            {
                return EmptyIterator.GetInstance();
            }
            else
            {
                return value.Iterate();
            }
        }

        //
        //
        //
        //
        //    }
        public static void MustBeArrayOrMap(Expression exp, IItem baseItem)
        {
            throw new XPathException("The items on the LHS of the '?' operator must be maps or arrays; but value (" + baseItem.ToShortString() + ") was supplied", "XPTY0004").AsTypeError().WithLocation(exp.GetLocation()).WithFailingExpression(exp);
        }

        //
        //
        //
        //
        //    }
        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("lookup", this);
            GetLhsExpression().Export(destination);
            GetRhsExpression().Export(destination);
            destination.EndElement();
        }

        //
        //
        //
        //
        //    }
        public override string ToString()
        {
            string rhs;
            if (GetRhsExpression() is Literal)
            {
                Literal lit = (Literal)GetRhsExpression();
                if (lit is StringLiteral && NameChecker.IsValidNCName(((StringLiteral)lit).GroundedValue.CodePoints()))
                {
                    rhs = ((StringLiteral)lit).Stringify();
                }
                else if (lit.GroundedValue is Int64Value)
                {
                    rhs = lit.GroundedValue.ToString();
                }
                else
                {
                    rhs = ExpressionTool.Parenthesize(lit);
                }
            }
            else
            {
                rhs = ExpressionTool.Parenthesize(GetRhsExpression());
            }

            return ExpressionTool.Parenthesize(GetLhsExpression()) + "?" + rhs;
        }

        //
        //
        //
        //
        //    }
        public override Elaborator GetElaborator()
        {
            return new LookupElaborator();
        }

        //
        //
        //
        //
        //    }
        internal sealed class LookupElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                LookupExpression expr = (LookupExpression)GetExpression();
                if (expr.isArrayLookup)
                {
                    if (expr.isSingleContainer && expr.isSingleEntry)
                    {
                        IItemEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForItem();
                        IItemEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForItem();
                        return (context) =>
                        {
                            ArrayItem array = (ArrayItem)lhs.Eval(context);
                            IntegerValue subscript = (IntegerValue)rhs.Eval(context);
                            int index = ArrayFunctionSet.CheckSubscript(subscript, array.ArrayLength());
                            return array[index - 1].Iterate();
                        };
                    }
                    else if (expr.isSingleEntry)
                    {
                        IPullEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForPull();
                        IItemEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForItem();
                        return (context) =>
                        {
                            IntegerValue subscriptValue = (IntegerValue)rhs.Eval(context);
                            int subscript = subscriptValue.AsSubscript() - 1;
                            return MappingIterator.IMap(lhs.Iterate(context), (baseItem) =>
                            {
                                ArrayItem array = (ArrayItem)baseItem;
                                if (subscript >= 0 && subscript < array.ArrayLength())
                                {
                                    return array[subscript].Iterate();
                                }
                                else
                                {

                                    // reuse the diagnostic logic
                                    ArrayFunctionSet.CheckSubscript(subscriptValue, array.ArrayLength());
                                    return null; // shouldn't happen
                                }
                            });
                        };
                    }
                    else
                    {
                        IPullEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForPull();
                        IPullEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForPull();
                        return (context) =>
                        {
                            ISequenceIterator baseIterator = lhs.Iterate(context);
                            IGroundedValue rhsValue;
                            try
                            {
                                rhsValue = SequenceTool.ToGroundedValue(rhs.Iterate(context));
                            }
                            catch (UncheckedXPathException e)
                            {
                                throw e.GetXPathException();
                            }

                            return MappingIterator.IMap(baseIterator, (baseItem) => MappingIterator.IMap(rhsValue.Iterate(), (index) =>
                            {
                                ArrayItem array = (ArrayItem)baseItem;
                                int subscript = ArrayFunctionSet.CheckSubscript((IntegerValue)index, array.ArrayLength()) - 1;
                                return array[subscript].Iterate();
                            }));
                        };
                    }
                }
                else if (expr.isMapLookup)
                {
                    if (expr.isSingleContainer && expr.isSingleEntry)
                    {
                        IItemEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForItem();
                        IItemEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForItem();
                        return (context) =>
                        {
                            MapItem map = (MapItem)lhs.Eval(context);
                            AtomicValue key = (AtomicValue)rhs.Eval(context);
                            return OptionalGroundedValueIterator(map[key]);
                        };
                    }
                    else if (expr.isSingleEntry)
                    {
                        IPullEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForPull();
                        IItemEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForItem();
                        return (context) =>
                        {
                            ISequenceIterator baseIterator = lhs.Iterate(context);
                            AtomicValue key = (AtomicValue)rhs.Eval(context);
                            return MappingIterator.IMap(baseIterator, (baseItem) => OptionalGroundedValueIterator(((MapItem)baseItem)[key]));
                        };
                    }
                    else
                    {
                        IPullEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForPull();
                        IPullEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForPull();
                        return (context) =>
                        {
                            ISequenceIterator baseIterator = lhs.Iterate(context);
                            IGroundedValue rhsVal;
                            try
                            {
                                rhsVal = SequenceTool.ToGroundedValue(rhs.Iterate(context));
                            }
                            catch (UncheckedXPathException e)
                            {
                                throw e.GetXPathException();
                            }

                            return MappingIterator.IMap(baseIterator, (baseItem) => MappingIterator.IMap(rhsVal.Iterate(), (index) => OptionalGroundedValueIterator(((MapItem)baseItem)[(AtomicValue)index])));
                        };
                    }
                }
                else
                {
                    IPullEvaluator lhs = expr.GetLhsExpression().MakeElaborator().ElaborateForPull();
                    IPullEvaluator rhs = expr.GetRhsExpression().MakeElaborator().ElaborateForPull();
                    return (context) =>
                    {
                        ISequenceIterator baseIterator = lhs.Iterate(context);
                        IGroundedValue rhsVal;
                        try
                        {
                            rhsVal = SequenceTool.ToGroundedValue(rhs.Iterate(context));
                        }
                        catch (UncheckedXPathException e)
                        {
                            throw e.GetXPathException();
                        }

                        IMappingFunction mappingFunction = SequenceMapper.Of((baseItem) =>
                        {
                            switch (baseItem.GetGenre())
                            {
                                case Genre.ARRAY:
                                    {
                                        IMappingFunction arrayAccess = SequenceMapper.Of((index) =>
                                        {
                                            if (index is IntegerValue)
                                            {
                                                int subscript = ArrayFunctionSet.CheckSubscript((IntegerValue)index, ((ArrayItem)baseItem).ArrayLength()) - 1;
                                                IGroundedValue member = ((ArrayItem)baseItem)[subscript];
                                                return member.Iterate();
                                            }
                                            else
                                            {
                                                throw new XPathException("An item on the LHS of the '?' operator (" + expr.GetLhsExpression().ToShortString() + ") is an array, but a value on the RHS of the operator (" + baseItem.ToShortString() + ") is not an integer", "XPTY0004").AsTypeError().WithLocation(expr.GetLocation()).WithFailingExpression(expr);
                                            }
                                        });
                                        ISequenceIterator rhsIter = rhsVal.Iterate();
                                        return new MappingIterator(rhsIter, arrayAccess);
                                    }

                                case Genre.MAP:
                                    {
                                        ISequenceIterator rhsIter = rhsVal.Iterate();
                                        return MappingIterator.IMap(rhsIter, (key) => OptionalGroundedValueIterator(((MapItem)baseItem)[(AtomicValue)key]));
                                    }

                                case Genre.EXTERNAL:
                                    {
                                        if (!(rhsVal is StringValue))
                                        {
                                            throw new XPathException("An item on the LHS of the '?' operator is an external object, but a value on the RHS of the operator (" + baseItem.ToShortString() + ") is not a singleton string", "XPTY0004").AsTypeError().WithLocation(expr.GetLocation()).WithFailingExpression(expr);
                                        }

                                        string key = ((StringValue)rhsVal).GetStringValue();
                                        IGroundedValue entry = context.GetConfiguration().ExternalObjectAsMap((ObjectValue<object>)baseItem, key)[(StringValue)rhsVal];
                                        if (entry == null)
                                        {
                                            throw new XPathException("There is no unique method named " + key + " in the external object of type " + ((ObjectValue<object>)baseItem).GetObject().GetType().FullName, "XPTY0004");
                                        }

                                        return entry.Iterate();
                                    }

                                default:
                                    {
                                        MustBeArrayOrMap(expr, baseItem);
                                        return null;
                                    }
                            }
                        });
                        return new MappingIterator(baseIterator, mappingFunction);
                    };
                }
            }
        }
    }
}

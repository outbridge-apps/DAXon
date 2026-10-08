////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using static OutSmart.DAXon.Types.Affinity;
using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class TypeChecker
    {
        public TypeChecker()
        {
        }

        public virtual Expression StaticTypeCheck(Expression supplied, SequenceType req, Func<RoleDiagnostic> roleSupplier, ExpressionVisitor visitor)
        {

            if (supplied.ImplementsStaticTypeCheck())
            {
                return supplied.StaticTypeCheck(req, false, roleSupplier, visitor);
            }

            IStaticContext env = visitor.StaticContext;
            Configuration config = env.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            if (supplied is Literal && ((Literal)supplied).IsInstance(req, th))
            {
                return supplied;
            }

            Expression exp = supplied;
            ContextItemStaticInfo defaultContextInfo = config.DefaultContextItemStaticInfo;
            bool allow40 = env.GetXPathVersion() >= 40;
            ItemType reqItemType = req.PrimaryType;
            int reqCard = req.GetCardinality();
            ItemType suppliedItemType = null;

            // item type of the supplied expression: null means not yet calculated
            int suppliedCard = -1;

            // cardinality of the supplied expression: -1 means not yet calculated
            bool cardOK = reqCard == StaticProperty.ALLOWS_ZERO_OR_MORE;

            // Unless the required cardinality is zero-or-more (no constraints).
            // check the static cardinality of the supplied expression
            if (!cardOK)
            {
                suppliedCard = exp.GetCardinality();
                cardOK = Cardinality.Subsumes(reqCard, suppliedCard); // May later find that cardinality is not OK after all, if atomization takes place
            }

            bool itemTypeOK = reqItemType is AnyItemType;
            if (reqCard == StaticProperty.ALLOWS_ZERO)
            {

                // required type is empty sequence; we don't need an item check because a cardinality check suffices
                itemTypeOK = true;
            }


            // Unless the required item type and content type are ITEM (no constraints)
            // check the static item type against the supplied expression.
            // NOTE: we don't currently do any static inference regarding the content type
            if (!itemTypeOK)
            {
                suppliedItemType = exp.GetItemType();
                if (reqItemType == null || suppliedItemType == null)
                {
                    throw new NullReferenceException();
                }

                Affinity affinity = th.Relationship(reqItemType, suppliedItemType);
                itemTypeOK = affinity == Affinity.SAME_TYPE || affinity == Affinity.SUBSUMES;
            }

            if (reqItemType.IsPlainType())
            {
                if (!itemTypeOK)
                {

                    // rule 1: Atomize
                    if (!suppliedItemType.IsPlainType() && !(suppliedCard == StaticProperty.EMPTY))
                    {
                        bool atomizable = suppliedItemType.IsAtomizable(th);
                        if (atomizable && (exp.GetSpecialProperties() & StaticProperty.COMPUTED_FUNCTION) != 0)
                        {
                            atomizable = false; // in this case we know the function isn't going to be an array
                        }

                        if (!atomizable)
                        {
                            string shortItemType;
                            if (suppliedItemType is IRecordType)
                            {
                                shortItemType = "a record type";
                            }
                            else if (suppliedItemType is MapType)
                            {
                                shortItemType = "a map type";
                            }
                            else if (suppliedItemType is IFunctionItemType)
                            {
                                shortItemType = "a function type";
                            }
                            else if (suppliedItemType is NodeTest)
                            {
                                shortItemType = "an element type with element-only content";
                            }
                            else
                            {
                                shortItemType = suppliedItemType.ToString();
                            }

                            RoleDiagnostic role = roleSupplier();
                            throw new XPathException("An atomic value is required for the " + role.GetMessage() + ", but the supplied type is " + shortItemType + ", which cannot be atomized").WithErrorCode("FOTY0013").WithLocation(supplied.GetLocation()).AsTypeError().WithFailingExpression(supplied);
                        }

                        if (exp.GetRetainedStaticContext() == null)
                        {
                            exp.SetRetainedStaticContextLocally(env.MakeRetainedStaticContext());
                        }

                        Expression cexp = Atomizer.MakeAtomizer(exp, roleSupplier);
                        ExpressionTool.CopyLocationInfo(exp, cexp);
                        exp = cexp;
                        cexp = exp.Simplify();
                        ExpressionTool.CopyLocationInfo(exp, cexp);
                        exp = cexp;
                        suppliedItemType = exp.GetItemType();
                        suppliedCard = exp.GetCardinality();
                        cardOK = Cardinality.Subsumes(reqCard, suppliedCard);
                    }
                }


                // rule 2: convert untypedAtomic to the required type
                // The specification says we do untypedAtomic conversion first, then promotion. However, if the
                // target type is one to which promotion applies, then we combine the two operations into one:
                // the conversion functions that handle type promotion (for example from float to double) also
                // handle conversion from untypedAtomic, so we only need to make one pass over the data.
                // rule 3: type promotion (combined with untypedAtomic conversion)
                if (reqItemType is BuiltInAtomicType && ((BuiltInAtomicType)reqItemType).IsPrimitiveType() && !itemTypeOK)
                {
                    int rt = ((BuiltInAtomicType)reqItemType).Fingerprint;
                    UType promotables = PromotableTypes(rt, allow40);
                    if (suppliedItemType.GetUType().Intersection(promotables).Equals(UType.VOID))
                    {

                        // Promotion cannot succeed: raise a static type error
                        RoleDiagnostic role = roleSupplier();
                        throw new XPathException("An item of type " + suppliedItemType + " cannot be converted to " + reqItemType + " as required for the " + role.GetMessage()).WithErrorCode(role.ErrorCode).WithLocation(supplied.GetLocation()).WithFailingExpression(supplied);
                    }

                    ConversionRules rules = config.GetConversionRules();
                    Expression promoted = null;
                    Converter converter = MakePromotingConverter(suppliedItemType, rt, rules, allow40);
                    if (converter != null)
                    {
                        promoted = MakePromoter(exp, converter, (BuiltInAtomicType)reqItemType);
                    }

                    if (promoted != null)
                    {
                        if (promoted is AtomicSequenceConverter)
                        {
                            ((AtomicSequenceConverter)promoted).SetRoleDiagnostic(roleSupplier);
                        }

                        exp = promoted;
                        try
                        {
                            exp = exp.Simplify().TypeCheck(visitor, defaultContextInfo);
                        }
                        catch (XPathException err)
                        {
                            throw err.MaybeWithLocation(exp.GetLocation()).AsStaticError().WithFailingExpression(supplied);
                        }

                        suppliedItemType = reqItemType;
                        suppliedCard = -1;
                        itemTypeOK = true;
                    }
                }

                if (!itemTypeOK)
                {

                    // Revisit rule 2 (conversion from untyped atomic) for target types that have not been handled by a Promoter
                    //   2b: all supplied values are untyped atomic. Convert if necessary, and we're finished.
                    if (suppliedItemType.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) && !(reqItemType.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) || reqItemType.Equals(BuiltInAtomicType.ANY_ATOMIC)))
                    {
                        if (((IPlainType)reqItemType).IsNamespaceSensitive())
                        {

                            // See spec bug 11964
                            RoleDiagnostic role = roleSupplier();
                            throw new XPathException("An untyped atomic value cannot be converted to a QName or NOTATION as required for the " + role.GetMessage()).WithErrorCode("XPTY0117").WithLocation(supplied.GetLocation()).WithFailingExpression(supplied);
                        }

                        UntypedSequenceConverter cexp = UntypedSequenceConverter.MakeUntypedSequenceConverter(config, exp, (IPlainType)reqItemType);
                        cexp.SetRoleDiagnostic(roleSupplier);
                        ExpressionTool.CopyLocationInfo(exp, cexp);
                        try
                        {
                            if (exp is Literal)
                            {
                                try
                                {
                                    exp = Literal.MakeLiteral(SequenceTool.ToGroundedValue(cexp.Iterate(visitor.MakeDynamicContext())), exp);
                                    ExpressionTool.CopyLocationInfo(cexp, exp);
                                }
                                catch (UncheckedXPathException e)
                                {
                                    throw e.GetXPathException();
                                }
                            }
                            else
                            {
                                exp = cexp;
                            }
                        }
                        catch (XPathException err)
                        {
                            throw err.MaybeWithLocation(exp.GetLocation()).WithFailingExpression(supplied).MaybeWithErrorCode(roleSupplier().ErrorCode).AsStaticError();
                        }

                        itemTypeOK = true;
                        suppliedItemType = reqItemType;
                    }


                    //   2c: some supplied values are untyped atomic. Convert these to the required type; but
                    //   there may be other values in the sequence that won't convert and still need to be checked
                    if (suppliedItemType.Equals(BuiltInAtomicType.ANY_ATOMIC) && !(reqItemType.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) || reqItemType.Equals(BuiltInAtomicType.ANY_ATOMIC)) && !exp.HasSpecialProperty(StaticProperty.NOT_UNTYPED_ATOMIC))
                    {
                        Expression conversion;
                        if (((IPlainType)reqItemType).IsNamespaceSensitive())
                        {
                            conversion = UntypedSequenceConverter.MakeUntypedSequenceRejector(config, exp, (IPlainType)reqItemType);
                        }
                        else
                        {
                            UntypedSequenceConverter usc = UntypedSequenceConverter.MakeUntypedSequenceConverter(config, exp, (IPlainType)reqItemType);
                            usc.SetRoleDiagnostic(roleSupplier);
                            conversion = usc;
                        }

                        ExpressionTool.CopyLocationInfo(exp, conversion);
                        try
                        {
                            if (exp is Literal)
                            {
                                try
                                {
                                    exp = Literal.MakeLiteral(SequenceTool.ToGroundedValue(conversion.Iterate(visitor.MakeDynamicContext())), exp);
                                    ExpressionTool.CopyLocationInfo(supplied, exp);
                                }
                                catch (UncheckedXPathException e)
                                {
                                    throw e.GetXPathException();
                                }
                            }
                            else
                            {
                                exp = conversion;
                            }

                            suppliedItemType = exp.GetItemType();
                        }
                        catch (XPathException err)
                        {
                            throw err.MaybeWithLocation(exp.GetLocation()).AsStaticError();
                        }
                    }
                }


                // New 4.0 rule - relabelling (or "downcasting")
                if (!itemTypeOK && reqItemType.BasicAlphaCode.Length > 2 && visitor.StaticContext.GetXPathVersion() >= 40)
                {

                    // allow down-conversion ("relabelling")
                    if (reqItemType.GetUType().Overlaps(suppliedItemType.GetUType()))
                    {
                        itemTypeOK = true;
                        Expression cexp = MakeDownCaster(exp, (IAtomicType)reqItemType, config);
                        if (cexp is AtomicSequenceConverter)
                        {
                            ((AtomicSequenceConverter)cexp).SetRoleDiagnostic(roleSupplier);
                        }

                        ExpressionTool.CopyLocationInfo(exp, cexp);
                        exp = cexp;
                        try
                        {
                            exp = exp.Simplify().TypeCheck(visitor, defaultContextInfo);
                        }
                        catch (XPathException err)
                        {
                            throw err.MaybeWithLocation(exp.GetLocation()).AsStaticError().WithFailingExpression(supplied);
                        }

                        suppliedItemType = reqItemType;
                    }
                } // Function coercion
            }
            else if (!itemTypeOK && reqItemType is IFunctionItemType && !((IFunctionItemType)reqItemType).IsMapType() && !((IFunctionItemType)reqItemType).IsArrayType())
            {
                Affinity r = th.Relationship(suppliedItemType, th.GenericFunctionItemType);
                if (r != DISJOINT)
                {
                    if (!(suppliedItemType is IFunctionItemType))
                    {
                        exp = new ItemChecker(exp, th.GenericFunctionItemType, roleSupplier);
                        suppliedItemType = th.GenericFunctionItemType;
                    }

                    exp = MakeFunctionSequenceCoercer(exp, (IFunctionItemType)reqItemType, roleSupplier, allow40);
                    itemTypeOK = true;
                } // External object conversion
            }
            else if (!itemTypeOK && reqItemType is JavaExternalObjectType && reqCard == StaticProperty.EXACTLY_ONE)
            {
                if (typeof(ISequence).IsAssignableFrom(((JavaExternalObjectType)reqItemType).JavaClass))
                {

                    // special case: allow an extension function to call an instance method on the implementation type of an XDM value
                    // we leave the conversion to be sorted out at run-time
                    itemTypeOK = true;
                }
                else if (supplied is FunctionCall)
                {

                    // adjust the required type of the Java extension function call
                    // this does nothing unless supplied is an is JavaExtensionFunctionCall if (((FunctionCall)supplied).AdjustRequiredType((JavaExternalObjectType)reqItemType))
                    {
                        itemTypeOK = true;
                        cardOK = true;
                    }
                }
            }


            // If both the cardinality and item type are statically OK, return now.
            if (itemTypeOK && cardOK)
            {
                return exp;
            }


            // If we haven't evaluated the cardinality of the supplied expression, do it now
            if (suppliedCard == -1)
            {
                suppliedCard = exp.GetCardinality();
                if (!cardOK)
                {
                    cardOK = Cardinality.Subsumes(reqCard, suppliedCard);
                }
            }


            // If an empty sequence was explicitly supplied, and empty sequence is allowed,
            // then the item type doesn't matter
            if (cardOK && suppliedCard == StaticProperty.EMPTY)
            {
                return exp;
            }


            // If the supplied value is () and () isn't allowed, fail now
            if (suppliedCard == StaticProperty.EMPTY && ((reqCard & StaticProperty.ALLOWS_ZERO) == 0))
            {
                RoleDiagnostic role = roleSupplier();
                throw new XPathException("An empty sequence is not allowed as the " + role.GetMessage()).WithErrorCode(role.ErrorCode).WithLocation(supplied.GetLocation()).AsTypeErrorIf(role.IsTypeError()).WithFailingExpression(supplied);
            }


            // Try a static type check. We only throw it out if the call cannot possibly succeed, unless
            // pessimistic type checking is enabled
            Affinity relation = itemTypeOK ? SUBSUMED_BY : th.Relationship(suppliedItemType, reqItemType);
            if (reqCard == StaticProperty.ALLOWS_ZERO)
            {

                //  No point doing any item checking if no items are allowed in the result
                relation = SAME_TYPE;
            }

            if (relation == DISJOINT)
            {

                // The item types may be disjoint, but if both the supplied and required types permit
                // an empty sequence, we can't raise a static error. Raise a warning instead.
                RoleDiagnostic role = roleSupplier();
                if (Cardinality.AllowsZero(suppliedCard) && Cardinality.AllowsZero(reqCard))
                {
                    if (suppliedCard != StaticProperty.EMPTY)
                    {
                        string msg = role.ComposeErrorMessage(reqItemType, supplied, th);
                        msg += ". The expression can succeed only if the supplied value is an empty sequence.";
                        visitor.IssueWarning(msg, DAXonErrorCode.SXWN9026, supplied.GetLocation());
                    }
                }
                else
                {
                    string msg = role.ComposeErrorMessage(reqItemType, supplied, th);
                    throw new XPathException(msg).WithErrorCode(role.ErrorCode).WithLocation(supplied.GetLocation()).AsTypeErrorIf(role.IsTypeError()).WithFailingExpression(supplied);
                }
            }


            // Unless the type is guaranteed to match, add a dynamic type check,
            // unless the value is already known in which case we might as well report
            // the error now.
            if (!(relation == SAME_TYPE || relation == SUBSUMED_BY))
            {
                if (exp is Literal)
                {

                    // Try a more detailed check, since for maps, functions etc getItemType() can be imprecise
                    if (req.Matches(((Literal)exp).GroundedValue, th))
                    {
                        return exp;
                    }

                    RoleDiagnostic role = roleSupplier();
                    string msg = role.ComposeErrorMessage(reqItemType, supplied, th);
                    throw new XPathException(msg).WithErrorCode(role.ErrorCode).WithLocation(supplied.GetLocation()).AsTypeErrorIf(role.IsTypeError()).WithFailingExpression(supplied);
                }
                else
                {
                    Expression cexp = new ItemChecker(exp, reqItemType, roleSupplier);
                    ExpressionTool.CopyLocationInfo(exp, cexp);
                    exp = cexp;
                }
            }

            if (!cardOK)
            {
                if (exp is Literal)
                {
                    RoleDiagnostic role = roleSupplier();
                    throw new XPathException("Required cardinality of " + role.GetMessage() + " is " + Cardinality.Describe(reqCard) + "; supplied value has cardinality " + Cardinality.Describe(suppliedCard)).WithErrorCode(role.ErrorCode).WithLocation(supplied.GetLocation()).WithFailingExpression(supplied).AsTypeErrorIf(role.IsTypeError());
                }
                else
                {
                    Expression cexp = CardinalityChecker.MakeCardinalityChecker(exp, reqCard, roleSupplier);
                    ExpressionTool.CopyLocationInfo(exp, cexp);
                    exp = cexp;
                }
            }

            return exp;
        }

        public static Converter MakePromotingConverter(ItemType suppliedItemType, int requiredType, ConversionRules rules, bool allow40)
        {
            switch (requiredType)
            {
                case StandardNames.XS_DOUBLE:
                    return new PromoterToDouble(rules);
                case StandardNames.XS_FLOAT:
                    return new PromoterToFloat(rules);
                case StandardNames.XS_STRING:
                    return new PromoterToString();
                case StandardNames.XS_ANY_URI:
                    if (allow40)
                    {
                        return new PromoterToAnyURI();
                    }

                    break;
                case StandardNames.XS_HEX_BINARY:
                    if (allow40)
                    {
                        return new PromoterToHexBinary();
                    }

                    break;
                case StandardNames.XS_BASE64_BINARY:
                    if (allow40)
                    {
                        return new PromoterToBase64Binary();
                    }

                    break;
            }

            return null;
        }

        public virtual Expression MakeArithmeticExpression(Expression lhs, int @operator, Expression rhs)
        {
            return new ArithmeticExpression(lhs, @operator, rhs);
        }

        public virtual Expression MakeGeneralComparison(Expression lhs, int @operator, Expression rhs)
        {
            return new GeneralComparison20(lhs, @operator, rhs);
        }

        public virtual Expression ProcessValueOf(Expression select, Configuration config)
        {
            return select;
        }

        private static Expression MakeFunctionSequenceCoercer(Expression exp, IFunctionItemType reqItemType, Func<RoleDiagnostic> role, bool allow40)
        {

            // Apply function coercion as defined in XPath 3.0 or 4.0
            return reqItemType.MakeFunctionSequenceCoercer(exp, role, allow40);
        }

        private Expression MakeDownCaster(Expression exp, IAtomicType reqItemType, Configuration config)
        {
            return AtomicSequenceConverter.MakeDownCaster(exp, reqItemType, config);
        }

        public static Expression StrictTypeCheck(Expression supplied, SequenceType req, Func<RoleDiagnostic> roleSupplier, IStaticContext env)
        {

            Expression exp = supplied;
            TypeHierarchy th = env.GetConfiguration().GetTypeHierarchy();
            ItemType reqItemType = req.PrimaryType;
            int reqCard = req.GetCardinality();
            ItemType suppliedItemType = null;

            // item type of the supplied expression: null means not yet calculated
            int suppliedCard = -1;

            // cardinality of the supplied expression: -1 means not yet calculated
            bool cardOK = reqCard == StaticProperty.ALLOWS_ZERO_OR_MORE;

            if (!cardOK)
            {
                suppliedCard = exp.GetCardinality();
                cardOK = Cardinality.Subsumes(reqCard, suppliedCard);
            }

            bool itemTypeOK = req.PrimaryType is AnyItemType;

            if (!itemTypeOK)
            {
                suppliedItemType = exp.GetItemType();
                Affinity affinity = th.Relationship(reqItemType, suppliedItemType);
                itemTypeOK = affinity == SAME_TYPE || affinity == SUBSUMES;
            }


            // If both the cardinality and item type are statically OK, return now.
            if (itemTypeOK && cardOK)
            {
                return exp;
            }


            // If we haven't evaluated the cardinality of the supplied expression, do it now
            if (suppliedCard == -1)
            {
                if (suppliedItemType is ErrorType)
                {
                    suppliedCard = StaticProperty.EMPTY;
                }
                else
                {
                    suppliedCard = exp.GetCardinality();
                }

                if (!cardOK)
                {
                    cardOK = Cardinality.Subsumes(reqCard, suppliedCard);
                }
            }


            if (cardOK && suppliedCard == StaticProperty.EMPTY)
            {
                return exp;
            }


            // If we haven't evaluated the item type of the supplied expression, do it now
            if (suppliedItemType == null)
            {
                suppliedItemType = exp.GetItemType();
            }

            if (suppliedCard == StaticProperty.EMPTY && ((reqCard & StaticProperty.ALLOWS_ZERO) == 0))
            {
                RoleDiagnostic role = roleSupplier();
                XPathException err = new XPathException("An empty sequence is not allowed as the " + role.GetMessage(), role.ErrorCode, supplied.GetLocation());
                err.SetIsTypeError(role.IsTypeError());
                throw err;
            }


            // Try a static type check. We only throw it out if the call cannot possibly succeed.
            Affinity relation = th.Relationship(suppliedItemType, reqItemType);
            if (relation == DISJOINT)
            {

                if (Cardinality.AllowsZero(suppliedCard) && Cardinality.AllowsZero(reqCard))
                {
                    if (suppliedCard != StaticProperty.EMPTY)
                    {
                        RoleDiagnostic role = roleSupplier();
                        string msg = "Required item type of " + role.GetMessage() + " is " + reqItemType + "; supplied value (" + supplied.ToShortString() + ") has item type " + suppliedItemType + ". The expression can succeed only if the supplied value is an empty sequence.";
                        env.IssueWarning(msg, DAXonErrorCode.SXWN9026, supplied.GetLocation());
                    }
                }
                else
                {
                    RoleDiagnostic role = roleSupplier();
                    string msg = role.ComposeErrorMessage(reqItemType, supplied, th);
                    XPathException err = new XPathException(msg, role.ErrorCode, supplied.GetLocation());
                    err.SetIsTypeError(role.IsTypeError());
                    throw err;
                }
            }


            if (!(relation == SAME_TYPE || relation == SUBSUMED_BY))
            {
                Expression cexp = new ItemChecker(exp, reqItemType, roleSupplier);
                cexp.AdoptChildExpression(exp);
                exp = cexp;
            }

            if (!cardOK)
            {
                if (exp is Literal)
                {
                    RoleDiagnostic role = roleSupplier();
                    XPathException err = new XPathException("Required cardinality of " + role.GetMessage() + " is " + Cardinality.Describe(reqCard) + "; supplied value has cardinality " + Cardinality.Describe(suppliedCard), role.ErrorCode, supplied.GetLocation());
                    err.SetIsTypeError(role.IsTypeError());
                    throw err;
                }
                else
                {
                    Expression cexp = CardinalityChecker.MakeCardinalityChecker(exp, reqCard, roleSupplier);
                    cexp.AdoptChildExpression(exp);
                    exp = cexp;
                }
            }

            return exp;
        }

        public static XPathException TestConformance(ISequence val, SequenceType requiredType, IXPathContext context)
        {
            ItemType reqItemType = requiredType.PrimaryType;
            ISequenceIterator iter = val.Iterate();
            int count = 0;
            for (IItem item; (item = iter.Next()) != null;)
            {
                count++;
                if (!reqItemType.Matches(item, context.GetConfiguration().GetTypeHierarchy()))
                {
                    return new XPathException("Required type is " + reqItemType + "; supplied value has type " + UType.GetUType(val.Materialize())).AsTypeError().WithErrorCode("XPTY0004");
                }
            }

            int reqCardinality = requiredType.GetCardinality();
            if (count == 0 && !Cardinality.AllowsZero(reqCardinality))
            {
                return new XPathException("Required type does not allow empty sequence, but supplied value is empty").AsTypeError().WithErrorCode("XPTY0004");
            }

            if (count > 1 && !Cardinality.AllowsMany(reqCardinality))
            {
                return new XPathException("Required type requires a singleton sequence; supplied value contains " + count + " items").AsTypeError().WithErrorCode("XPTY0004");
            }

            if (count > 0 && reqCardinality == StaticProperty.EMPTY)
            {
                return new XPathException("Required type requires an empty sequence, but supplied value is non-empty").AsTypeError().WithErrorCode("XPTY0004");
            }

            return null;
        }

        public static XPathException EbvError(Expression exp, TypeHierarchy th)
        {
            if (Cardinality.AllowsZero(exp.GetCardinality()))
            {
                return null;
            }

            ItemType t = exp.GetItemType();
            if (th.Relationship(t, Types.Type.NODE_TYPE) == DISJOINT && th.Relationship(t, BuiltInAtomicType.BOOLEAN) == DISJOINT && th.Relationship(t, BuiltInAtomicType.STRING) == DISJOINT && th.Relationship(t, BuiltInAtomicType.ANY_URI) == DISJOINT && th.Relationship(t, BuiltInAtomicType.UNTYPED_ATOMIC) == DISJOINT && th.Relationship(t, NumericType.GetInstance()) == DISJOINT && !(t is JavaExternalObjectType))
            {
                return new XPathException("Effective boolean value is defined only for sequences containing " + "booleans, strings, numbers, URIs, or nodes").WithErrorCode("FORG0006").AsTypeError();
            }

            return null;
        }

        private static Expression MakePromoter(Expression exp, Converter converter, BuiltInAtomicType type)
        {
            ConversionRules rules = exp.GetConfiguration().GetConversionRules();
            converter.SetConversionRules(rules);
            if (exp is Literal && ((Literal)exp).GroundedValue is AtomicValue)
            {
                IConversionResult result = converter.Convert((AtomicValue)((Literal)exp).GroundedValue);
                if (result is AtomicValue)
                {
                    Literal converted = Literal.MakeLiteral((AtomicValue)result, exp);
                    ExpressionTool.CopyLocationInfo(exp, converted);
                    return converted;
                }
            }

            AtomicSequenceConverter asc = new AtomicSequenceConverter(exp, type);
            asc.SetConverter(converter);
            ExpressionTool.CopyLocationInfo(exp, asc);
            return asc;
        }

        private UType PromotableTypes(int targetType, bool allow40)
        {
            if (allow40)
            {
                switch (targetType)
                {
                    case StandardNames.XS_DOUBLE:
                        return UType.UNTYPED_ATOMIC.Union(UType.DECIMAL).Union(UType.FLOAT).Union(UType.DOUBLE);
                    case StandardNames.XS_FLOAT:
                        return UType.UNTYPED_ATOMIC.Union(UType.DECIMAL).Union(UType.FLOAT);
                    case StandardNames.XS_ANY_URI:
                    case StandardNames.XS_STRING:
                        return UType.UNTYPED_ATOMIC.Union(UType.ANY_URI).Union(UType.STRING);
                    case StandardNames.XS_HEX_BINARY:
                    case StandardNames.XS_BASE64_BINARY:
                        return UType.UNTYPED_ATOMIC.Union(UType.HEX_BINARY).Union(UType.BASE64_BINARY);
                    default:
                        return UType.UNTYPED_ATOMIC.Union(UType.FromTypeCode(targetType));
                }
            }
            else
            {
                switch (targetType)
                {
                    case StandardNames.XS_DOUBLE:
                        return UType.UNTYPED_ATOMIC.Union(UType.DECIMAL).Union(UType.FLOAT).Union(UType.DOUBLE);
                    case StandardNames.XS_FLOAT:
                        return UType.UNTYPED_ATOMIC.Union(UType.DECIMAL).Union(UType.FLOAT);
                    case StandardNames.XS_STRING:
                        return UType.UNTYPED_ATOMIC.Union(UType.STRING).Union(UType.ANY_URI);
                    default:
                        return UType.UNTYPED_ATOMIC.Union(UType.FromTypeCode(targetType));
                }
            }
        }
    }
}
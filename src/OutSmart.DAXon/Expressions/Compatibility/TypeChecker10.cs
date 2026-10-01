////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Compatibility
{
    /// <summary>
    /// This class provides type checking capability with XPath 1.0 backwards compatibility enabled.
    /// </summary>
    internal sealed class TypeChecker10 : TypeChecker
    {
        public TypeChecker10()
        {
        }

        public override Expression StaticTypeCheck(Expression supplied, SequenceType req, Func<RoleDiagnostic> roleSupplier, ExpressionVisitor visitor)
        {
            if (supplied.ImplementsStaticTypeCheck())
            {
                return supplied.StaticTypeCheck(req, true, roleSupplier, visitor);
            }


            //        In a static function call, if XPath 1.0 compatibility mode is true and an argument of a static function is
            //        not of the expected type, then the following conversions are applied sequentially to the argument value V:
            //        (1) If the expected type calls for a single item or optional single item(examples:xs:
            //        string, xs:string ?, xs:untypedAtomic, xs:untypedAtomic ?, node(), node() ?, item(), item() ?),then the value V
            //        is effectively replaced by V[1].
            //        (2) If the expected type is xs:string or xs:string?, then the value V is effectively replaced by fn:string(V).
            //        (3) If the expected type is xs:double or xs:double?,then the value V is effectively replaced by fn:number(V).
            //        We interpret this as including xs:numeric so that the intended effect is achieved with functions such as fn:floor().
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();

            // rule 1
            if (!Cardinality.AllowsMany(req.GetCardinality()) && Cardinality.AllowsMany(supplied.GetCardinality()))
            {
                Expression cexp = FirstItemExpression.MakeFirstItemExpression(supplied);
                cexp.AdoptChildExpression(supplied);
                supplied = cexp;
            }


            // rule 2
            ItemType reqItemType = req.PrimaryType;
            if (req.PrimaryType.Equals(BuiltInAtomicType.STRING) && !Cardinality.AllowsMany(req.GetCardinality()) && !th.IsSubType(supplied.GetItemType(), BuiltInAtomicType.STRING))
            {
                RetainedStaticContext rsc = supplied.GetRetainedStaticContext();
                Expression fn = SystemFunction.MakeCall("string", rsc, supplied);
                try
                {
                    return fn.TypeCheck(visitor, config.DefaultContextItemStaticInfo);
                }
                catch (XPathException err)
                {
                    throw err.MaybeWithLocation(supplied.GetLocation()).AsStaticError();
                }
            }


            // rule 3
            if (reqItemType.Equals(NumericType.GetInstance()) || reqItemType.Equals(BuiltInAtomicType.DOUBLE) && !Cardinality.AllowsMany(req.GetCardinality()) && !th.IsSubType(supplied.GetItemType(), BuiltInAtomicType.DOUBLE))
            {
                RetainedStaticContext rsc = supplied.GetRetainedStaticContext();
                Expression fn = SystemFunction.MakeCall("number", rsc, supplied);
                try
                {
                    return fn.TypeCheck(visitor, config.DefaultContextItemStaticInfo);
                }
                catch (XPathException err)
                {
                    throw err.MaybeWithLocation(supplied.GetLocation()).AsStaticError();
                }
            }

            return base.StaticTypeCheck(supplied, req, roleSupplier, visitor);
        }

        // rule 1
        // rule 2
        // rule 3
        public override Expression MakeArithmeticExpression(Expression lhs, int @operator, Expression rhs)
        {
            return new ArithmeticExpression10(lhs, @operator, rhs);
        }

        public override Expression MakeGeneralComparison(Expression lhs, int @operator, Expression rhs)
        {
            return new GeneralComparison10(lhs, @operator, rhs);
        }

        public override Expression ProcessValueOf(Expression select, Configuration config)
        {
            TypeHierarchy th = config.GetTypeHierarchy();
            if (!select.GetItemType().IsPlainType())
            {
                select = Atomizer.MakeAtomizer(select, null);
            }

            if (Cardinality.AllowsMany(select.GetCardinality()))
            {
                select = FirstItemExpression.MakeFirstItemExpression(select);
            }

            if (!th.IsSubType(select.GetItemType(), BuiltInAtomicType.STRING))
            {
                select = new AtomicSequenceConverter(select, BuiltInAtomicType.STRING);
                ((AtomicSequenceConverter)select).AllocateConverterStatically(config, false);
            }

            return select;
        }
    }
}
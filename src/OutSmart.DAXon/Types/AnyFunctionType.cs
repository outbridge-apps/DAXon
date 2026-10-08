////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Types
{
    // IItemTypeWithSequenceTypeCache: the ItemType interface has no GetUType/GetGenre members, so the
    // extension shim (DAXonItemTypeUTypeExt) dispatches through this interface and falls back to
    // UType.VOID / Genre.ANY otherwise. Function types didn't implement it, so any interface-typed
    // GetUType() returned VOID -> Types.GetCommonSuperType(map, array) unioned VOID|VOID -> ErrorType ->
    // a filter predicate over a mixed function-genre sequence got an ErrorType context item and the
    // optimizer folded position()/last() into ErrorExpression (typeswitch-118/119).
    public class AnyFunctionType : IFunctionItemType, IItemTypeWithSequenceTypeCache
    {
        public static readonly AnyFunctionType ANY_FUNCTION = new AnyFunctionType();

        // IItemTypeWithSequenceTypeCache lazies, same pattern as NodeTest.
        private SequenceType _one;
        private SequenceType _zeroOrOne;
        private SequenceType _oneOrMore;
        private SequenceType _zeroOrMore;

        public virtual double DefaultPriority => -0.5;

        public virtual string BasicAlphaCode => "F";

        public virtual SequenceType[] ArgumentTypes => null;

        public virtual AnnotationList AnnotationAssertions => AnnotationList.EMPTY;

        public int PrimitiveType => Type.FUNCTION;

        public virtual SequenceType ResultType => SequenceType.ANY_SEQUENCE;
        public static AnyFunctionType GetInstance()
        {
            return ANY_FUNCTION;
        }

        public virtual UType GetUType()
        {
            return UType.FUNCTION;
        }

        public virtual bool IsAtomicType()
        {
            return false;
        }

        public virtual bool IsPlainType()
        {
            return false;
        }

        public virtual bool IsMapType()
        {
            return false;
        }

        public virtual bool IsArrayType()
        {
            return false;
        }

        public virtual bool Matches(IItem item, TypeHierarchy th)
        {
            return item is IFunctionItem;
        }

        public ItemType GetPrimitiveItemType()
        {
            return ANY_FUNCTION;
        }

        public override string ToString()
        {
            return "function(*)";
        }

        public virtual IPlainType GetAtomizedItemType()
        {

            // Bug 6253. Some instances of function(*) can be atomized, so returning null is wrong.
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        public virtual bool IsAtomizable(TypeHierarchy th)
        {
            return true; // arrays can be atomized
        }

        public virtual Affinity Relationship(IFunctionItemType other, TypeHierarchy th)
        {
            if (other == this)
            {
                return Affinity.SAME_TYPE;
            }
            else
            {
                return Affinity.SUBSUMES;
            }
        }

        public virtual Expression MakeFunctionSequenceCoercer(Expression exp, Func<RoleDiagnostic> role, bool allow40)
        {
            return new ItemChecker(exp, this, role);
        }

        // Upstream FunctionItemType default; MapType/ArrayItemType/RecordTest override to MAP/ARRAY.
        // Was a throwing stub that the subclasses HID (no override), so interface-typed GetGenre never
        // reached them.
        public virtual Genre GetGenre() => Genre.FUNCTION;
        public virtual SequenceType One()
        {
            if (_one == null)
            {
                _one = new SequenceType(this, StaticProperty.EXACTLY_ONE);
            }
            return _one;
        }
        public virtual SequenceType ZeroOrOne()
        {
            if (_zeroOrOne == null)
            {
                _zeroOrOne = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_ONE);
            }
            return _zeroOrOne;
        }
        public virtual SequenceType OneOrMore()
        {
            if (_oneOrMore == null)
            {
                _oneOrMore = new SequenceType(this, StaticProperty.ALLOWS_ONE_OR_MORE);
            }
            return _oneOrMore;
        }
        public virtual SequenceType ZeroOrMore()
        {
            if (_zeroOrMore == null)
            {
                _zeroOrMore = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_MORE);
            }
            return _zeroOrMore;
        }
    }
}
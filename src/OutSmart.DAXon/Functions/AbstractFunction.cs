////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Abstract superclass (and factory class) for implementations of Function
    /// </summary>
    public abstract class AbstractFunction : IFunctionItem
    {
        public virtual OperandRole[] OperandRoles
        {
            get
            {
                OperandRole[] roles = new OperandRole[GetArity()];
                ArrayTools.Fill(roles, new OperandRole(0, OperandUsage.NAVIGATION));
                return roles;
            }
        }

        public virtual UnicodeString UnicodeStringValue
        {
            get
            {
                throw new UncheckedXPathException(new XPathException("The string value of a function is not defined", "FOTY0014"));
            }
        }
        public abstract IFunctionItemType FunctionItemType { get; }
        public abstract string Description { get; }

        public virtual IAtomicSequence Atomize()
        {
            throw new XPathException("Function items (other than arrays) cannot be atomized", "FOTY0013");
        }

        public virtual bool IsArray()
        {
            return false;
        }

        public virtual bool IsMap()
        {
            return false;
        }

        public virtual AnnotationList GetAnnotations()
        {
            return AnnotationList.EMPTY;
        }

        public virtual bool EffectiveBooleanValue()
        {
            throw new XPathException("A function has no effective boolean value", "XPTY0004");
        }

        public virtual void Simplify()
        {
        }

        public virtual void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
        }

        public virtual IXPathContext MakeNewContext(IXPathContext callingContext, IContextOriginator originator)
        {
            return callingContext;
        }

        public virtual bool DeepEquals(IFunctionItem other, IXPathContext context, IAtomicComparer comparer, int flags)
        {
            throw new XPathException("Argument to deep-equal() contains a function item", "FOTY0015");
        }

        public virtual bool DeepEqual40(IFunctionItem other, IXPathContext context, DeepEqual.DeepEqualOptions options)
        {
            if (options.falseOnError)
            {
                return false;
            }

            throw new XPathException("Argument to deep-equal() contains a function item", "FOTY0015");
        }

        public virtual void Export(ExpressionPresenter @out)
        {
            throw new NotSupportedException("export() not implemented for " + this.GetType());
        }

        public virtual bool IsTrustedResultType()
        {
            return false;
        }

        public virtual string ToShortString()
        {

            // Need to disambiguate multiple inheritance candidates here
            return Description;
        }
        public abstract StructuredQName GetFunctionName();
        public abstract int GetArity();
        // Every function item's genre is FUNCTION (upstream Function.getGenre default); ArrayItem/MapItem
        // override to ARRAY/MAP. Was a throwing stub -> a plain function reached through a lookup expression
        // (LookupExpression querying the base item's genre) NRE'd (prod-Lookup / prod-UnaryLookup).
        public virtual Genre GetGenre() => Genre.FUNCTION;
        public abstract ISequence Call(IXPathContext arg0, ISequence[] arg1);
        // A function item is a single item; iterating it yields a singleton over itself. The prior stub
        // threw (and IItem.Iterate below returned null), so a function reached as a sequence - e.g. a
        // dynamic call on a looked-up function, $map('k')(args) - failed.
        public virtual ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        public virtual IItem ItemAt(int arg0) => arg0 == 0 ? this : null;
        public virtual IItem Head() => this;
        // A function item is a singleton sequence: the standard single-item subsequence rule.
        public virtual IGroundedValue Subsequence(int start, int length)
        {
            return start <= 0 && (long)start + length > 0 ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance();
        }
        public virtual int GetLength() => 1;
        // Mirrors UnicodeStringValue above: functions have no string value (FOTY0014).
        public virtual string GetStringValue()
        {
            throw new UncheckedXPathException(new XPathException("The string value of a function is not defined", "FOTY0014"));
        }
        SingletonIterator IItem.Iterate() => (SingletonIterator)SingletonIterator.MakeIterator(this);

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual bool IsSequenceVariadic() => false;
        public virtual IGroundedValue Reduce() => this;
        public virtual bool IsStreamed() => false;
        public virtual IGroundedValue Materialize() => this;
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public virtual bool ContainsNode(NodeInfo sought) => false;
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this;
    }
}


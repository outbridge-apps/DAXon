////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the min() and max() functions, with the collation argument already known.
    /// </summary>
    internal abstract class Minimax : CollatingFunctionFixed
    {
        private IPlainType argumentType = BuiltInAtomicType.ANY_ATOMIC;
        private bool ignoreNaN;

        public override string StreamerName => "Minimax";
        public abstract bool IsMaxFunction();
        public virtual void SetIgnoreNaN(bool ignore)
        {
            ignoreNaN = ignore;
        }

        /// <summary>
        /// Static analysis: preallocate a comparer if possible
        /// </summary>
        public override void SupplyTypeInformation(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, Expression[] arguments)
        {
            ItemType type = arguments[0].GetItemType();
            argumentType = type.GetAtomizedItemType() as IPlainType ?? BuiltInAtomicType.ANY_ATOMIC;
            if (argumentType is IAtomicType)
            {
                if (argumentType == BuiltInAtomicType.UNTYPED_ATOMIC)
                {
                    argumentType = BuiltInAtomicType.DOUBLE;
                }

                PreAllocateComparer((IAtomicType)argumentType, (IAtomicType)argumentType, visitor.StaticContext);
            }
        }

        /// <summary>
        /// Static analysis: preallocate a comparer if possible
        /// </summary>
        public override ItemType GetResultItemType(Expression[] args)
        {
            TypeHierarchy th = GetRetainedStaticContext().GetConfiguration().GetTypeHierarchy();
            ItemType @base = Atomizer.GetAtomizedItemType(args[0], false, th);
            if (@base.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                @base = BuiltInAtomicType.DOUBLE;
            }

            return @base.GetPrimitiveItemType();
        }

        public override int GetCardinality(Expression[] arguments)
        {
            if (!Cardinality.AllowsZero(arguments[0].GetCardinality()))
            {
                return StaticProperty.EXACTLY_ONE;
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {

            // test for a singleton: this often happens after (A<B) is rewritten as (min(A) lt max(B))
            int card = arguments[0].GetCardinality();
            if (!Cardinality.AllowsMany(card))
            {
                ItemType it = arguments[0].GetItemType().GetPrimitiveItemType();
                if (it is BuiltInAtomicType && ((BuiltInAtomicType)it).IsOrdered(false))
                {
                    TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
                    if (th.Relationship(it, BuiltInAtomicType.UNTYPED_ATOMIC) != Affinity.DISJOINT)
                    {
                        return UntypedSequenceConverter.MakeUntypedSequenceConverter(visitor.GetConfiguration(), arguments[0], BuiltInAtomicType.DOUBLE).TypeCheck(visitor, contextInfo);
                    }
                    else
                    {
                        return arguments[0];
                    }
                }
            }

            if (arguments[0] is RangeExpression)
            {

                // typically the min/max is the start/end of the range. But we need to be careful about handling
                // an empty sequence (A to B where A > B)
                if (IsMaxFunction())
                {
                    Expression start = ((RangeExpression)arguments[0]).StartExpression;
                    Expression end = ((RangeExpression)arguments[0]).EndExpression;
                    if (start is Literal && end is Literal)
                    {
                        return end;
                    }

                    return new LastItemExpression(arguments[0]);
                }
                else
                {
                    return FirstItemExpression.MakeFirstItemExpression(arguments[0]);
                }
            }

            return null;
        }

        public override IAtomicComparer GetAtomicComparer(IXPathContext context)
        {
            IAtomicComparer comparer = PreAllocatedAtomicComparer;
            if (comparer != null)
            {
                return comparer;
            }

            IPlainType type = argumentType.GetPrimitiveItemType();
            if (type.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                type = BuiltInAtomicType.DOUBLE;
            }

            BuiltInAtomicType prim = (BuiltInAtomicType)type;
            return GenericAtomicComparer.MakeAtomicComparer(prim, prim, StringCollator, context);
        }

        public static AtomicValue MinimaxFn(ISequenceIterator iter, bool isMaxFunction, IAtomicComparer atomicComparer, bool ignoreNaN, IXPathContext context)
        {
            ConversionRules rules = context.GetConfiguration().GetConversionRules();
            StringToDouble converter = context.GetConfiguration().GetConversionRules().StringToDoubleConverter;
            bool foundDouble = false;
            bool foundFloat = false;
            bool foundNaN = false;
            bool foundString = false;

            // For the max function, reverse the collator
            if (isMaxFunction)
            {
                atomicComparer = new DescendingComparer(atomicComparer);
            }

            atomicComparer = atomicComparer.ProvideContext(context);

            // Process the sequence, retaining the min (or max) so far. This will be an actual value found
            // in the sequence. At the same time, remember if a double and/or float has been encountered
            // anywhere in the sequence, and if so, convert the min/max to double/float at the end. This is
            // done to avoid problems if a decimal is converted first to a float and then to a double.
            // Get the first value in the sequence, ignoring any NaN values if we are ignoring NaN values
            AtomicValue min;
            AtomicValue prim;
            while (true)
            {

                // loop only repeats if first item is NaN
                min = (AtomicValue)iter.Next();
                if (min == null)
                {
                    return null;
                }

                prim = min;
                if (min.IsUntypedAtomic())
                {
                    try
                    {
                        min = new DoubleValue(converter.StringToNumber(min.UnicodeStringValue));
                        prim = min;
                        foundDouble = true;
                    }
                    catch (FormatException)
                    {
                        throw new XPathException("Failure converting " + Err.Wrap(min.UnicodeStringValue) + " to a number").WithErrorCode("FORG0001").WithXPathContext(context);
                    }
                }
                else
                {
                    if (prim is DoubleValue)
                    {
                        foundDouble = true;
                    }
                    else if (prim is FloatValue)
                    {
                        foundFloat = true;
                    }
                    else if (prim is StringValue && !(prim is AnyURIValue))
                    {
                        foundString = true;
                    }
                }

                if (prim.IsNaN())
                {

                    // if there's a NaN in the sequence, return NaN, unless ignoreNaN is set
                    if (ignoreNaN)
                    {
                    }
                    else if (prim is DoubleValue)
                    {
                        return min; // return double NaN
                    }
                    else
                    {

                        // we can't ignore a float NaN, because we might need to promote it to a double NaN
                        foundNaN = true;
                        min = FloatValue.NaN;
                        break;
                    }
                }
                else
                {
                    if (!prim.PrimitiveType.IsOrdered(false))
                    {
                        throw new XPathException("Type " + prim.PrimitiveType + " is not an ordered type").WithErrorCode("FORG0006").AsTypeError().WithXPathContext(context);
                    }

                    break; // process the rest of the sequence
                }
            }

            while (true)
            {
                AtomicValue test = (AtomicValue)iter.Next();
                if (test == null)
                {
                    break;
                }

                AtomicValue test2 = test;
                prim = test2;
                if (test.IsUntypedAtomic())
                {
                    try
                    {
                        test2 = new DoubleValue(converter.StringToNumber(test.UnicodeStringValue));
                        if (foundNaN)
                        {
                            return DoubleValue.NaN;
                        }

                        prim = test2;
                        foundDouble = true;
                    }
                    catch (FormatException)
                    {
                        throw new XPathException("Failure converting " + Err.Wrap(test.GetStringValue()) + " to a number").WithErrorCode("FORG0001").WithXPathContext(context);
                    }
                }
                else
                {
                    if (prim is DoubleValue)
                    {
                        if (foundNaN)
                        {
                            return DoubleValue.NaN;
                        }

                        foundDouble = true;
                    }
                    else if (prim is FloatValue)
                    {
                        foundFloat = true;
                    }
                    else if (prim is StringValue && !(prim is AnyURIValue))
                    {
                        foundString = true;
                    }
                }

                if (prim.IsNaN())
                {

                    // if there's a double NaN in the sequence, return NaN, unless ignoreNaN is set
                    if (ignoreNaN)
                    {
                    }
                    else if (foundDouble)
                    {
                        return DoubleValue.NaN;
                    }
                    else
                    {

                        // can't return float NaN until we know whether to promote it
                        foundNaN = true;
                    }
                }
                else
                {
                    try
                    {
                        if (atomicComparer.CompareAtomicValues(prim, min) < 0)
                        {
                            min = test2;
                        }
                    }
                    catch (InvalidCastException)
                    {
                        if (min.GetItemType() == test2.GetItemType())
                        {

                            // internal error
                            throw;
                        }
                        else
                        {
                            throw new XPathException("Cannot compare " + min.GetItemType() + " with " + test2.GetItemType()).WithErrorCode("FORG0006").AsTypeError().WithXPathContext(context);
                        }
                    }
                }
            }

            if (foundNaN)
            {
                return FloatValue.NaN;
            }

            if (foundDouble)
            {
                if (!(min is DoubleValue))
                {
                    min = (AtomicValue)Converter.Convert(min, BuiltInAtomicType.DOUBLE, rules);
                }
            }
            else if (foundFloat)
            {
                if (!(min is FloatValue))
                {
                    min = (AtomicValue)Converter.Convert(min, BuiltInAtomicType.FLOAT, rules);
                }
            }
            else if (min is AnyURIValue && foundString)
            {
                min = (AtomicValue)Converter.Convert(min, BuiltInAtomicType.STRING, rules);
            }

            return min;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ItemOrEmpty(MinimaxFn(arguments[0].Iterate(), IsMaxFunction(), GetAtomicComparer(context), ignoreNaN, context));
        }

        public override void ExportAttributes(ExpressionPresenter @out)
        {
            base.ExportAttributes(@out);
            if (ignoreNaN)
            {
                @out.EmitAttribute("flags", "i");
            }
        }

        public override void ImportAttributes(Properties attributes)
        {
            base.ImportAttributes(attributes);
            string flags = attributes.GetProperty("flags");
            if (flags != null && flags.Contains("i"))
            {
                SetIgnoreNaN(true);
            }
        }

        /// <summary>
        /// Concrete subclass to define the fn:min() function
        /// </summary>
        internal sealed class Min : Minimax
        {
            public override bool IsMaxFunction()
            {
                return false;
            }
        }

        /// <summary>
        /// Concrete subclass to define the fn:max() function
        /// </summary>
        internal sealed class Max : Minimax
        {
            public override bool IsMaxFunction()
            {
                return true;
            }
        }
    }
}

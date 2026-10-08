////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Function to test castability to a union type
    /// </summary>
    internal sealed class UnionCastableFunction : UnionConstructorFunction
    {

        public override IFunctionItemType FunctionItemType => new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE }, SequenceType.SINGLE_BOOLEAN);
        public UnionCastableFunction(IUnionType targetType, INamespaceResolver resolver, bool allowEmpty) : base(targetType, resolver, allowEmpty)
        {
        }

        public override StructuredQName GetFunctionName()
        {
            return null;
        }

        private bool EffectiveBooleanValue(ISequenceIterator iter, IXPathContext context)
        {

            // This method does its own atomization so that it can distinguish between atomization
            // failures and casting failures: a node gives its typed value and an array its members; a
            // map or a function has no atoms and fails the expression (FOTY0013), as for an atomic type
            int count = 0;
            for (IItem item; (item = iter.Next()) != null;)
            {
                IAtomicSequence atomizedValue = item.Atomize();
                int length = atomizedValue.GetLength();
                count += length;
                if (count > 1)
                {
                    return false;
                }

                if (length != 0)
                {
                    AtomicValue av = atomizedValue.Head();
                    if (!Castable(av, context))
                    {
                        return false;
                    }
                }
            }

            return count != 0 || allowEmpty;
        }

        private bool Castable(AtomicValue value, IXPathContext context)
        {
            try
            {
                Cast(value, context);
                return true;
            }
            catch (XPathException)
            {
                return false;
            }
        }

        // Java returns BooleanValue via covariant return; net472 must keep the base signature — the
        // previous `public BooleanValue Call` HID UnionConstructorFunction.Call, so `castable as
        // <union>` executed the base CAST (returning the value / throwing FORG0001) instead of the
        // boolean check (xs-numeric castable tests).
        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            bool value = EffectiveBooleanValue(args[0].Iterate(), context);
            return BooleanValue.Get(value);
        }
    }
}

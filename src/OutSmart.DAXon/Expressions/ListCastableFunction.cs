////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// A function item representing a castability test for a list type
    /// </summary>
    internal sealed class ListCastableFunction : ListConstructorFunction
    {

        public override IFunctionItemType FunctionItemType => new SpecificFunctionType(new SequenceType[] { SequenceType.ANY_SEQUENCE }, SequenceType.SINGLE_BOOLEAN);
        public ListCastableFunction(IListType targetType, INamespaceResolver resolver, bool allowEmpty) : base(targetType, resolver, allowEmpty)
        {
        }

        public override StructuredQName GetFunctionName()
        {
            return null;
        }

        // Same covariant-return hide as UnionCastableFunction: the previous `public BooleanValue Call`
        // hid ListConstructorFunction.Call, so `castable as <list-type>` executed the CAST instead of
        // returning a boolean.
        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            ISequenceIterator iter = args[0].Iterate();
            AtomicValue val = (AtomicValue)iter.Next();
            if (val == null)
            {
                return BooleanValue.Get(allowEmpty);
            }

            if (iter.Next() != null)
            {
                return BooleanValue.FALSE;
            }

            if (!(val is StringValue) || val is AnyURIValue)
            {
                return BooleanValue.FALSE;
            }

            ConversionRules rules = context.GetConfiguration().GetConversionRules();
            UnicodeString cs = val.UnicodeStringValue;
            ValidationFailure failure = targetType.ValidateContent(cs, nsResolver, rules);
            return BooleanValue.Get(failure == null);
        }
    }
}

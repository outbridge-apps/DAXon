////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ItemFilter : IItemMappingFunction
    {

        private readonly ILambda lambda;
        private ItemFilter(ILambda lambda)
        {
            this.lambda = lambda;
        }

        public static ItemFilter Of(ILambda lambda)
        {
            return new ItemFilter(lambda);
        }

        public IItem MapItem(IItem item)
        {
            if (lambda(item))
            {
                return item;
            }
            else
            {
                return null;
            }
        }
        // ILambda interface->delegate for lambda assignability.
        internal delegate bool ILambda(IItem item);
    }
}
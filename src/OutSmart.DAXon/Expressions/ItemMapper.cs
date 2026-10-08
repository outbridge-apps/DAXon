////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ItemMapper : IItemMappingFunction
    {

        private readonly ILambda lambda;
        private ItemMapper(ILambda lambda)
        {
            this.lambda = lambda;
        }

        public static ItemMapper Of(ILambda lambda)
        {
            return new ItemMapper(lambda);
        }

        public IItem MapItem(IItem item)
        {
            return lambda(item);
        }
        // ILambda interface->delegate for lambda assignability.
        internal delegate IItem ILambda(IItem item);
    }
}
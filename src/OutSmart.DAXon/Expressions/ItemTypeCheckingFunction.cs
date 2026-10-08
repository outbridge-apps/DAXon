////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ItemTypeCheckingFunction : IItemMappingFunction
    {
        private readonly Types.ItemType requiredItemType;
        private readonly Func<RoleDiagnostic> roleSupplier;
        private readonly ILocation location;
        private Configuration config = null;
        public ItemTypeCheckingFunction(Types.ItemType requiredItemType, Func<RoleDiagnostic> roleSupplier, ILocation locator, Configuration config)
        {
            this.requiredItemType = requiredItemType;
            this.roleSupplier = roleSupplier;
            this.location = locator;
            this.config = config;
        }

        public IItem MapItem(IItem item)
        {
            TestConformance(item, config);
            return item;
        }

        // Dispatch to the concrete item type's real Matches. IPlainType and NodeTest check atomic/node
        // conformance; MAP and ARRAY required types validate their member types (MapType/ArrayItemType.Matches).
        // The old code returned a permissive `true` for those, so a function-argument / treat check against
        // array(T) / map(...) never rejected a non-conforming value (prod-ArrayTest 075/079/081/084/087 — passing
        // ['a',0] where array(xs:string) is required should be XPTY0004). We deliberately do NOT do this for a
        // required FUNCTION type: a function argument to a HOF is *coerced* (FunctionSequenceCoercer wraps it to
        // adapt the signature), not rejected, and the port's function-signature Matches over-rejects valid
        // functions here — enforcing it broke fn-filter/fold/for-each/function-lookup. Keep them permissive.
        private static bool RealMatches(Types.ItemType requiredItemType, IItem item, TypeHierarchy th)
        {
            if (requiredItemType is OutSmart.DAXon.Types.IPlainType pt)
            {
                return pt.Matches(item, th);
            }
            if (requiredItemType is OutSmart.DAXon.Patterns.NodeTest nt)
            {
                return nt.Matches(item, th);
            }
            if (requiredItemType is OutSmart.DAXon.Values.Maps.MapType || requiredItemType is OutSmart.DAXon.Values.Arrays.ArrayItemType)
            {
                return requiredItemType.Matches(item, th);
            }
            // function-item / any / unknown kinds: keep the permissive behaviour (functions are coerced, not
            // checked, and other kinds we can't reliably evaluate here).
            return true;
        }

        private void TestConformance(IItem item, Configuration config)
        {
            TypeHierarchy th = config.GetTypeHierarchy();
            if (RealMatches(requiredItemType, item, th))
            {
            }
            else if (requiredItemType.GetUType().Subsumes(UType.STRING) && BuiltInAtomicType.ANY_URI.Matches(item, th))
            {
            }
            else
            {
                RoleDiagnostic role = roleSupplier();
                string message = role.ComposeErrorMessage(requiredItemType, item, th);
                string errorCode = role.ErrorCode;
                if (errorCode == "XPDY0050")
                {

                    // error in "treat as" assertion
                    XPathException te = new XPathException(message, errorCode);
                    te.SetLocator(location);
                    te.SetIsTypeError(false);
                    throw te;
                }
                else
                {
                    XPathException te = new XPathException(message, errorCode);
                    te.SetLocator(location);
                    te.SetIsTypeError(true);
                    throw te;
                }
            }
        }
    }
}

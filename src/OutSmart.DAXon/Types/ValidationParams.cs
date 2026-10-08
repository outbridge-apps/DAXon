////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Types
{
    public class ValidationParams : Dictionary<StructuredQName, ISequence>
    {
        public ValidationParams() : base(20)
        {
        }

        public static void SetValidationParams(Dictionary<StructuredQName, XPathVariable> declaredParams, ValidationParams actualParams, XPathDynamicContext context)
        {
            foreach (StructuredQName p in declaredParams.Keys)
            {
                XPathVariable var = declaredParams.GetOrDefault(p);
                ISequence paramValue = ((Dictionary<StructuredQName, ISequence>)actualParams).GetOrDefault(p);
                if (paramValue != null)
                {
                    context.SetVariable(var, paramValue);
                }
                else
                {
                    context.SetVariable(var, var.DefaultValue);
                }
            }
        }
    }
}
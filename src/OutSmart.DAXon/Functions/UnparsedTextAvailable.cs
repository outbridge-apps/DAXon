////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    internal sealed class UnparsedTextAvailable : UnparsedTextFunction, ICallable
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue hrefVal = (StringValue)arguments[0].Head();
            if (hrefVal == null)
            {
                return BooleanValue.FALSE;
            }

            string encoding;
            if (GetArity() == 2)
            {
                IItem enc = arguments[1].Head();
                encoding = enc == null ? null : enc.GetStringValue();
            }
            else
            {
                encoding = null;
            }

            return BooleanValue.Get(EvalUnparsedTextAvailable(hrefVal, encoding, context));
        }

        public bool EvalUnparsedTextAvailable(StringValue hrefVal, string encoding, IXPathContext context)
        {
            try
            {
                UnparsedText.EvalUnparsedText(hrefVal, StaticBaseUriString, encoding, context);
                return true;
            }
            catch (XPathException err)
            {
                return false;
            }
        }
        ISequence ICallable.Call(IXPathContext arg0, ISequence[] arg1) => Call(arg0, arg1);
    }
}


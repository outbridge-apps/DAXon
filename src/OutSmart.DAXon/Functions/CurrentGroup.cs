////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XSLT function current-group()
    /// </summary>
    internal sealed class CurrentGroup : ContextAccessorFunction
    {
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            return new CurrentGroupCall();
        }

        public override IFunctionItem BindContext(IXPathContext context)
        {
            if (GetRetainedStaticContext().GetPackageData().HostLanguageVersion < 40)
            {
                throw new XPathException("Dynamic call on current-group() fails (the current group is absent)", "XTDE1061");
            }

            IGroupIterator gi = context.GetCurrentGroupIterator();
            if (gi == null)
            {
                throw new XPathException("There is no current group", "XTDE1061");
            }

            IGroundedValue group = gi.CurrentGroup();
            ConstantFunction fn = new ConstantFunction(group);
            fn.Details = Details;
            fn.SetRetainedStaticContext(GetRetainedStaticContext());
            return fn;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            throw new XPathException("Dynamic call on current-group() fails (the current group is absent)", "XTDE1061");
        }
    }
}
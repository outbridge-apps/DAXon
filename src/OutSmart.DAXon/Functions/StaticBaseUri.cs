////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath function static-base-uri()
    /// </summary>
    internal sealed class StaticBaseUri : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            return new AnyURIValue(GetRetainedStaticContext().StaticBaseUriString);
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            PackageData pd = GetRetainedStaticContext().GetPackageData();
            if (pd.IsRelocatable())
            {
                return base.MakeFunctionCall(arguments);
            }
            else
            {
                return Literal.MakeLiteral(new AnyURIValue(GetRetainedStaticContext().StaticBaseUriString));
            }
        }
    }
}

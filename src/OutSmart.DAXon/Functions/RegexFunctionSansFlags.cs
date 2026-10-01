////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
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
    internal sealed class RegexFunctionSansFlags : SystemFunction
    {
        private SystemFunction AddFlagsArgument()
        {
            RetainedStaticContext rsc = GetRetainedStaticContext();
            Configuration config = rsc.GetConfiguration();
            SystemFunction @fixed = config.MakeSystemFunction(GetFunctionName().GetLocalPart(), GetArity() + 1, rsc.GetPackageData().HostLanguageVersion);
            @fixed.SetRetainedStaticContext(rsc);
            return @fixed;
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            SystemFunction withFlags = AddFlagsArgument();
            Expression[] newArgs = new Expression[arguments.Length + 1];
            Array.Copy(arguments, 0, newArgs, 0, arguments.Length);
            newArgs[arguments.Length] = new StringLiteral("");
            return withFlags.MakeFunctionCall(newArgs);
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            SystemFunction withFlags = AddFlagsArgument();
            ISequence[] newArgs = new ISequence[args.Length + 1];
            Array.Copy(args, 0, newArgs, 0, args.Length);
            newArgs[args.Length] = StringValue.EMPTY_STRING;
            return withFlags.Call(context, newArgs);
        }
    }
}
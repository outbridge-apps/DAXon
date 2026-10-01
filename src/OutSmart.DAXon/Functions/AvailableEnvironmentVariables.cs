////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    internal sealed class AvailableEnvironmentVariables : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IEnvironmentVariableResolver resolver = context.GetConfiguration().GetConfigurationProperty(Feature<IEnvironmentVariableResolver>.ENVIRONMENT_VARIABLE_RESOLVER);
            IList<IItem> myList = new List<IItem>();
            if (context.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
            {
                // Only the names the policy would let environment-variable() read.
                bool gated = resolver is StandardEnvironmentVariableResolver;
                foreach (string s in resolver.GetAvailableEnvironmentVariables())
                {
                    if (!gated || OutSmart.DAXon.Internal.ResourceGate.PermitsEnvironment(context.GetConfiguration(), s))
                    {
                        myList.Add(new StringValue(s));
                    }
                }
            }

            return new SequenceExtent.Of<IItem>(myList);
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly AvailableEnvironmentVariables parent;
            public AnonymousSystemFunctionCall(AvailableEnvironmentVariables parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            // Suppress early evaluation
            public override Expression PreEvaluate(ExpressionVisitor visitor)
            {
                return this;
            }
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System;
using System.Security;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath 3.0 fn:environment-variable() function
    /// </summary>
    internal sealed class EnvironmentVariable : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue value = GetVariable((StringValue)arguments[0].Head(), context);
            if (value != null)
            {
                return value;
            }
            else
            {
                return EmptySequence.GetInstance();
            }
        }

        private static StringValue GetVariable(StringValue environVar, IXPathContext context)
        {
            IEnvironmentVariableResolver resolver = context.GetConfiguration().GetConfigurationProperty(Feature<IEnvironmentVariableResolver>.ENVIRONMENT_VARIABLE_RESOLVER);
            string environVarName = environVar.GetStringValue();
            string environValue = "";
            if (context.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
            {
                // A host-installed resolver is the host's own policy; the built-in one reads the process environment.
                if (resolver is StandardEnvironmentVariableResolver && !OutSmart.DAXon.Internal.ResourceGate.PermitsEnvironment(context.GetConfiguration(), environVarName))
                {
                    return null;
                }

                try
                {
                    environValue = resolver.GetEnvironmentVariable(environVarName);
                    if (environValue == null)
                    {
                        return null;
                    }
                }
                catch (SecurityException)
                {
                }
                catch (NullReferenceException)
                {
                }
            }

            return (new StringValue(environValue));
        }
    }
}

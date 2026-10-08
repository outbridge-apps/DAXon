////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class GlobalParameterSet
    {
        private readonly Dictionary<StructuredQName, IGroundedValue> @params = new Dictionary<StructuredQName, IGroundedValue>(10);

        // PHASE7_INDEXER_GPS
        public IGroundedValue this[StructuredQName key] { get { return Get(key); } set { Put(key, value); } }

        public virtual ICollection<StructuredQName> Keys => @params.Keys;

        public virtual int NumberOfKeys => @params.Count;
        /// <summary>
        /// Create an empty parameter set
        /// </summary>
        public GlobalParameterSet()
        {
        }

        public GlobalParameterSet(GlobalParameterSet parameterSet)
        {

            // Type parameters needed for C#
            this.@params = new Dictionary<StructuredQName, IGroundedValue>(parameterSet.@params);
        }

        public virtual void Put(StructuredQName qName, IGroundedValue value)
        {
            if (value == null)
            {
                @params.Remove(qName);
            }
            else
            {
                @params.PutAndGetPrevious(qName, value);
            }
        }

        public virtual IGroundedValue Get(StructuredQName qName)
        {
            return @params.GetOrDefault(qName);
        }

        public virtual bool ContainsKey(StructuredQName qName)
        {
            return @params.ContainsKey(qName);
        }

        public virtual IGroundedValue ConvertParameterValue(StructuredQName qName, SequenceType requiredType, bool convert, IXPathContext context)
        {
            ISequence val = Get(qName);
            if (val == null)
            {
                return null;
            }

            if (requiredType != null)
            {
                if (convert)
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, qName.DisplayName, -1);
                    Configuration config = context.GetConfiguration();
                    val = config.GetTypeHierarchy().ApplyFunctionConversionRules(val, requiredType, role, Loc.NONE);
                }
                else
                {
                    XPathException err = TypeChecker.TestConformance(val, requiredType, context);
                    if (err != null)
                    {
                        throw err;
                    }
                }
            }

            return val.Materialize();
        }

        /// <summary>
        /// Clear all values
        /// </summary>
        public virtual void Clear()
        {
            @params.Clear();
        }
    }
}

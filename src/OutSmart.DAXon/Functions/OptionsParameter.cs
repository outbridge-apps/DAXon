////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    public class OptionsParameter
    {
        private readonly Dictionary<string, SequenceType> allowedOptions = new Dictionary<string, SequenceType>(8);
        private readonly Dictionary<string, IGroundedValue> defaultValues = new Dictionary<string, IGroundedValue>(8);
        private readonly HashSet<string> requiredOptions = new HashSet<string>();
        private readonly Dictionary<string, HashSet<string>> allowedValues = new Dictionary<string, HashSet<string>>(8);
        private string errorCodeForDisallowedValue;
        private string errorCodeForAbsentValue = "SXJE9999";
        private bool allowCastFromString = false;

        public virtual Dictionary<string, IGroundedValue> DefaultOptions => new Dictionary<string, IGroundedValue>(defaultValues);

        public virtual string ErrorCodeForAbsentValue
        {
            get => errorCodeForAbsentValue; set
            {
                this.errorCodeForAbsentValue = value;
            }
        }
        public OptionsParameter()
        {
        }

        public virtual void AddAllowedOption(string name, SequenceType type)
        {
            allowedOptions[name] = type;
        }

        public virtual void AddRequiredOption(string name, SequenceType type)
        {
            allowedOptions[name] = type;
            requiredOptions.Add(name);
        }

        public virtual void AddAllowedOption(string name, SequenceType type, IGroundedValue defaultValue)
        {
            allowedOptions[name] = type;
            if (defaultValue != null)
            {
                defaultValues[name] = defaultValue;
            }
        }

        public virtual void SetAllowedValues(string name, string errorCode, params string[] values)
        {
            HashSet<string> valueSet = new HashSet<string>(values.ToList());
            allowedValues[name] = valueSet;
            errorCodeForDisallowedValue = errorCode;
        }

        public virtual Dictionary<string, IGroundedValue> ProcessSuppliedOptions(MapItem supplied, IXPathContext context)
        {
            // Empty options map (e.g. every fn:deep-equal call without options): the result is exactly
            // the registered defaults — skip the full allowedOptions walk with its per-key
            // StringValue/QNameValue allocations and lookups. Fresh copy: callers may mutate.
            if (supplied != null && supplied.Size() == 0 && requiredOptions.Count == 0)
            {
                return new Dictionary<string, IGroundedValue>(defaultValues);
            }

            Dictionary<string, IGroundedValue> result = new Dictionary<string, IGroundedValue>();
            TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
            foreach (string req in requiredOptions)
            {
                if (supplied[new StringValue(req)] == null)
                {
                    throw new XPathException("No value supplied for required option: " + req, errorCodeForAbsentValue);
                }
            }

            foreach (KeyValuePair<string, SequenceType> allowed in allowedOptions)
            {
                string nominalKey = allowed.Key;
                AtomicValue actualKey;
                if (nominalKey.StartsWith("Q{", StringComparison.Ordinal))
                {
                    actualKey = new QNameValue(StructuredQName.FromEQName((nominalKey)), BuiltInAtomicType.QNAME);
                }
                else
                {
                    actualKey = new StringValue(nominalKey);
                }

                SequenceType required = allowed.Value;
                IGroundedValue actual = supplied[actualKey];
                if (actual != null)
                {
                    if (!required.Matches(actual, th))
                    {
                        bool ok = false;
                        if (actual is StringValue && allowCastFromString && required.PrimaryType is IAtomicType)
                        {
                            try
                            {
                                ConversionRules rules = context.GetConfiguration().GetConversionRules();
                                actual = (IGroundedValue)Converter.Convert((StringValue)actual, (IAtomicType)required.PrimaryType, rules);
                                ok = true;
                            }
                            catch (XPathException err)
                            {
                                ok = false;
                            }
                        }

                        if (!ok)
                        {
                            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.OPTION, nominalKey, 0, "XPTY0004");
                            actual = th.ApplyFunctionConversionRules(actual, required, role, Loc.NONE);
                        }
                    }

                    actual = actual.Materialize();
                    HashSet<string> permitted = allowedValues.GetOrDefault(nominalKey);
                    if (permitted != null)
                    {
                        if (!(actual is AtomicValue) || !permitted.Contains(((AtomicValue)actual).GetStringValue()))
                        {
                            StringBuilder message = new StringBuilder("Invalid option " + nominalKey + "=" + Err.DepictSequence(actual) + ". Valid values are:");
                            int i = 0;
                            foreach (string v in permitted)
                            {
                                message.Append(i++ == 0 ? " " : ", ").Append(v);
                            }

                            throw new XPathException(message.ToString(), errorCodeForDisallowedValue);
                        }
                    }

                    result[nominalKey] = actual;
                }
                else
                {
                    IGroundedValue def = defaultValues.TryGetValue(nominalKey, out var __def) ? __def : null;
                    if (def != null)
                    {
                        result[nominalKey] = def;
                    }
                }
            }

            return result;
        }

        public virtual bool IsAllowCastFromString()
        {
            return allowCastFromString;
        }

        public virtual void SetAllowCastFromString(bool allowCastFromString)
        {
            this.allowCastFromString = allowCastFromString;
        }
    }
}

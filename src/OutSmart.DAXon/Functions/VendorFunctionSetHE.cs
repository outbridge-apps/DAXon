////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// excluded stubs.cs -- VendorFunctionSetHE
//
// #88 (2026-06-19): SURGICAL augmentation. The full poc VendorFunctionSetHE.cs is <Compile Remove>'d
// because re-including it transitively pulls in excluded function classes (MapCreate / Doc_2 /
// MapUntypedContains / ...). The xsl:catch err: variables (err:code/description/...) only need the
// single `dynamic-error-info` vendor function (ExpressionContext compiles them to a
// dynamic-error-info(<name>) call). So register JUST that one function here + port its
// DynamicErrorInfoFn implementation, and inherit the working BuiltInFunctionSet.MakeFunction
// (registry lookup). Other vendor functions stay unregistered -- they were NIE stubs before, so no
// regression. DynamicErrorInfoFn body is ported verbatim from the real (excluded) nested class.

using System;
using OutSmart.DAXon.Internal;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Lib;

namespace OutSmart.DAXon.Functions
{
    internal static class VendorFunctionSetHE
    {
        private static readonly VendorFunctionSetHE_Inner _instance = new VendorFunctionSetHE_Inner();
        public static VendorFunctionSetHE_Inner GetInstance() => _instance;

        // Implements IFunctionLibrary via BuiltInFunctionSet for AddFunctionLibrary call sites.
        internal sealed class VendorFunctionSetHE_Inner : BuiltInFunctionSet
        {
            public VendorFunctionSetHE_Inner()
            {
                // #88: only dynamic-error-info (xsl:catch err: variables). Other vendor functions stay
                // unregistered (they were NIE stubs before -> no regression). MakeFunction is inherited
                // from BuiltInFunctionSet (registry lookup), so this resolves at compile + run.
                Register("dynamic-error-info", 1, (e) => e.Populate(DynamicErrorInfoFn.New(), AnyItemType.GetInstance(), STAR, FOCUS | LATE | SIDE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            }
        }

        // Ported verbatim from the (excluded) real VendorFunctionSetHE.DynamicErrorInfoFn.
        // Evaluates an xsl:catch error variable such as $err:code from the current caught exception.
        internal sealed class DynamicErrorInfoFn : SystemFunction
        {
            public static Func<DynamicErrorInfoFn> New() => () => new DynamicErrorInfoFn();

            public override int GetSpecialProperties(Expression[] arguments)
            {
                return 0; // treat as creative to avoid loop-lifting: test case try-catch-err-code-variable-14
            }

            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                string var = arguments[0].Head().GetStringValue();
                XPathException error = context.GetCurrentException();
                if (error == null)
                {
                    return EmptySequence.GetInstance();
                }

                ILocation locator = error.GetLocator();
                switch (var)
                {
                    case "code":
                        StructuredQName errorCodeQName = error.ErrorCodeQName;
                        if (errorCodeQName == null)
                        {
                            errorCodeQName = new StructuredQName("saxon", NamespaceUri.SAXON, "XXXX9999");
                        }

                        return new QNameValue(errorCodeQName, BuiltInAtomicType.QNAME);
                    case "description":
                        string s = error.Message;
                        if (error.InnerException != null)
                        {
                            s += "(" + error.InnerException.Message + ")"; // GetCause() -> BCL System.Exception (.Message, not .GetMessage())
                        }

                        return new StringValue(s);
                    case "value":
                        ISequence value = error.ErrorObject;
                        if (value == null)
                        {
                            return EmptySequence.GetInstance();
                        }
                        else
                        {
                            return value;
                        }

                    case "module":
                        string module = locator == null ? null : locator.GetSystemId();
                        if (module == null)
                        {
                            return EmptySequence.GetInstance();
                        }
                        else
                        {
                            return new StringValue(module);
                        }

                    case "line-number":
                        int line = locator == null ? -1 : locator.GetLineNumber();
                        if (line == -1)
                        {
                            return EmptySequence.GetInstance();
                        }
                        else
                        {
                            return new Int64Value(line);
                        }

                    case "column-number":

                        // Bug 4144
                        int column = -1;
                        if (locator == null)
                        {
                            return EmptySequence.GetInstance();
                        }
                        else if (locator is XPathParser.NestedLocation)
                        {
                            column = ((XPathParser.NestedLocation)locator).GetContainingLocation().GetColumnNumber();
                        }
                        else
                        {
                            column = locator.GetColumnNumber();
                        }

                        if (column == -1)
                        {
                            return EmptySequence.GetInstance();
                        }
                        else
                        {
                            return new Int64Value(column);
                        }

                    default:
                        return EmptySequence.GetInstance();
                }
            }
        }
    }
}

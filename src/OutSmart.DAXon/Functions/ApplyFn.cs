////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Functions
{
    internal sealed class ApplyFn : SystemFunction
    {
        private string dynamicFunctionCall;
        public ApplyFn()
        {
        }

        public override ItemType GetResultItemType(Expression[] args)
        {

            // IItem type of the result is the same as that of the supplied function
            ItemType fnType = args[0].GetItemType();
            if (fnType is MapType)
            {
                return ((MapType)fnType).ValueType.PrimaryType;
            }
            else if (fnType is ArrayItemType)
            {
                return ((ArrayItemType)fnType).MemberType.PrimaryType;
            }
            else if (fnType is IFunctionItemType)
            {
                return ((IFunctionItemType)fnType).ResultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            if (arguments.Length == 2 && arguments[1] is SquareArrayConstructor)
            {
                Expression target = arguments[0];
                if (target.GetItemType() is MapType)
                {

                    // Convert $map($key) to map:get($map, $key)
                    // This improves streamability analysis - see accumulator-053
                    return MakeGetCall(visitor, MapFunctionSet.GetInstance(31), contextInfo, arguments);
                }
                else if (target.GetItemType() is ArrayItemType)
                {

                    // Convert $array($key) to array:get($array, $key)
                    return MakeGetCall(visitor, ArrayFunctionSet.GetInstance(31), contextInfo, arguments);
                }
            }

            return null;
        }

        private Expression MakeGetCall(ExpressionVisitor visitor, BuiltInFunctionSet fnSet, ContextItemStaticInfo contextInfo, Expression[] arguments)
        {
            Expression target = arguments[0];
            Expression key = ((SquareArrayConstructor)arguments[1]).GetOperanda().GetOperand(0).GetChildExpression();
            Expression getter = fnSet.MakeFunction("get", 2).MakeFunctionCall(target, key);
            getter.SetRetainedStaticContext(target.GetRetainedStaticContext());

            // Use custom diagnostics for type errors on the argument of the call (bug 4772)
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(visitor.StaticContext.IsInBackwardsCompatibleMode());
            if (fnSet == MapFunctionSet.GetInstance(31))
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "key value supplied when calling a map as a function", 0);
                ((SystemFunctionCall)getter).SetArg(1, tc.StaticTypeCheck(key, SequenceType.SINGLE_ATOMIC, role, visitor));
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "subscript supplied when calling an array as a function", 0);
                ((SystemFunctionCall)getter).SetArg(1, tc.StaticTypeCheck(key, SequenceType.SINGLE_INTEGER, role, visitor));
            }

            return getter.TypeCheck(visitor, contextInfo);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IFunctionItem function = (IFunctionItem)arguments[0].Head();
            TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
            IFunctionItemType fit = function.FunctionItemType;
            ArrayItem args = (ArrayItem)arguments[1].Head();
            if (function.GetArity() != args.ArrayLength())
            {
                string errorCode = "FOAP0001";
                throw new XPathException("Number of arguments required for dynamic call to " + function.Description + " is " + function.GetArity() + "; number supplied = " + args.ArrayLength(), errorCode).WithXPathContext(context);
            }

            ISequence[] argArray = new ISequence[args.ArrayLength()];
            if (fit == AnyFunctionType.ANY_FUNCTION)
            {
                for (int i = 0; i < argArray.Length; i++)
                {
                    argArray[i] = args[i];
                }
            }
            else
            {
                for (int i = 0; i < argArray.Length; i++)
                {
                    SequenceType expected = fit.ArgumentTypes[i];
                    int pos = i;
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, "fn:apply", pos + 1);
                    ISequence converted = th.ApplyFunctionConversionRules(args[i], expected, role, Loc.NONE);
                    argArray[i] = converted.Materialize();
                }
            }

            if (function.IsSequenceVariadic())
            {
                IList<IItem> members = new List<IItem>();
                foreach (IGroundedValue mem in args.Members())
                {
                    foreach (IItem it in mem.AsIterable())
                    {
                        members.Add(it);
                    }
                }

                IGroundedValue argSequence = SequenceExtent.MakeSequenceExtent(members);
                IList<IGroundedValue> singletonArg = new List<IGroundedValue>(1);
                singletonArg.Add(argSequence);
            }
            else
            {
            }

            ISequence rawResult = DynamicCall(function, context, argArray);
            if (function.IsTrustedResultType())
            {

                // trust system functions to return a result of the correct type
                return rawResult;
            }
            else
            {

                // Check the result of the function
                Func<RoleDiagnostic> resultRole = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, "fn:apply", -1);
                return th.ApplyFunctionConversionRules(rawResult, fit.ResultType, resultRole, Loc.NONE);
            }
        }

        public override void ExportAttributes(ExpressionPresenter @out)
        {
            @out.EmitAttribute("dyn", dynamicFunctionCall);
        }

        public override void ImportAttributes(Properties attributes)
        {
            dynamicFunctionCall = attributes.GetProperty("dyn");
        }
    }
}
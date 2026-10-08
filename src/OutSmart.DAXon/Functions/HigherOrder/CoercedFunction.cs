////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class CoercedFunction : AbstractFunction
    {
        private readonly IFunctionItem targetFunction;
        private readonly SpecificFunctionType requiredType;

        public IFunctionItem TargetFunction => targetFunction;

        public override IFunctionItemType FunctionItemType => requiredType;

        public override string Description => targetFunction.Description + " (used where the required type is " + requiredType + ")";
        public CoercedFunction(IFunctionItem targetFunction, SpecificFunctionType requiredType, bool allowReducedArity)
        {
            if (targetFunction.GetArity() != requiredType.GetArity())
            {
                if (targetFunction.GetArity() > requiredType.GetArity() || !allowReducedArity)
                {
                    throw new XPathException(WrongArityMessage(targetFunction, requiredType.GetArity()), "XPTY0004");
                }
            }

            this.targetFunction = targetFunction;
            this.requiredType = requiredType;
        }

        public override void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            if (targetFunction is AbstractFunction)
            {
                ((AbstractFunction)targetFunction).TypeCheck(visitor, contextItemType);
            }
        }

        // These two hid (not overrode) AbstractFunction's virtual NIE stubs, so function-name() on a
        // coerced function dispatched to the base and threw (fn-function-name-024).
        public override StructuredQName GetFunctionName()
        {
            return targetFunction.GetFunctionName();
        }

        public override int GetArity()
        {
            return requiredType.GetArity();
        }

        public override AnnotationList GetAnnotations()
        {
            return targetFunction.GetAnnotations();
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            SpecificFunctionType req = requiredType;
            SequenceType[] argTypes = targetFunction.FunctionItemType.ArgumentTypes;
            int suppliedArity = Math.Min(args.Length, argTypes.Length);
            TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
            ISequence[] targetArgs = new ISequence[suppliedArity];
            for (int i = 0; i < suppliedArity; i++)
            {
                IGroundedValue gVal = args[i].Materialize();
                if (argTypes[i].Matches(gVal, th))
                {
                    targetArgs[i] = gVal;
                }
                else
                {
                    int pos = i;
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, targetFunction.Description, pos);
                    targetArgs[i] = th.ApplyFunctionConversionRules(gVal, argTypes[i], role, Loc.NONE);
                }
            }


            // TODO: don't materialize the result if static type checking tells us the result will be OK
            IGroundedValue rawResult = targetFunction.Call(context, targetArgs).Materialize();
            if (req.ResultType.Matches(rawResult, th))
            {
                return rawResult;
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, Description, 0);
                return th.ApplyFunctionConversionRules(rawResult, req.ResultType, role, Loc.NONE);
            }
        }

        private static string WrongArityMessage(IFunctionItem supplied, int expected)
        {
            return "The supplied function (" + supplied.Description + ") has " + FunctionCall.Plural(supplied.GetArity(), "parameter") + " - expected a function with " + FunctionCall.Plural(expected, "parameter");
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("coercedFn");
            @out.EmitAttribute("type", requiredType.ToExportString());
            new FunctionLiteral(targetFunction).Export(@out);
            @out.EndElement();
        }
    }
}

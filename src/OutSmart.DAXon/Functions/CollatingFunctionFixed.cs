////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Functions
{
    public abstract class CollatingFunctionFixed : SystemFunction, IStatefulSystemFunction
    {
        private string collationName;
        private IStringCollator stringCollator = null;
        private IAtomicComparer atomicComparer = null;

        public virtual IStringCollator StringCollator => stringCollator;

        public virtual IAtomicComparer PreAllocatedAtomicComparer => atomicComparer;
        public virtual bool IsSubstringMatchingFunction()
        {
            return false;
        }

        public override void SetRetainedStaticContext(RetainedStaticContext retainedStaticContext)
        {
            base.SetRetainedStaticContext(retainedStaticContext);
            if (collationName == null)
            {
                collationName = retainedStaticContext.DefaultCollationName;
                try
                {
                    AllocateCollator();
                }
                catch (XPathException e)
                {
                }
            }
        }

        public virtual void SetCollationName(string collationName)
        {
            this.collationName = collationName;
            AllocateCollator();
        }

        private void AllocateCollator()
        {
            stringCollator = GetRetainedStaticContext().GetConfiguration().GetCollation(collationName);
            if (stringCollator == null)
            {
                throw new XPathException("Unknown collation " + collationName, "FOCH0002");
            }

            if (IsSubstringMatchingFunction())
            {
                if (stringCollator is SimpleCollation)
                {
                    stringCollator = ((SimpleCollation)stringCollator).SubstringMatcher;
                }

                if (!(stringCollator is ISubstringMatcher))
                {
                    throw new XPathException("The collation requested for " + GetFunctionName().DisplayName + " does not support substring matching", "FOCH0004");
                }
            }
        }

        protected virtual void PreAllocateComparer(IAtomicType type0, IAtomicType type1, IStaticContext env)
        {
            IStringCollator collation = StringCollator;
            if (type0 == ErrorType.GetInstance() || type1 == ErrorType.GetInstance())
            {

                // there will be no instances to compare, so we can use any comparer
                atomicComparer = GenericObjectEqualityAtomicComparer.Instance;
                return;
            }

            atomicComparer = GenericAtomicComparer.MakeAtomicComparer((BuiltInAtomicType)type0.BuiltInBaseType, (BuiltInAtomicType)type1.BuiltInBaseType, stringCollator, env.MakeEarlyEvaluationContext());
        }

        public virtual IAtomicComparer GetAtomicComparer(IXPathContext context)
        {
            if (atomicComparer != null)
            {
                return atomicComparer.ProvideContext(context);
            }
            else
            {
                return new GenericAtomicComparer(StringCollator, context);
            }
        }

        public override void ExportAttributes(ExpressionPresenter @out)
        {
            if (!collationName.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI))
            {
                @out.EmitAttribute("collation", collationName);
            }
        }

        public override void ImportAttributes(Properties attributes)
        {
            string collationName = attributes.GetProperty("collation");
            if (collationName != null)
            {
                SetCollationName(collationName);
            }
        }

        public CollatingFunctionFixed Copy()
        {
            SystemFunction copy = SystemFunction.MakeFunction(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), GetArity());
            if (copy is CollatingFunctionFree)
            {
                try
                {
                    copy = ((CollatingFunctionFree)copy).BindCollation(collationName);
                }
                catch (XPathException e)
                {
                    throw new InvalidOperationException(e.Message, e);
                }
            }

            if (copy is CollatingFunctionFixed)
            {
                ((CollatingFunctionFixed)copy).collationName = collationName;
                ((CollatingFunctionFixed)copy).atomicComparer = atomicComparer;
                ((CollatingFunctionFixed)copy).stringCollator = stringCollator;
                return (CollatingFunctionFixed)copy;
            }

            throw new InvalidOperationException();
        }
        // net472 has no covariant return: delegate the interface method to the real Copy() (was => default =
        // null, so SystemFunctionCall.Copy NRE'd when the optimizer rebound a tree containing this function).
        SystemFunction IStatefulSystemFunction.Copy() => Copy();
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the XSLT 3.0 function current-output-uri()
    /// </summary>
    internal sealed class CurrentOutputUri : ContextAccessorFunction, ICallable
    {
        public override int GetSpecialProperties(Expression[] arguments)
        {

            // Prevent inlining of stylesheet functions calling current-output-uri()
            return base.GetSpecialProperties(arguments) | StaticProperty.HAS_SIDE_EFFECTS;
        }

        public override IFunctionItem BindContext(IXPathContext context)
        {
            string uri = context.CurrentOutputUri;
            ConstantFunction fn = new ConstantFunction(uri == null ? EmptySequence.GetInstance() : new AnyURIValue(uri));
            fn.Details = Details;
            fn.SetRetainedStaticContext(GetRetainedStaticContext());
            return fn;
        }

        /// <summary>
        /// Evaluate in a general context
        /// </summary>
        public AnyURIValue EvaluateItem(IXPathContext context)
        {
            string uri = context.CurrentOutputUri;
            return uri == null ? null : new AnyURIValue(uri);
        }

        /// <summary>
        /// Evaluate in a general context
        /// </summary>
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ItemOrEmpty(EvaluateItem(context));
        }
    }
}
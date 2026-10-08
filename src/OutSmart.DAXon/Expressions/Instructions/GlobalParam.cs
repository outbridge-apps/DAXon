////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public sealed class GlobalParam : GlobalVariable
    {
        private bool implicitlyRequired;

        public override string TracingTag => "xsl:param";

        protected override string Flags
        {
            get
            {
                string f = base.Flags;
                if (IsImplicitlyRequiredParam())
                {
                    f += "i";
                }

                return f;
            }
        }
        public GlobalParam()
        {
        }

        public void SetImplicitlyRequiredParam(bool requiredParam)
        {
            this.implicitlyRequired = requiredParam;
        }

        public bool IsImplicitlyRequiredParam()
        {
            return this.implicitlyRequired;
        }

        public override IGroundedValue EvaluateVariable(IXPathContext context, Component target)
        {
            Controller controller = context.GetController();
            Bindery b = controller.GetBindery(GetPackageData());
            IGroundedValue val = b.GetGlobalVariableValue(this);
            if (val != null)
            {
                if (val is Bindery.FailureValue)
                {
                    throw ((Bindery.FailureValue)val).GetObject();
                }

                return val;
            }

            val = controller.GetConvertedParameter(GetVariableQName(), GetRequiredType(), context);
            if (val != null)
            {
                return b.SaveGlobalVariableValue(this, val);
            }

            if (IsRequiredParam())
            {
                throw new XPathException("No value supplied for required parameter $" + GetVariableQName().DisplayName).WithXPathContext(context).WithLocation(this).WithErrorCode(GetPackageData().IsXSLT() ? "XTDE0050" : "XPDY0002");
            }
            else if (IsImplicitlyRequiredParam())
            {
                throw new XPathException("A value must be supplied for parameter $" + GetVariableQName().DisplayName + " because there is no default value for the required type").WithXPathContext(context).WithLocation(this).WithErrorCode("XTDE0700");
            }


            // evaluate and save the default value
            return ActuallyEvaluate(context, target);
        }

        public override IGroundedValue EvaluateVariable(IXPathContext context)
        {
            Component target = context.GetCurrentComponent(); // Bug #6236
            if (target == null)
            {
                target = DeclaringComponent;
            }

            return EvaluateVariable(context, target);
        }
    }
}
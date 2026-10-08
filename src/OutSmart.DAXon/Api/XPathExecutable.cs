////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XPath;
using System.Collections.Generic;
namespace OutSmart.DAXon.Api
{
    public class XPathExecutable
    {
        private readonly XPathExpression exp;
        private readonly Processor processor;
        private readonly IndependentContext env;

        public virtual ItemType ResultItemType
        {
            get
            {
                Types.ItemType it = exp.InternalExpression.GetItemType();
                return new ConstructedItemType(it, processor.UnderlyingConfiguration);
            }
        }

        public virtual OccurrenceIndicator ResultCardinality
        {
            get
            {
                int card = exp.InternalExpression.GetCardinality();
                return OccurrenceIndicatorHelper.GetOccurrenceIndicator(card);
            }
        }

        public virtual XPathExpression UnderlyingExpression => exp;

        public virtual IStaticContext UnderlyingStaticContext => env;
        // protected constructor
        public XPathExecutable(XPathExpression exp, Processor processor, IndependentContext env)
        {
            this.exp = exp;
            this.processor = processor;
            this.env = env; //this.declaredVariables = declaredVariables;
        }

        public virtual XPathSelector Load()
        {
            Dictionary<StructuredQName, XPathVariable> declaredVariables = new Dictionary<StructuredQName, XPathVariable>();
            foreach (XPathVariable var in env.ExternalVariables)
            {
                declaredVariables[var.GetVariableQName()] = var;
            }

            return new XPathSelector(exp, declaredVariables);
        }

        public virtual IEnumerator<QName> IterateExternalVariables()
        {
            IList<QName> list = new List<QName>();
            foreach (XPathVariable var in env.ExternalVariables)
            {
                list.Add(new QName(var.GetVariableQName()));
            }

            return list.GetEnumerator();
        }

        public virtual ItemType GetRequiredItemTypeForVariable(QName variableName)
        {
            XPathVariable var = env.GetExternalVariable(variableName.GetStructuredQName());
            if (var == null)
            {
                return null;
            }
            else
            {
                return new ConstructedItemType(var.GetRequiredType().PrimaryType, processor.UnderlyingConfiguration);
            }
        }

        public virtual OccurrenceIndicator? GetRequiredCardinalityForVariable(QName variableName)
        {
            XPathVariable var = env.GetExternalVariable(variableName.GetStructuredQName());
            if (var == null)
            {
                return null;
            }
            else
            {
                return OccurrenceIndicatorHelper.GetOccurrenceIndicator(var.GetRequiredType().GetCardinality());
            }
        }
    }
}

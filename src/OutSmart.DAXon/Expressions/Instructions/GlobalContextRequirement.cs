////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class GlobalContextRequirement
    {
        private bool mayBeOmitted = true;
        private bool absentFocus;
        private bool external; // XQuery only
        private readonly IList<ItemType> requiredItemTypes = new List<ItemType>();
        private Expression defaultValue; // Used in XQuery only
        public virtual ItemType RequiredItemType
        {
            get
            {
                if (requiredItemTypes.Count == 0)
                {
                    return AnyItemType.GetInstance();
                }
                else
                {
                    return requiredItemTypes[0];
                }
            }
        }

        public virtual IList<ItemType> RequiredItemTypes => requiredItemTypes;

        public virtual Expression DefaultValue
        {
            get => defaultValue; set
            {
                this.defaultValue = value;
            }
        }

        public virtual void AddRequiredItemType(ItemType requiredItemType)
        {
            requiredItemTypes.Add(requiredItemType);
        }

        public virtual void Export(ExpressionPresenter @out)
        {
            @out.StartElement("glob");
            string use;
            if (IsMayBeOmitted())
            {
                if (IsAbsentFocus())
                {
                    use = "pro";
                }
                else
                {
                    use = "opt";
                }
            }
            else
            {
                use = "req";
            }

            @out.EmitAttribute("use", use);
            if (!RequiredItemType.Equals(AnyItemType.GetInstance()))
            {
                @out.EmitAttribute("type", RequiredItemType.ToExportString());
            }

            @out.EndElement();
        }

        public virtual void SetAbsentFocus(bool absent)
        {
            this.absentFocus = absent;
        }

        public virtual bool IsAbsentFocus()
        {
            return absentFocus;
        }

        public virtual void SetMayBeOmitted(bool mayOmit)
        {
            this.mayBeOmitted = mayOmit;
        }

        public virtual bool IsMayBeOmitted()
        {
            return mayBeOmitted;
        }

        public virtual void SetExternal(bool external)
        {
            this.external = external;
        }

        public virtual bool IsExternal()
        {
            return external;
        }

        public virtual ContextItemStaticInfo MakeGlobalContextInfo(Configuration config)
        {
            ItemType type = IsAbsentFocus() ? ErrorType.GetInstance() : RequiredItemType;
            return config.MakeContextItemStaticInfo(type, IsMayBeOmitted());
        }
    }
}
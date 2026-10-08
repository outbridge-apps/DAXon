////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using System.Text;
namespace OutSmart.DAXon.Tracing
{
    public class TemplateRuleTraceListener
    {
        private int depth = 0;
        private readonly Logger logger;
        public TemplateRuleTraceListener(Logger logger)
        {
            this.logger = logger;
        }

        public virtual void Enter(string instName, ILocation instLoc, IItem item, TemplateRule rule)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < depth; i++)
            {
                builder.Append(' ');
            }

            depth++;
            builder.Append(instName).Append(" at ").Append(Err.Show(instLoc));
            builder.Append(" to ").Append(item.ToShortString());
            if (item is NodeInfo && ((NodeInfo)item).GetLineNumber() != -1)
            {
                builder.Append(" at ").Append(Err.AbbreviateURI(((NodeInfo)item).GetBaseURI())).Append('#').Append(((NodeInfo)item).GetLineNumber());
            }

            builder.Append(" using ");
            if (rule == null)
            {
                builder.Append("built-in rule");
            }
            else
            {
                builder.Append("rule at ").Append(Err.Show(rule));
            }

            string message = builder.ToString().Replace(" .../", " ");
            logger.Info(message);
        }

        public virtual void Leave()
        {
            depth--;
        }
    }
}
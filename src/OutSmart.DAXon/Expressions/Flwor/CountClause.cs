////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// A "count" clause in a FLWOR expression
    /// </summary>
    internal sealed class CountClause : Clause
    {
        private LocalVariableBinding rangeVariable;
        public override ClauseName ClauseKey => COUNT;

        public LocalVariableBinding RangeVariable
        {
            get => rangeVariable; set
            {
                this.rangeVariable = value;
            }
        }

        public override LocalVariableBinding[] RangeVariables => new LocalVariableBinding[]
            {
                rangeVariable
            };

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            CountClause c2 = new CountClause();
            c2.rangeVariable = rangeVariable.Copy();
            c2.SetPackageData(GetPackageData());
            c2.Location = Location;
            return c2;
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            return new CountClausePull(@base, this);
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            return new CountClausePush(output, destination, this);
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
        }

        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
        }

        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("count");
            @out.EmitAttribute("var", RangeVariable.GetVariableQName());
            @out.EndElement();
        }

        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("count $");
            fsb.Append(rangeVariable.GetVariableQName().DisplayName);
            return fsb.ToString();
        }
    }
}

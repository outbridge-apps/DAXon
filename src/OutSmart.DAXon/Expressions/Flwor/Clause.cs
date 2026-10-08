////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Flwor
{
    public abstract class Clause
    {

        private ILocation location;
        private PackageData packageData;
        private bool repeated;
        public virtual ILocation Location
        {
            get => location == null ? Loc.NONE : location; set
            {
                this.location = value;
            }
        }
        public virtual LocalVariableBinding[] RangeVariables => new LocalVariableBinding[0];
        public abstract ClauseName ClauseKey { get; }

        public virtual Dictionary<string, object> TraceInfo
        {
            get
            {
                LocalVariableBinding[] vars = RangeVariables;
                if (vars.Length == 0)
                {
                    return new Dictionary<string, object>();
                }
                else
                {
                    Dictionary<string, object> info = new Dictionary<string, object>(1);
                    info["var"] = "$" + vars[0].GetVariableQName().DisplayName;
                    return info;
                }
            }
        }

        public virtual void SetPackageData(PackageData pd)
        {
            this.packageData = pd;
        }

        public virtual PackageData GetPackageData()
        {
            return packageData;
        }

        public virtual Configuration GetConfiguration()
        {
            return packageData.GetConfiguration();
        }

        public virtual void SetRepeated(bool repeated)
        {
            this.repeated = repeated;
        }

        public virtual bool IsRepeated()
        {
            return repeated;
        }

        public abstract Clause Copy(FLWORExpression flwor, RebindingMap rebindings);
        public virtual void Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
        }

        public virtual void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
        }

        public abstract TuplePull GetPullStream(TuplePull @base, IXPathContext context);
        public abstract TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context);
        public abstract void ProcessOperands(IOperandProcessor processor);
        public abstract void Explain(ExpressionPresenter @out);

        public virtual void GatherVariableReferences(ExpressionVisitor visitor, IBinding binding, IList<VariableReference> refs)
        {
        }

        public virtual bool ContainsNonInlineableVariableReference(IBinding binding)
        {
            return false;
        }

        public virtual void RefineVariableType(ExpressionVisitor visitor, IList<VariableReference> references, Expression returnExpr)
        {
        }

        public abstract void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet);
        public virtual string ToShortString()
        {
            return ToString();
        }
        public enum ClauseName
        {
            FOR,
            LET,
            WINDOW,
            GROUP_BY,
            COUNT,
            ORDER_BY,
            WHERE,
            TRACE,
            DIAG,
            FOR_MEMBER
        }
    }
}
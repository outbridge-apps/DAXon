////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Parsing;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An xsl:apply-imports or xsl:next-match element in the stylesheet.
    /// </summary>
    internal abstract class ApplyNextMatchingTemplate : Instruction, IITemplateCall
    {
        private WithParam[] actualParams;
        private WithParam[] tunnelParams;

        public override int ImplementationMethod => base.ImplementationMethod | Expression.WATCH_METHOD;

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CONTEXT_ITEM;
        public ApplyNextMatchingTemplate()
        {
        }

        public WithParam[] GetActualParams()
        {
            return actualParams;
        }

        public WithParam[] GetTunnelParams()
        {
            return tunnelParams;
        }

        public virtual void SetActualParams(WithParam[] @params)
        {
            this.actualParams = @params;
        }

        public virtual void SetTunnelParams(WithParam[] @params)
        {
            this.tunnelParams = @params;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> operanda = new List<Operand>(actualParams.Length + tunnelParams.Length);
            WithParam.GatherOperands(this, actualParams, operanda);
            WithParam.GatherOperands(this, tunnelParams, operanda);
            return operanda;
        }

        public override Expression Simplify()
        {
            WithParam.Simplify(GetActualParams());
            WithParam.Simplify(GetTunnelParams());
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            WithParam.TypeCheck(actualParams, visitor, contextInfo);
            WithParam.TypeCheck(tunnelParams, visitor, contextInfo);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            WithParam.Optimize(visitor, actualParams, contextInfo);
            WithParam.Optimize(visitor, tunnelParams, contextInfo);
            return this;
        }

        public override bool MayCreateNewNodes()
        {
            return true;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {

            // This logic is assuming the mode is streamable (so called templates can't return streamed nodes)
            //PathMap.PathMapNodeSet result = super.addToPathMap(pathMap, pathMapNodeSet);
            if (pathMapNodeSet == null)
            {
                ContextItemExpression cie = new ContextItemExpression();

                pathMapNodeSet = new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(cie));
            }

            pathMapNodeSet.AddDescendants();
            return new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(this));
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal abstract class ParentNodeConstructor : Instruction, IValidatingInstruction, IInstructionWithComplexContent
    {
        private static readonly OperandRole SAME_FOCUS_CONTENT = new OperandRole(0, OperandUsage.ABSORPTION, SequenceType.ANY_SEQUENCE);
        protected Operand contentOp;
        private ParseOptions validationOptions;
        protected bool preservingTypes = true;

        public virtual ParseOptions ValidationOptions
        {
            get => validationOptions; set
            {
                this.validationOptions = value;
            }
        }

        public virtual Operand ContentOperand => contentOp;
        public ParentNodeConstructor()
        {
        }

        public ISchemaType GetSchemaType()
        {
            return validationOptions == null ? null : validationOptions.TopLevelType;
        }

        public virtual void SetValidationAction(int mode, ISchemaType schemaType)
        {
            preservingTypes = mode == Validation.PRESERVE && schemaType == null;
            if (!preservingTypes)
            {
                if (validationOptions == null)
                {
                    validationOptions = new ParseOptions();
                }

                if (schemaType == Untyped.INSTANCE)
                {
                    validationOptions = validationOptions.WithSchemaValidationMode(Validation.SKIP);
                }
                else
                {
                    validationOptions = validationOptions.WithSchemaValidationMode(mode).WithTopLevelType(schemaType);
                }
            }
        }

        public int GetValidationAction()
        {
            return validationOptions == null ? Validation.PRESERVE : validationOptions.GetSchemaValidationMode();
        }

        public virtual void SetNoNeedToStrip()
        {
            preservingTypes = true;
        }

        public virtual void SetContentExpression(Expression content)
        {
            if (contentOp == null)
            {
                contentOp = new Operand(this, content, SAME_FOCUS_CONTENT);
            }
            else
            {
                contentOp.SetChildExpression(content);
            }
        }

        public Expression GetContentExpression()
        {
            return contentOp == null ? null : contentOp.GetChildExpression();
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            CheckContentSequence(visitor.StaticContext);
            return this;
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        protected abstract void CheckContentSequence(IStaticContext env);
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            OptimizeChildren(visitor, contextItemType);
            if (!Literal.IsEmptySequence(GetContentExpression()))
            {
                if (GetContentExpression() is Block)
                {
                    SetContentExpression(((Block)GetContentExpression()).MergeAdjacentTextInstructions());
                }


                // This code removed 25 Aug 2016. We no longer introduce copy operations. But we could go
                // further, and try to get rid of more unnecessary copy operations (whether streaming or not...)
                //  Reinstated 14 Oct 2016
                if (visitor.IsOptimizeForStreaming())
                {
                    visitor.ObtainOptimizer().MakeCopyOperationsExplicit(this, contentOp);
                }
            }

            if (visitor.StaticContext.GetPackageData().IsSchemaAware())
            {
                TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
                if (GetValidationAction() == Validation.STRIP)
                {
                    if (GetContentExpression().HasSpecialProperty(StaticProperty.ALL_NODES_UNTYPED) || th.Relationship(GetContentExpression().GetItemType(), MultipleNodeKindTest.DOC_ELEM_ATTR) == Affinity.DISJOINT)
                    {

                        // No need to strip type annotations if there are none needing to be stripped
                        SetNoNeedToStrip();
                    }
                }
            }
            else
            {
                SetValidationAction(Validation.STRIP, null);
                SetNoNeedToStrip();
            }

            return this;
        }

        public override bool MayCreateNewNodes()
        {
            return true;
        }

        public override bool AlwaysCreatesNewNodes()
        {
            return true;
        }

        public override int GetCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet result = base.AddToPathMap(pathMap, pathMapNodeSet);
            result.SetReturnable(false);
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            ItemType type = GetItemType();
            if (th.Relationship(type, NodeKindTest.ELEMENT) != Affinity.DISJOINT || th.Relationship(type, NodeKindTest.DOCUMENT) != Affinity.DISJOINT)
            {
                result.AddDescendants();
            }

            return new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(this));
        }

        public virtual bool IsLocal()
        {
            return ExpressionTool.IsLocalConstructor(this);
        }
    }
}

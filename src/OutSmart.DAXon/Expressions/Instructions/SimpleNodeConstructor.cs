////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public abstract class SimpleNodeConstructor : Instruction
    {
        protected Operand selectOp;

        public virtual Expression Select
        {
            get => selectOp.GetChildExpression(); set
            {
                selectOp.SetChildExpression(value);
            }
        }

        public virtual Operand SelectOp => selectOp;

        public override string StreamerName => "SimpleNodeConstructor";
        public SimpleNodeConstructor()
        {
            Expression select = Literal.MakeEmptySequence();
            selectOp = new Operand(this, select, OperandRole.SINGLE_ATOMIC);
        }

        public override IEnumerable<Operand> Operands()
        {
            return selectOp;
        }

        public override bool MayCreateNewNodes()
        {
            return true;
        }

        public override bool AlwaysCreatesNewNodes()
        {
            return true;
        }

        protected override int ComputeCardinality()
        {
            return Select.GetCardinality(); // may allow empty sequence
        }

        protected override int ComputeSpecialProperties()
        {
            return base.ComputeSpecialProperties() | StaticProperty.SINGLE_DOCUMENT_NODESET;
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        public abstract void LocalTypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType);
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            LocalTypeCheck(visitor, contextInfo);
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            if (Select is ValueOf)
            {
                Expression valSelect = ((ValueOf)Select).Select;
                if (th.IsSubType(valSelect.GetItemType(), BuiltInAtomicType.STRING) && !Cardinality.AllowsMany(valSelect.GetCardinality()))
                {
                    Select = valSelect;
                }
            }


            // Don't bother converting untypedAtomic to string
            if (Select.IsCallOn(typeof(String_1)))
            {
                SystemFunctionCall fn = (SystemFunctionCall)Select;
                Expression arg = fn.GetArg(0);
                if (arg.GetItemType() == BuiltInAtomicType.UNTYPED_ATOMIC && !Cardinality.AllowsMany(arg.GetCardinality()))
                {
                    Select = arg;
                }
            }
            else if (Select is CastExpression && ((CastExpression)Select).TargetType == BuiltInAtomicType.STRING)
            {
                Expression arg = ((CastExpression)Select).BaseExpression;
                if (arg.GetItemType() == BuiltInAtomicType.UNTYPED_ATOMIC && !Cardinality.AllowsMany(arg.GetCardinality()))
                {
                    Select = arg;
                }
            }

            AdoptChildExpression(Select);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            OptimizeChildren(visitor, contextItemType);
            if (Select.IsCallOn(typeof(String_1)))
            {
                SystemFunctionCall sf = (SystemFunctionCall)Select;
                TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
                if (th.IsSubType(sf.GetArg(0).GetItemType(), BuiltInAtomicType.STRING) && !Cardinality.AllowsMany(sf.GetArg(0).GetCardinality()))
                {
                    Select = sf.GetArg(0);
                }
            }

            return this;
        }

        public abstract void ProcessValue(UnicodeString value, Outputter output, IXPathContext context);
        public override IItem EvaluateItem(IXPathContext context)
        {
            IItem contentItem = Select.MakeElaborator().ElaborateForItem().Eval(context);
            UnicodeString content;
            if (contentItem == null)
            {
                content = EmptyUnicodeString.GetInstance();
            }
            else
            {
                content = contentItem.UnicodeStringValue;
                content = CheckContent(content, context);
            }

            Orphan o = new Orphan(context.GetConfiguration());
            o.SetNodeKind((short)GetItemType().PrimitiveType);
            o.SetStringValue(content);
            o.SetNodeName(EvaluateNodeName(context));
            return o;
        }

        public virtual UnicodeString CheckContent(UnicodeString data, IXPathContext context)
        {
            return data;
        }

        public virtual INodeName EvaluateNodeName(IXPathContext context)
        {
            return null;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return SingletonIterator.MakeIterator(EvaluateItem(context));
        }

        public virtual bool IsLocal()
        {
            return ExpressionTool.IsLocalConstructor(this);
        }
    }
}
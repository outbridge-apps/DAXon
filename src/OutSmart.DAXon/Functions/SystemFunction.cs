////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Abstract superclass for calls to functions in the standard function library
    /// </summary>
    public abstract class SystemFunction : AbstractFunction
    {
        private int arity;
        private BuiltInFunctionSet.Entry details;
        private RetainedStaticContext retainedStaticContext;

        public virtual int NetCost => 1;

        public virtual BuiltInFunctionSet.Entry Details
        {
            get => details; set
            {
                details = value;
            }
        }

        public override string Description => details.name.DisplayName;

        public override OperandRole[] OperandRoles
        {
            get
            {
                OperandRole[] roles = new OperandRole[GetArity()];
                OperandUsage[] usages;
                if (IsSequenceVariadic())
                {
                    usages = new OperandUsage[GetArity()];
                    ArrayTools.Fill(usages, details.usage[0]);
                }
                else
                {
                    usages = details.usage;
                }

                try
                {
                    for (int i = 0; i < roles.Length; i++)
                    {
                        roles[i] = new OperandRole(0, usages[i], GetRequiredType(i));
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // ignored, as upstream does (it only printed the stack trace)
                }

                return roles;
            }
        }

        public virtual IntegerValue[] IntegerBounds => null;

        public virtual string ErrorCodeForTypeErrors => "XPTY0004";

        public virtual ItemType ResultItemType => details.itemType;

        public override IFunctionItemType FunctionItemType
        {
            get
            {
                SequenceType resultType = SequenceType.MakeSequenceType(ResultItemType, details.cardinality);
                SequenceType[] paramTypes = details.paramTypes;
                if (paramTypes.Length != arity)
                {
                    Array.Resize(ref paramTypes, arity);
                }

                return new SpecificFunctionType(paramTypes, resultType);
            }
        }

        public virtual string StaticBaseUriString => GetRetainedStaticContext().StaticBaseUriString;

        public virtual string StreamerName => null;
        public static Expression MakeCall(string name, RetainedStaticContext rsc, params Expression[] arguments)
        {
            SystemFunction f = MakeFunction(name, rsc, arguments.Length);
            Expression expr = f.MakeFunctionCall(arguments);
            expr.SetRetainedStaticContext(rsc);
            return expr;
        }

        public static SystemFunction MakeFunction(string name, RetainedStaticContext rsc, int arity)
        {
            if (rsc == null)
                throw new NullReferenceException();
            SystemFunction fn = rsc.GetConfiguration().MakeSystemFunction(name, arity, rsc.GetPackageData().HostLanguageVersion);
            if (fn == null)
            {
                throw new ArgumentException(name + "#" + arity);
            }

            fn.SetRetainedStaticContext(rsc);
            return fn;
        }

        public static SystemFunction MakeFunction40(string name, RetainedStaticContext rsc, int arity)
        {
            if (rsc == null)
                throw new NullReferenceException();
            SystemFunction fn = rsc.GetConfiguration().MakeSystemFunction40(name, arity);
            if (fn == null)
            {
                throw new ArgumentException(name + "#" + arity);
            }

            fn.SetRetainedStaticContext(rsc);
            return fn;
        }

        public virtual Expression MakeFunctionCall(params Expression[] arguments)
        {
            if (arguments.Length > GetArity() && IsSequenceVariadic())
            {
                if (GetArity() != 1)
                {
                    throw new NotSupportedException("Not implemented: sequence-variadic function with arity>1");
                }

                arguments = new Expression[]
                {
                    new Block(arguments)
                };
            }

            Expression e = new SystemFunctionCall(this, arguments);
            e.SetRetainedStaticContext(GetRetainedStaticContext());
            return e;
        }

        public virtual void SetArity(int arity)
        {
            this.arity = arity;
        }

        public override bool IsSequenceVariadic()
        {
            return (details.properties & BuiltInFunctionSet.SEQV) != 0;
        }

        public virtual Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            Optimizer opt = visitor.ObtainOptimizer();
            if (opt.IsOptionSet(OptimizerOptions.CONSTANT_FOLDING))
            {
                return FixArguments(arguments);
            }
            else
            {
                return null;
            }
        }

        public virtual Expression FixArguments(params Expression[] arguments)
        {

            // Check if any arguments are known to be empty, with a declared result for that case
            for (int i = 0; i < GetArity(); i++)
            {
                if (Literal.IsEmptySequence(arguments[i]) && ResultIfEmpty(i) != null)
                {
                    return Literal.MakeLiteral(details.resultIfEmpty[i].Materialize());
                }
            }

            return null;
        }

        protected virtual ISequence ResultIfEmpty(int arg)
        {
            return details.resultIfEmpty[arg];
        }

        public virtual RetainedStaticContext GetRetainedStaticContext()
        {
            return retainedStaticContext;
        }

        public virtual void SetRetainedStaticContext(RetainedStaticContext retainedStaticContext)
        {
            this.retainedStaticContext = retainedStaticContext;
        }

        public virtual bool DependsOnContextItem()
        {
            return (details.properties & (BuiltInFunctionSet.CITEM | BuiltInFunctionSet.CDOC)) != 0;
        }

        public override StructuredQName GetFunctionName()
        {
            return details.name;
        }

        public override int GetArity()
        {
            return arity;
        }

        public virtual void SupplyTypeInformation(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, Expression[] arguments)
        {
        }

        public override bool Equals(object o)
        {
            return (o is SystemFunction) && base.Equals(o);
        }

        public override int GetHashCode()
        {

            // included explicitly because equals() is overridden: prevents compiler warnings
            return base.GetHashCode();
        }

        public virtual SequenceType GetRequiredType(int arg)
        {
            if (details == null)
            {
                return SequenceType.ANY_SEQUENCE;
            }

            return details.paramTypes[arg]; // this is overridden for concat()
        }

        public virtual ItemType GetResultItemType(Expression[] args)
        {
            if ((details.properties & BuiltInFunctionSet.AS_ARG0) != 0)
            {
                return args[0].GetItemType();
            }
            else if ((details.properties & BuiltInFunctionSet.AS_PRIM_ARG0) != 0)
            {
                IPlainType atomized = (IPlainType)args[0].GetItemType().GetAtomizedItemType();
                return atomized.Equals(BuiltInAtomicType.UNTYPED_ATOMIC) ? BuiltInAtomicType.DOUBLE : atomized;
            }
            else
            {
                return details.itemType;
            }
        }

        public virtual int GetCardinality(Expression[] args)
        {
            int c = details.cardinality;
            if (c == BuiltInFunctionSet.OPT && (details.properties & BuiltInFunctionSet.CARD0) != 0 && !Cardinality.AllowsZero(args[0].GetCardinality()))
            {
                return StaticProperty.EXACTLY_ONE;
            }
            else
            {
                return c;
            }
        }

        public virtual int GetSpecialProperties(Expression[] arguments)
        {
            if ((details.properties & BuiltInFunctionSet.NEW) != 0)
            {
                return StaticProperty.ALL_NODES_NEWLY_CREATED;
            }

            int p = StaticProperty.NO_NODES_NEWLY_CREATED;
            if ((details.properties & BuiltInFunctionSet.SIDE) != 0)
            {
                p |= StaticProperty.HAS_SIDE_EFFECTS;
            }

            return p;
        }

        protected virtual NodeInfo GetContextNode(IXPathContext context)
        {
            IItem item = context.GetContextItem();
            if (item == null)
            {
                XPathException err = new XPathException("Context item for " + GetFunctionName() + "() is absent", "XPDY0002");
                err.MaybeSetContext(context);
                throw err;
            }
            else if (!(item is NodeInfo))
            {
                XPathException err = new XPathException("Context item for " + GetFunctionName() + "() is not a node", "XPTY0004");
                err.MaybeSetContext(context);
                throw err;
            }
            else
            {
                return (NodeInfo)item;
            }
        }

        public static ISequence DynamicCall(IFunctionItem f, IXPathContext context, params ISequence[] args)
        {
            return f.Call(context, args);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("fnRef");
            StructuredQName qName = GetFunctionName();
            string name = qName.HasURI(NamespaceUri.FN) ? qName.GetLocalPart() : qName.EQName;
            @out.EmitAttribute("name", name);
            @out.EmitAttribute("arity", GetArity() + "");
            if ((Details.properties & BuiltInFunctionSet.DEPENDS_ON_STATIC_CONTEXT) != 0)
            {
                @out.EmitRetainedStaticContext(GetRetainedStaticContext(), null);
            }

            @out.EndElement();
        }

        public virtual Expression TypeCheckCaller(FunctionCall caller, ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return caller;
        }

        public override bool IsTrustedResultType()
        {
            return true;
        }

        public virtual void ExportAttributes(ExpressionPresenter @out)
        {
        }

        public virtual void ExportAdditionalArguments(SystemFunctionCall call, ExpressionPresenter @out)
        {
        }

        public virtual void ImportAttributes(Properties attributes)
        {
        }

        public override string ToShortString()
        {
            return GetFunctionName().DisplayName + '#' + GetArity();
        }

        public override string ToString()
        {
            return GetFunctionName().DisplayName + '#' + GetArity();
        }

        protected virtual UnicodeString GetUniStringArg(ISequence supplied)
        {
            StringValue item = (StringValue)supplied.Head();
            return item == null ? EmptyUnicodeString.GetInstance() : item.UnicodeStringValue;
        }

        public virtual Elaborator GetElaborator()
        {
            return null;
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
namespace OutSmart.DAXon.Expressions.Sorting
{
    // TODO: optimise also for the case where the attributes depend only on global variables
    // or parameters, in which case the same IAtomicComparer can be used for the duration of a
    // transformation.
    public class SortKeyDefinition : PseudoExpression
    {
        protected Operand sortKey;
        private CachedEvaluator<IItemEvaluator> sortKeyEvaluator;
        protected Operand order;
        protected Operand dataTypeExpression = null;
        protected Operand caseOrder;
        protected Operand language;
        protected Operand collationName = null;
        protected Operand stable = null; // not actually used, but present so it can be validated
        protected IStringCollator collation;
        protected string baseURI; // needed in case collation URI is relative
        protected bool emptyLeast = true;
        protected bool backwardsCompatible = false;
        protected bool setContextForSortKey = false;
        private IAtomicComparer finalComparator = null;

        public virtual Expression SortKey => sortKey.GetChildExpression();

        // For callers that evaluate the key per item outside elaborated code (order by, merge keys).
        internal IItemEvaluator SortKeyEvaluator => CachedEvaluator<IItemEvaluator>.Get(ref sortKeyEvaluator, SortKey, e => e.ElaborateForItem());

        public virtual Operand SortKeyOperand => sortKey;

        public virtual Expression Order
        {
            get => order.GetChildExpression(); set
            {
                order.SetChildExpression(value);
            }
        }

        public virtual Expression DataTypeExpression
        {
            get => dataTypeExpression == null ? null : dataTypeExpression.GetChildExpression(); set
            {
                if (value == null)
                {
                    dataTypeExpression = null;
                }
                else
                {
                    if (dataTypeExpression == null)
                    {
                        dataTypeExpression = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                    }

                    dataTypeExpression.SetChildExpression(value);
                }
            }
        }

        public virtual Expression CaseOrder
        {
            get => caseOrder.GetChildExpression(); set
            {
                caseOrder.SetChildExpression(value);
            }
        }

        public virtual Expression Language
        {
            get => language.GetChildExpression(); set
            {
                language.SetChildExpression(value);
            }
        }

        public virtual Expression CollationNameExpression
        {
            get => collationName == null ? null : collationName.GetChildExpression(); set
            {
                if (value == null)
                {
                    collationName = null;
                }
                else
                {
                    if (collationName == null)
                    {
                        collationName = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                    }

                    collationName.SetChildExpression(value);
                }
            }
        }

        public virtual IStringCollator Collation
        {
            get => collation; set
            {
                this.collation = value;
            }
        }

        public virtual string BaseURI
        {
            get => baseURI; set
            {
                this.baseURI = value;
            }
        }

        public virtual Expression Stable
        {
            get => stable.GetChildExpression(); set
            {
                if (value == null)
                {
                    value = new StringLiteral("yes");
                }

                if (stable == null)
                {
                    stable = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                }

                stable.SetChildExpression(value);
            }
        }

        public virtual bool EmptyLeast
        {
            get => emptyLeast; set
            {
                this.emptyLeast = value;
            }
        }

        public override int ImplementationMethod => 0;

        public virtual IAtomicComparer FinalComparator
        {
            get => finalComparator; set
            {
                finalComparator = value;
            }
        }
        public SortKeyDefinition()
        {
            order = new Operand(this, new StringLiteral("ascending"), OperandRole.SINGLE_ATOMIC);
            caseOrder = new Operand(this, new StringLiteral("#default"), OperandRole.SINGLE_ATOMIC);
            language = new Operand(this, new StringLiteral(StringValue.EMPTY_STRING), OperandRole.SINGLE_ATOMIC);
        }

        public override bool IsLiftable(bool forStreaming)
        {
            return false;
        }

        public virtual void SetSortKey(Expression exp, bool setContext)
        {
            OperandRole opRole;
            if (setContext)
            {
                opRole = new OperandRole(OperandRole.HAS_SPECIAL_FOCUS_RULES | OperandRole.HIGHER_ORDER, OperandUsage.TRANSMISSION, SequenceType.ANY_SEQUENCE);
            }
            else
            {
                opRole = OperandRole.ATOMIC_SEQUENCE;
            }

            sortKey = new Operand(this, exp, opRole);
            setContextForSortKey = setContext;
        }

        public virtual bool IsSetContextForSortKey()
        {
            return setContextForSortKey;
        }

        public virtual void SetBackwardsCompatible(bool compatible)
        {
            backwardsCompatible = compatible;
        }

        public virtual bool IsBackwardsCompatible()
        {
            return backwardsCompatible;
        }

        public virtual bool IsFixed()
        {
            return order.GetChildExpression() is Literal && (dataTypeExpression == null || dataTypeExpression.GetChildExpression() is Literal) && caseOrder.GetChildExpression() is Literal && language.GetChildExpression() is Literal && (stable == null || stable.GetChildExpression() is Literal) && (collationName == null || collationName.GetChildExpression() is Literal);
        }

        public override Expression Copy(RebindingMap rm)
        {
            SortKeyDefinition sk2 = new SortKeyDefinition();
            sk2.SetSortKey(Copy(sortKey.GetChildExpression(), rm), true);
            sk2.Order = Copy(order.GetChildExpression(), rm);
            sk2.DataTypeExpression = dataTypeExpression == null ? null : Copy(dataTypeExpression.GetChildExpression(), rm);
            sk2.CaseOrder = Copy(caseOrder.GetChildExpression(), rm);
            sk2.Language = Copy(language.GetChildExpression(), rm);
            sk2.Stable = Copy(stable == null ? null : stable.GetChildExpression(), rm);
            sk2.CollationNameExpression = collationName == null ? null : Copy(collationName.GetChildExpression(), rm);
            sk2.collation = collation;
            sk2.emptyLeast = emptyLeast;
            sk2.baseURI = baseURI;
            sk2.backwardsCompatible = backwardsCompatible;
            sk2.finalComparator = finalComparator;
            sk2.setContextForSortKey = setContextForSortKey;
            return sk2;
        }

        private Expression Copy(Expression @in, RebindingMap rebindings)
        {
            return @in == null ? null : @in.Copy(rebindings);
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            foreach (Operand o in CheckedOperands())
            {
                if (o.HasSameFocus())
                {
                    o.TypeCheck(visitor, contextItemType);
                } // Otherwise rely on the containing SortExpression to type-check the sort key
            }

            Expression lang = Language;
            if (lang is StringLiteral && !((StringLiteral)lang).GetString().IsEmpty())
            {
                ValidationFailure vf = StringConverter.StringToLanguage.INSTANCE.Validate(((StringLiteral)lang).GroundedValue.UnicodeStringValue);
                if (vf != null)
                {
                    throw new XPathException("The lang attribute of xsl:sort must be a valid language code", "XTDE0030");
                }
            }

            return this;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> list = new List<Operand>(8);
            list.Add(sortKey);
            list.Add(order);
            if (dataTypeExpression != null)
            {
                list.Add(dataTypeExpression);
            }

            list.Add(caseOrder);
            list.Add(language);
            if (stable != null)
            {
                list.Add(stable);
            }

            if (collationName != null)
            {
                list.Add(collationName);
            }

            return list;
        }

        public virtual IAtomicComparer MakeComparator(IXPathContext context)
        {
            string orderX = order.GetChildExpression().EvaluateAsString(context).ToString();
            Configuration config = context.GetConfiguration();
            IAtomicComparer atomicComparer;
            IStringCollator stringCollator;
            if (collation != null)
            {
                stringCollator = collation;
            }
            else if (collationName != null)
            {
                string cname = collationName.GetChildExpression().EvaluateAsString(context).ToString();
                URI collationURI;
                try
                {
                    collationURI = new URI(cname);
                    if (!collationURI.IsAbsolute())
                    {
                        if (baseURI == null)
                        {
                            throw new XPathException("Collation URI is relative, and base URI is unknown");
                        }
                        else
                        {
                            URI @base = new URI(baseURI);
                            collationURI = @base.Resolve(collationURI);
                        }
                    }
                }
                catch (URISyntaxException err)
                {
                    throw new XPathException("Collation name " + cname + " is not a valid URI: " + err);
                }

                stringCollator = context.GetConfiguration().GetCollation(collationURI.ToString());
                if (stringCollator == null)
                {
                    throw new XPathException("Unknown collation " + collationURI, "XTDE1035");
                }
            }
            else
            {
                string caseOrderX = caseOrder.GetChildExpression().EvaluateAsString(context).ToString();
                string languageX = language.GetChildExpression().EvaluateAsString(context).ToString();
                string uri = "http://saxon.sf.net/collation";
                bool firstParam = true;
                Properties props = new Properties();
                if (!(languageX.Length == 0))
                {
                    ValidationFailure vf = StringConverter.StringToLanguage.INSTANCE.Validate(StringView.Of(languageX).Tidy());
                    if (vf != null)
                    {
                        throw new XPathException("The lang attribute of xsl:sort must be a valid language code", "XTDE0030");
                    }

                    props.SetProperty("lang", languageX);
                    uri += "?lang=" + languageX;
                    firstParam = false;
                }

                if (!caseOrderX.Equals("#default"))
                {
                    props.SetProperty("case-order", caseOrderX);
                    uri += (firstParam ? "?" : ";") + "case-order=" + caseOrderX;
                    firstParam = false;
                }

                stringCollator = Core.Version.platform.MakeCollation(config, props, uri);
            }

            if (dataTypeExpression == null)
            {
                atomicComparer = AtomicSortComparer.MakeSortComparer(stringCollator, sortKey.GetChildExpression().GetItemType().GetAtomizedItemType().PrimitiveType, context);
                if (!emptyLeast)
                {
                    atomicComparer = (IAtomicComparer)new EmptyGreatestComparer(atomicComparer);
                }
            }
            else
            {
                string dataType = dataTypeExpression.GetChildExpression().EvaluateAsString(context).ToString();
                switch (dataType)
                {
                    case "text":
                        atomicComparer = AtomicSortComparer.MakeSortComparer(stringCollator, StandardNames.XS_STRING, context);
                        atomicComparer = new TextComparer(atomicComparer);
                        break;
                    case "number":
                        atomicComparer = context.GetConfiguration().XsdVersion == Configuration.XSD10 ? NumericComparer.GetInstance() : NumericComparer11.GetInstance();
                        break;
                    default:
                        throw new XPathException("data-type on xsl:sort must be 'text' or 'number'", "XTDE0030");
                }
            }

            if (stable != null)
            {
                StringValue stableVal = (StringValue)stable.GetChildExpression().EvaluateItem(context);
                string s = Whitespace.Trim(stableVal.GetStringValue());
                if (s.Equals("yes") || s.Equals("no") || s.Equals("true") || s.Equals("false") || s.Equals("1") || s.Equals("0"))
                {
                }
                else
                {
                    throw new XPathException("Value of 'stable' on xsl:sort must be yes|no|true|false|1|0", "XTDE0030");
                }
            }

            switch (orderX)
            {
                case "ascending":
                    return atomicComparer;
                case "descending":
                    return new DescendingComparer(atomicComparer);
                default:
                    throw new XPathException("order must be 'ascending' or 'descending'", "XTDE0030");
            }
        }

        public virtual SortKeyDefinition Fix(IXPathContext context)
        {
            SortKeyDefinition newSKD = (SortKeyDefinition)this.Copy(new RebindingMap());
            newSKD.Language = new StringLiteral(this.Language.EvaluateAsString(context));
            newSKD.Order = new StringLiteral(this.Order.EvaluateAsString(context));
            if (collationName != null)
            {
                newSKD.CollationNameExpression = new StringLiteral(this.CollationNameExpression.EvaluateAsString(context));
            }

            newSKD.CaseOrder = new StringLiteral(this.CaseOrder.EvaluateAsString(context));
            if (dataTypeExpression != null)
            {
                newSKD.DataTypeExpression = new StringLiteral(this.DataTypeExpression.EvaluateAsString(context));
            }

            newSKD.SetSortKey(new ContextItemExpression(), true);
            if (Stable != null)
            {
                newSKD.Stable = new StringLiteral(this.Stable.EvaluateAsString(context));
            }

            return newSKD;
        }

        public override bool Equals(object other)
        {
            if (other is SortKeyDefinition)
            {
                SortKeyDefinition s2 = (SortKeyDefinition)other;
                return object.Equals(SortKey, s2.SortKey) && object.Equals(Order, s2.Order) && object.Equals(Language, s2.Language) && object.Equals(DataTypeExpression, s2.DataTypeExpression) && object.Equals(Stable, s2.Stable) && object.Equals(CollationNameExpression, s2.CollationNameExpression);
            }
            else
            {
                return false;
            }
        }

        protected override int ComputeHashCode()
        {
            int h = 0;
            h ^= Order.GetHashCode();
            h ^= CaseOrder.GetHashCode();
            h ^= Language.GetHashCode();
            if (DataTypeExpression != null)
            {
                h ^= DataTypeExpression.GetHashCode();
            }

            if (Stable != null)
            {
                h ^= Stable.GetHashCode();
            }

            if (CollationNameExpression != null)
            {
                h ^= CollationNameExpression.GetHashCode();
            }

            return h;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("sortKey", this);
            if (finalComparator != null)
            {
                @out.EmitAttribute("comp", finalComparator.Save());
            }

            @out.SetChildRole("select");
            sortKey.GetChildExpression().Export(@out);
            @out.SetChildRole("order");
            order.GetChildExpression().Export(@out);
            if (dataTypeExpression != null)
            {
                @out.SetChildRole("dataType");
                dataTypeExpression.GetChildExpression().Export(@out);
            }

            @out.SetChildRole("lang");
            language.GetChildExpression().Export(@out);
            @out.SetChildRole("caseOrder");
            caseOrder.GetChildExpression().Export(@out);
            if (stable != null)
            {
                @out.SetChildRole("stable");
                stable.GetChildExpression().Export(@out);
            }

            if (collationName != null)
            {
                @out.SetChildRole("collation");
                collationName.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }
    }
}
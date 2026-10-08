////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Numbering;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Globalization;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class NumberSequenceFormatter : Expression
    {
        private readonly Operand valueOp;
        private readonly Operand formatOp;
        private readonly Operand groupSizeOp;
        private readonly Operand groupSeparatorOp;
        private readonly Operand letterValueOp;
        private readonly Operand ordinalOp;
        private readonly Operand startAtOp;
        private readonly Operand langOp;
        private readonly NumberFormatter formatter;
        private INumberer numberer;
        private readonly bool backwardsCompatible;

        public override int ImplementationMethod => EVALUATE_METHOD;
        public NumberSequenceFormatter(Expression value, Expression format, Expression groupSize, Expression groupSeparator, Expression letterValue, Expression ordinal, Expression startAt, Expression lang, NumberFormatter formatter, bool backwardsCompatible)
        {
            if (value != null)
            {
                valueOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
            }

            if (format != null)
            {
                formatOp = new Operand(this, format, OperandRole.SINGLE_ATOMIC);
            }

            if (groupSize != null)
            {
                groupSizeOp = new Operand(this, groupSize, OperandRole.SINGLE_ATOMIC);
            }

            if (groupSeparator != null)
            {
                groupSeparatorOp = new Operand(this, groupSeparator, OperandRole.SINGLE_ATOMIC);
            }

            if (letterValue != null)
            {
                letterValueOp = new Operand(this, letterValue, OperandRole.SINGLE_ATOMIC);
            }

            if (ordinal != null)
            {
                ordinalOp = new Operand(this, ordinal, OperandRole.SINGLE_ATOMIC);
            }


            //if (startAt != null) {
            startAtOp = new Operand(this, startAt, OperandRole.SINGLE_ATOMIC);

            //}
            if (lang != null)
            {
                langOp = new Operand(this, lang, OperandRole.SINGLE_ATOMIC);
            }

            this.formatter = formatter;
            this.backwardsCompatible = backwardsCompatible;
            if (formatter == null && format is StringLiteral)
            {
                this.formatter = new NumberFormatter();
                this.formatter.Prepare(((StringLiteral)format).Stringify());
            }
        }

        //}
        public override Expression Simplify()
        {
            if (valueOp != null && !valueOp.GetChildExpression().GetItemType().IsPlainType())
            {
                valueOp.SetChildExpression(Atomizer.MakeAtomizer(valueOp.GetChildExpression(), null));
            }

            PreallocateNumberer(GetConfiguration());
            return base.Simplify();
        }

        public void PreallocateNumberer(Configuration config)
        {
            if (langOp == null)
            {
                numberer = config.MakeNumberer(null, null);
            }
            else
            {
                if (langOp.GetChildExpression() is StringLiteral)
                {
                    string language = ((StringLiteral)langOp.GetChildExpression()).Stringify();
                    if (!(language.Length == 0))
                    {
                        ValidationFailure vf = StringConverter.StringToLanguage.INSTANCE.Validate(StringView.Tidy(language));
                        if (vf != null)
                        {
                            langOp.SetChildExpression(new StringLiteral(StringValue.EMPTY_STRING));
                            throw new XPathException("The lang attribute must be a valid language code", "XTDE0030").WithLocation(GetLocation());
                        }
                    }

                    numberer = config.MakeNumberer(language, null);
                } // else we allocate a numberer at run-time
            }
        }

        //}
        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(valueOp, formatOp, groupSizeOp, groupSeparatorOp, letterValueOp, ordinalOp, startAtOp, langOp);
        }

        private bool IsFixed(Operand op)
        {
            return op == null || op.GetChildExpression() is Literal;
        }

        private bool HasFixedOperands()
        {
            foreach (Operand o in Operands())
            {
                if (!IsFixed(o))
                {
                    return false;
                }
            }

            return true;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            OptimizeChildren(visitor, contextInfo);
            if (HasFixedOperands())
            {
                StringValue val = (StringValue)EvaluateItem(visitor.MakeDynamicContext());
                StringLiteral literal = new StringLiteral(val);
                ExpressionTool.CopyLocationInfo(this, literal);
                return literal;
            }
            else
            {
                return this;
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NumberSequenceFormatter exp = new NumberSequenceFormatter(Copy(valueOp, rebindings), Copy(formatOp, rebindings), Copy(groupSizeOp, rebindings), Copy(groupSeparatorOp, rebindings), Copy(letterValueOp, rebindings), Copy(ordinalOp, rebindings), Copy(startAtOp, rebindings), Copy(langOp, rebindings), formatter, backwardsCompatible);
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        private Expression Copy(Operand op, RebindingMap rebindings)
        {
            return op == null ? null : op.GetChildExpression().Copy(rebindings);
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.STRING;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override IItem EvaluateItem(IXPathContext context) /*Java covariant StringValue widened (C# 7.3)*/
        {
            UnicodeString s = MakeElaborator().ElaborateForUnicodeString(true).Eval(context);
            return new StringValue(s);
        }

        public IList<int> ParseStartAtValue(string value)
        {
            IList<int> list = new List<int>();
            string[] tokens = value.SplitRegex("\\s+");
            foreach (string tok in tokens)
            {
                try
                {
                    int n = int.Parse(tok, CultureInfo.InvariantCulture);
                    list.Add(n);
                }
                catch (Exception err) when (err is FormatException || err is OverflowException)
                {
                    throw new XPathException("Invalid start-at value: non-integer component {" + tok + "}").WithErrorCode("XTDE0030").WithLocation(GetLocation());
                }
            }

            if (list.Count == 0)
            {
                throw new XPathException("Invalid start-at value: no numeric components found").WithErrorCode("XTDE0030").WithLocation(GetLocation());
            }

            return list;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("numSeqFmt", this);
            string flags = "";
            if (backwardsCompatible)
            {
                flags += "1";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            if (valueOp != null)
            {
                @out.SetChildRole("value");
                valueOp.GetChildExpression().Export(@out);
            }

            if (formatOp != null)
            {
                @out.SetChildRole("format");
                formatOp.GetChildExpression().Export(@out);
            }

            if (startAtOp != null)
            {
                @out.SetChildRole("startAt");
                startAtOp.GetChildExpression().Export(@out);
            }

            if (langOp != null)
            {
                @out.SetChildRole("lang");
                langOp.GetChildExpression().Export(@out);
            }

            if (ordinalOp != null)
            {
                @out.SetChildRole("ordinal");
                ordinalOp.GetChildExpression().Export(@out);
            }

            if (groupSeparatorOp != null)
            {
                @out.SetChildRole("gpSep");
                groupSeparatorOp.GetChildExpression().Export(@out);
            }

            if (groupSizeOp != null)
            {
                @out.SetChildRole("gpSize");
                groupSizeOp.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new NumberSequenceFormatterElaborator();
        }

        private sealed class NumberSequenceFormatterElaborator : StringElaborator
        {
            public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
            {
                NumberSequenceFormatter expr = (NumberSequenceFormatter)GetExpression();
                IStringEvaluator startAtEvaluator = expr.startAtOp.GetChildExpression().MakeElaborator().ElaborateForString(true);
                IPullEvaluator valueEvaluator = expr.valueOp.GetChildExpression().MakeElaborator().ElaborateForPull();
                IStringEvaluator groupSizeEvaluator = expr.groupSizeOp == null ? null : expr.groupSizeOp.GetChildExpression().MakeElaborator().ElaborateForString(true);
                IStringEvaluator groupSeparatorEvaluator = expr.groupSeparatorOp == null ? null : expr.groupSeparatorOp.GetChildExpression().MakeElaborator().ElaborateForString(true);
                IStringEvaluator langEvaluator = expr.langOp == null ? null : expr.langOp.GetChildExpression().MakeElaborator().ElaborateForString(true);
                IStringEvaluator ordinalEvaluator = expr.ordinalOp == null ? null : expr.ordinalOp.GetChildExpression().MakeElaborator().ElaborateForString(true);
                IStringEvaluator letterValueEvaluator = expr.letterValueOp == null ? null : expr.letterValueOp.GetChildExpression().MakeElaborator().ElaborateForString(true);
                IStringEvaluator formatEvaluator = expr.formatter != null ? null : expr.formatOp.GetChildExpression().MakeElaborator().ElaborateForString(true);
                // start-at is nearly always a literal ("1"): ParseStartAtValue runs a regex split
                // per instruction execution, which dominated per-node xsl:number. One-entry memo
                // keyed by the evaluated string; the tuple is immutable, so the racy publish is
                // benign and the parsed list is never mutated after creation.
                Tuple<string, IList<int>> startAtMemo = null;
                return (context) =>
                {
                    IList<object> vec = new List<object>(4); // a list whose items may be of type either Long or

                    // BigInteger or the string to be output (e.g. "NaN")
                    ConversionRules rules = context.GetConfiguration().GetConversionRules();
                    string startAv = startAtEvaluator.Eval(context);
                    Tuple<string, IList<int>> memo = startAtMemo;
                    IList<int> startValues;
                    if (memo != null && string.Equals(memo.Item1, startAv, StringComparison.Ordinal))
                    {
                        startValues = memo.Item2;
                    }
                    else
                    {
                        startValues = expr.ParseStartAtValue(startAv);
                        startAtMemo = Tuple.Create(startAv, startValues);
                    }
                    ISequenceIterator iter = valueEvaluator.Iterate(context);
                    AtomicValue val;
                    int pos = 0;
                    while ((val = (AtomicValue)iter.Next()) != null)
                    {
                        if (expr.backwardsCompatible && vec.Count > 0)
                        {
                            break;
                        }

                        int startValue = startValues.Count > pos ? startValues[pos] : startValues[startValues.Count - 1];
                        pos++;
                        try
                        {
                            // xsl:number value="position()" and friends: a plain integer needs no
                            // rounding, no MAX_LONG probe, no converter — the long IS the number.
                            // Negatives fall to the same catch → XTDE0980 as the generic path.
                            if (val is Int64Value plain)
                            {
                                long iv = plain.LongValue();
                                if (iv < 0)
                                {
                                    throw new XPathException("The numbers to be formatted must not be negative");
                                }

                                vec.Add(iv + (startValue - 1));
                                continue;
                            }

                            NumericValue num;
                            if (val is NumericValue)
                            {
                                num = (NumericValue)val;
                            }
                            else
                            {
                                num = Number_1.Convert(val, context.GetConfiguration());
                            }

                            if (num.IsNaN())
                            {
                                throw new XPathException("NaN"); // thrown to be caught
                            }

                            num = num.Round(0);
                            if (num.CompareTo(Int64Value.MAX_LONG) > 0)
                            {
                                BigInteger bi = ((BigIntegerValue)((AtomicValue)Converter.Convert(num, BuiltInAtomicType.INTEGER, rules)).AsAtomic()).AsBigInteger();
                                if (startValue != 1)
                                {
                                    bi = bi + new BigInteger(startValue - 1);
                                }

                                vec.Add(bi);
                            }
                            else
                            {
                                if (num.CompareTo(Int64Value.ZERO) < 0)
                                {
                                    throw new XPathException("The numbers to be formatted must not be negative"); // thrown to be caught
                                }

                                long i = ((NumericValue)((AtomicValue)Converter.Convert(num, BuiltInAtomicType.INTEGER, rules)).AsAtomic()).LongValue();
                                i += startValue - 1;
                                vec.Add(i);
                            }
                        }
                        catch (XPathException err)
                        {
                            if (expr.backwardsCompatible)
                            {
                                vec.Add("NaN");
                            }
                            else
                            {
                                vec.Add(val.UnicodeStringValue);
                                throw new XPathException("Cannot convert supplied value to an integer. " + err.Message).WithErrorCode("XTDE0980").WithLocation(expr.GetLocation()).WithXPathContext(context);
                            }
                        }
                    }

                    if (expr.backwardsCompatible && vec.Count == 0)
                    {
                        vec.Add("NaN");
                    }

                    int gpsize = 0;
                    string gpseparator = "";
                    string letterVal;
                    string ordinalVal = null;
                    if (groupSizeEvaluator != null)
                    {
                        string g = groupSizeEvaluator.Eval(context);
                        try
                        {
                            gpsize = int.Parse(g, CultureInfo.InvariantCulture);
                        }
                        catch (Exception err) when (err is FormatException || err is OverflowException)
                        {
                            throw new XPathException("grouping-size must be numeric").WithXPathContext(context).WithErrorCode("XTDE0030").WithLocation(expr.GetLocation());
                        }
                    }

                    if (groupSeparatorEvaluator != null)
                    {
                        gpseparator = groupSeparatorEvaluator.Eval(context);
                    }

                    if (ordinalEvaluator != null)
                    {
                        ordinalVal = ordinalEvaluator.Eval(context);
                    }


                    // Use the numberer decided at compile time if possible; otherwise try to get it from
                    // a table of numberers indexed by language; if not there, load the relevant class and
                    // add it to the table.
                    INumberer numb = expr.numberer;
                    if (numb == null)
                    {
                        if (langEvaluator == null)
                        {
                            numb = context.GetConfiguration().MakeNumberer(null, null);
                        }
                        else
                        {
                            string language = langEvaluator.Eval(context);
                            ValidationFailure vf = StringConverter.StringToLanguage.INSTANCE.Validate(StringView.Tidy(language));
                            if (vf != null)
                            {
                                throw new XPathException("The lang attribute of xsl:number must be a valid language code", "XTDE0030");
                            }

                            numb = context.GetConfiguration().MakeNumberer(language, null);
                        }
                    }

                    if (letterValueEvaluator == null)
                    {
                        letterVal = "";
                    }
                    else
                    {
                        letterVal = letterValueEvaluator.Eval(context).ToString();
                        if (!(letterVal == "alphabetic" || letterVal == "traditional"))
                        {
                            throw new XPathException("letter-value must be \"traditional\" or \"alphabetic\"").WithXPathContext(context).WithErrorCode("XTDE0030").WithLocation(expr.GetLocation());
                        }
                    }

                    NumberFormatter nf;
                    if (expr.formatter == null)
                    {

                        // format not known until run-time
                        nf = new NumberFormatter();
                        nf.Prepare(formatEvaluator.Eval(context));
                    }
                    else
                    {
                        nf = expr.formatter;
                    }

                    return nf.Format(vec, gpsize, gpseparator, letterVal, ordinalVal, numb);
                };
            }
        }
    }
}

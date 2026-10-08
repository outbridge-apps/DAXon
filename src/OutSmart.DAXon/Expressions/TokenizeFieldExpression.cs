////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Globalization;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// The fused form of <c>tokenize($in, single-char-literal)[N]</c>: instead of building a
    /// SingleCharTokenIterator and materializing tokens 1..N (N-1 of them discarded), it scans the
    /// input once for the (N-1)th and Nth separator and returns just field N as a zero-copy slice.
    /// The base operand is the tokenize input expression; the separator codepoint and field position
    /// are compile-time constants. Byte-identical to the tokenize + subscript it replaces (same gate
    /// as SingleCharTokenIterator, same Token() slicing, same empty-input / short-field semantics).
    /// The heavy caller is `unparsed-text-lines(...) ! tokenize(.,";")[k]` (csv sort / group over
    /// millions of lines), where the per-line iterator + discarded-token allocations dominate.
    /// </summary>
    internal sealed class TokenizeFieldExpression : SingleItemFilter
    {
        private readonly int separator;   // separator codepoint
        private readonly int field;       // 1-based field position N

        public TokenizeFieldExpression(Expression input, int separator, int field) : base(input)
        {
            this.separator = separator;
            this.field = field;
        }

        public override int ImplementationMethod => EVALUATE_METHOD;
        public override string ExpressionName => "tokenizeField";
        public override string StreamerName => "TokenizeFieldExpression";

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.STRING;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            // Only optimize the operand -- do NOT fall into SingleItemFilter.Optimize, which drops the
            // filter wrapper when the base cardinality is not "many" (our base is a single string).
            GetOperand().Optimize(visitor, contextInfo);
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            TokenizeFieldExpression e = new TokenizeFieldExpression(BaseExpression.Copy(rebindings), separator, field);
            ExpressionTool.CopyLocationInfo(this, e);
            return e;
        }

        public override bool Equals(object other)
        {
            return other is TokenizeFieldExpression o && o.separator == separator && o.field == field
                && BaseExpression.IsEqual(o.BaseExpression);
        }

        protected override int ComputeHashCode()
        {
            return BaseExpression.GetHashCode() ^ (separator * 31 + field);
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("tokenizeField", this);
            destination.EmitAttribute("sep", separator.ToString(CultureInfo.InvariantCulture));
            destination.EmitAttribute("field", field.ToString(CultureInfo.InvariantCulture));
            BaseExpression.Export(destination);
            destination.EndElement();
        }

        public override string ToString()
        {
            return "tokenize-field(" + BaseExpression + ", " + separator + ", " + field + ")";
        }

        public override string ToShortString()
        {
            return "tokenize-field(" + BaseExpression.ToShortString() + ", " + separator + ", " + field + ")";
        }

        public override Elaborator GetElaborator()
        {
            return new TokenizeFieldElaborator();
        }

        // Try to rewrite subscript(base, index) as a fused field extractor. Succeeds only when base is
        // tokenize(x, single-char-literal[, flags]) with a compile-time single-codepoint separator
        // (the exact SingleCharTokenIterator gate) and index is a positive integer literal.
        internal static Expression TryFuse(Expression baseExpr, Expression index)
        {
            if (!(index is Literal indexLit) || !(indexLit.GroundedValue is IntegerValue iv))
            {
                return null;
            }

            int n = iv.AsSubscript();
            if (n < 1)
            {
                return null;
            }

            if (!(baseExpr is SystemFunctionCall sfc) || !(sfc.TargetFunction is RegexFunction rf))
            {
                return null;
            }

            if (rf.GetFunctionName().GetLocalPart() != "tokenize" || !(rf.StaticRegex is ARegularExpression are))
            {
                return null;
            }

            int sep = are.SingleCharLiteral();
            if (sep < 0)
            {
                return null;
            }

            return new TokenizeFieldExpression(sfc.GetArg(0), sep, n);
        }

        // Field N of the single-char tokenization of s, or null (empty sequence). Mirrors draining a
        // SingleCharTokenIterator and taking the Nth token: empty input yields no tokens (-> null),
        // otherwise fields are the codepoint ranges between separators, N past the last -> null.
        internal static IItem Extract(IItem input, int separator, int field)
        {
            AtomicValue in0 = (AtomicValue)input;
            if (in0 == null)
            {
                return null;   // tokenize(()) = () -> [N] = ()
            }

            UnicodeString s = in0.UnicodeStringValue;
            if (s.IsEmpty())
            {
                return null;   // tokenize("") = () -> [N] = ()
            }

            long len = s.Length();
            int prevEnd = 0;
            int f = 1;
            while (true)
            {
                long sep = s.IndexOf(separator, prevEnd);
                long to = sep >= 0 ? sep : len;
                if (f == field)
                {
                    return new StringValue(Token(s, prevEnd, to));
                }

                if (sep < 0)
                {
                    return null;   // fewer than N fields
                }

                prevEnd = (int)sep + 1;
                f++;
            }
        }

        // Byte-identical to SingleCharTokenIterator.Token: a BMP token is a zero-copy view of the
        // input line rather than a copied substring.
        private static UnicodeString Token(UnicodeString input, int from, long to)
        {
            if (input is BMPString b)
            {
                string str = b.ToString();
                return from == 0 && to == str.Length ? input : new BMPSlice(str, from, (int)to);
            }

            return input.Substring(from, to);
        }

        internal sealed class TokenizeFieldElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                TokenizeFieldExpression expr = (TokenizeFieldExpression)GetExpression();
                IItemEvaluator inEval = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                int sep = expr.separator;
                int field = expr.field;
                return (context) => Extract(inEval.Eval(context), sep, field);
            }
        }
    }
}

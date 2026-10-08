////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;

using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Subclass of Literal used specifically for string literals, as this is a common case
    /// </summary>
    internal sealed class StringLiteral : Literal
    {

        public new StringValue GroundedValue => (StringValue)base.GroundedValue;
        public StringLiteral(StringValue value) : base(value)
        {
        }

        public StringLiteral(UnicodeString value) : this(new StringValue(value))
        {
        }

        public StringLiteral(string value) : this(new StringValue(StringTool.FromCharSequence(value)))
        {
        }

        public UnicodeString GetString()
        {
            return GroundedValue.UnicodeStringValue;
        }

        public string Stringify()
        {
            return GroundedValue.GetStringValue();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            StringLiteral stringLiteral = new StringLiteral(GroundedValue);
            ExpressionTool.CopyLocationInfo(this, stringLiteral);
            return stringLiteral;
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System.Globalization;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the function fn:codepoints-to-string()
    /// </summary>
    internal sealed class CodepointsToString : SystemFunction, ICallable
    {

        public override string StreamerName => "CodepointsToString";
        public static StringValue UnicodeToString(ISequenceIterator chars, IIntPredicateProxy checker)
        {
            UnicodeBuilder sb = new UnicodeBuilder();
            while (true)
            {
                NumericValue nextInt = (NumericValue)chars.Next();
                if (nextInt == null)
                {
                    return new StringValue(sb.ToUnicodeString());
                }

                long next = nextInt.LongValue();
                if (next < 0 || next > int.MaxValue || !checker.Test((int)next))
                {
                    throw new XPathException("codepoints-to-string(): invalid XML character [x" + ((int)next).ToString("x", CultureInfo.InvariantCulture) + ']', "FOCH0001");
                }

                sb.Append((int)next);
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequenceIterator chars = arguments[0].Iterate();
            return UnicodeToString(chars, context.GetConfiguration().ValidCharacterChecker);
        }
        ISequence ICallable.Call(IXPathContext arg0, ISequence[] arg1) => Call(arg0, arg1);
    }
}


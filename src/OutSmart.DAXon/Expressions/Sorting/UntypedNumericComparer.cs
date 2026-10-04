////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Core;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class UntypedNumericComparer : IAtomicComparer
    {
        private static readonly double[][] bounds = new double[][]
        {
            new double[]
            {
                1,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0
            },
            new double[]
            {
                1,
                1,
                10,
                100,
                1000,
                10000,
                100000,
                1000000,
                10000000,
                100000000,
                1000000000,
                10000000000
            },
            new double[]
            {
                1,
                2,
                20,
                200,
                2000,
                20000,
                200000,
                2000000,
                20000000,
                200000000,
                2000000000,
                20000000000
            },
            new double[]
            {
                1,
                3,
                30,
                300,
                3000,
                30000,
                300000,
                3000000,
                30000000,
                300000000,
                3000000000,
                30000000000
            },
            new double[]
            {
                1,
                4,
                40,
                400,
                4000,
                40000,
                400000,
                4000000,
                40000000,
                400000000,
                4000000000,
                40000000000
            },
            new double[]
            {
                1,
                5,
                50,
                500,
                5000,
                50000,
                500000,
                5000000,
                50000000,
                500000000,
                5000000000,
                50000000000
            },
            new double[]
            {
                1,
                6,
                60,
                600,
                6000,
                60000,
                600000,
                6000000,
                60000000,
                600000000,
                6000000000,
                60000000000
            },
            new double[]
            {
                1,
                7,
                70,
                700,
                7000,
                70000,
                700000,
                7000000,
                70000000,
                700000000,
                7000000000,
                70000000000
            },
            new double[]
            {
                1,
                8,
                80,
                800,
                8000,
                80000,
                800000,
                8000000,
                80000000,
                800000000,
                8000000000,
                80000000000
            },
            new double[]
            {
                1,
                9,
                90,
                900,
                9000,
                90000,
                900000,
                9000000,
                90000000,
                900000000,
                9000000000,
                90000000000
            },
            new double[]
            {
                1,
                10,
                100,
                1000,
                10000,
                100000,
                1000000,
                10000000,
                100000000,
                1000000000,
                10000000000,
                100000000000
            }
        };
        private ConversionRules rules = ConversionRules.DEFAULT;

        public IStringCollator Collator => null;
        public static bool QuickCompare(StringValue a0, NumericValue a1, int @operator, ConversionRules rules)
        {
            if (a1.IsNaN())
            {
                return @operator == Token.FNE;
            }

            int comp = QuickComparison(a0, a1, rules);
            switch (@operator)
            {
                case Token.FEQ:
                    return comp == 0;
                case Token.FLE:
                    return comp <= 0;
                case Token.FLT:
                    return comp < 0;
                case Token.FGE:
                    return comp >= 0;
                case Token.FGT:
                    return comp > 0;
                case Token.FNE:
                default:
                    return comp != 0;
            }
        }

        private static int QuickComparison(StringValue a0, NumericValue a1, ConversionRules rules)
        {
            double d1 = a1.GetDoubleValue();
            UnicodeString cs = Whitespace.Trim(a0.UnicodeStringValue);
            bool simple = true;
            int wholePartLength = 0;
            int firstDigit = -1;
            int decimalPoints = 0;
            int sign = '?';
            for (int i = 0; i < cs.Length(); i++)
            {
                int c = cs.CodePointAt(i);
                if (c >= '0' && c <= '9')
                {
                    if (firstDigit < 0)
                    {
                        firstDigit = c - '0';
                    }

                    if (decimalPoints == 0)
                    {
                        wholePartLength++;
                    }
                }
                else if (c == '-')
                {
                    if (sign != '?' || wholePartLength > 0 || decimalPoints > 0)
                    {
                        simple = false;
                        break;
                    }

                    sign = c;
                }
                else if (c == '.')
                {
                    if (decimalPoints > 0)
                    {
                        simple = false;
                        break;
                    }

                    decimalPoints = 1;
                }
                else
                {
                    simple = false;
                    break;
                }
            }

            if (firstDigit < 0)
            {
                simple = false;
            }

            if (simple && wholePartLength > 0 && wholePartLength <= 10)
            {
                double lowerBound = bounds[firstDigit][wholePartLength];
                double upperBound = bounds[firstDigit + 1][wholePartLength];
                if (sign == '-')
                {
                    double temp = lowerBound;
                    lowerBound = -upperBound;
                    upperBound = -temp;
                }

                if (upperBound < d1)
                {
                    return -1;
                }

                if (lowerBound > d1)
                {
                    return +1;
                }
            }


            // The quick check was inconclusive, so we now parse the number.
            // We use integer comparison if both sides are simple integers, or double comparison otherwise
            if (simple && decimalPoints == 0 && wholePartLength <= 15 && a1 is Int64Value)
            {
                long l0 = long.Parse(cs.ToString(), CultureInfo.InvariantCulture);
                return l0.CompareTo(a1.LongValue());
            }
            else
            {
                IConversionResult result;
                lock (a0)
                {
                    result = BuiltInAtomicType.DOUBLE.GetStringConverter(rules).ConvertString(a0.UnicodeStringValue);
                }

                AtomicValue av = result.AsAtomic();
                return CompareDoublesTotalOrder(((DoubleValue)av).GetDoubleValue(), d1);
            }
        }

        // Total-order double comparison matching java.lang.Double.compare: NaN sorts
        // above +INF, and -0.0 sorts below +0.0 (unlike double.CompareTo / the &lt;,&gt; operators).
        private static int CompareDoublesTotalOrder(double a, double b)
        {
            if (a < b)
                return -1;
            if (a > b)
                return 1;
            long ab = double.IsNaN(a) ? 0x7ff8000000000000L : BitConverter.DoubleToInt64Bits(a);
            long bb = double.IsNaN(b) ? 0x7ff8000000000000L : BitConverter.DoubleToInt64Bits(b);
            return ab == bb ? 0 : (ab < bb ? -1 : 1);
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            try
            {
                return QuickComparison((StringValue)a, (NumericValue)b, rules);
            }
            catch (XPathException e)
            {
                throw new ComparisonException(e);
            }
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            rules = context.GetConfiguration().GetConversionRules();
            return this;
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }

        public string Save()
        {
            return "QUNC";
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Values
{
    internal sealed class Cardinality
    {

        public static bool AllowsMany(int cardinality)
        {
            return (cardinality & StaticProperty.ALLOWS_MANY) != 0;
        }

        public static bool Allows(int cardinality, int count)
        {
            if (count == 0)
            {
                return AllowsZero(cardinality);
            }
            else if (count == 1)
            {
                return (cardinality & StaticProperty.ALLOWS_ONE) != 0;
            }
            else
            {
                return AllowsMany(cardinality);
            }
        }

        public static bool AllowsZero(int cardinality)
        {
            return (cardinality & StaticProperty.ALLOWS_ZERO) != 0;
        }

        public static int Union(int c1, int c2)
        {
            int r = c1 | c2;

            // eliminate disallowed options
            if (r == (StaticProperty.ALLOWS_MANY | StaticProperty.ALLOWS_ZERO))
            {
                r = StaticProperty.ALLOWS_ZERO_OR_MORE;
            }

            return r;
        }

        public static int Sum(int c1, int c2)
        {
            int mini = Min(c1) + Min(c2);
            int maxi = Max(c1) + Max(c2);
            return FromMinAndMax(mini, maxi);
        }

        static int Min(int cardinality)
        {
            if (AllowsZero(cardinality))
            {
                return 0;
            }
            else if (cardinality == StaticProperty.ALLOWS_MANY)
            {
                return 2;
            }
            else
            {
                return 1;
            }
        }

        static int Max(int cardinality)
        {
            if (AllowsMany(cardinality))
            {
                return 2;
            }
            else if (cardinality == StaticProperty.ALLOWS_ZERO)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        static int FromMinAndMax(int min, int max)
        {
            bool zero = min == 0;
            bool one = min <= 1 || max <= 1;
            bool many = max > 1;
            return (zero ? StaticProperty.ALLOWS_ZERO : 0) + (one ? StaticProperty.ALLOWS_ONE : 0) + (many ? StaticProperty.ALLOWS_MANY : 0);
        }

        public static bool Subsumes(int c1, int c2)
        {
            return (c1 | c2) == c1;
        }

        public static int Multiply(int c1, int c2)
        {
            if (c1 == StaticProperty.EMPTY || c2 == StaticProperty.EMPTY)
            {
                return StaticProperty.EMPTY;
            }

            if (c2 == StaticProperty.EXACTLY_ONE)
            {
                return c1;
            }

            if (c1 == StaticProperty.EXACTLY_ONE)
            {
                return c2;
            }

            if (c1 == StaticProperty.ALLOWS_ZERO_OR_ONE && c2 == StaticProperty.ALLOWS_ZERO_OR_ONE)
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }

            if (c1 == StaticProperty.ALLOWS_ONE_OR_MORE && c2 == StaticProperty.ALLOWS_ONE_OR_MORE)
            {
                return StaticProperty.ALLOWS_ONE_OR_MORE;
            }

            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public static string Describe(int cardinality)
        {
            switch (cardinality)
            {
                case StaticProperty.ALLOWS_ZERO_OR_ONE:
                    return "zero or one";
                case StaticProperty.EXACTLY_ONE:
                    return "exactly one";
                case StaticProperty.ALLOWS_ZERO_OR_MORE:
                    return "zero or more";
                case StaticProperty.ALLOWS_ONE_OR_MORE:
                    return "one or more";
                case StaticProperty.EMPTY:
                    return "exactly zero";
                case StaticProperty.ALLOWS_MANY:
                    return "more than one";
                default:
                    return "code " + cardinality;
            }
        }

        public static string GetOccurrenceIndicator(int cardinality)
        {
            switch (cardinality)
            {
                case StaticProperty.ALLOWS_ZERO_OR_ONE:
                    return "?";
                case StaticProperty.EXACTLY_ONE:
                    return "";
                case StaticProperty.ALLOWS_ZERO_OR_MORE:
                    return "*";
                case StaticProperty.ALLOWS_ONE_OR_MORE:
                    return "+";
                case StaticProperty.ALLOWS_MANY:
                    return "+";
                case StaticProperty.EMPTY:
                    return "0";
                default:
                    return "*";
            }
        }
    }
}
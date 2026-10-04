////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class RoleDiagnostic
    {
        public const int FUNCTION = 0;
        public const int BINARY_EXPR = 1;
        public const int TYPE_OP = 2;
        public const int VARIABLE = 3;
        public const int INSTRUCTION = 4;
        public const int FUNCTION_RESULT = 5;
        public const int ORDER_BY = 6;
        public const int TEMPLATE_RESULT = 7;
        public const int PARAM = 8;
        public const int UNARY_EXPR = 9;
        public const int UPDATING_EXPR = 10;
        //public static final int GROUPING_KEY = 11;  // 9.8 and earlier
        public const int EVALUATE_RESULT = 12;
        public const int CONTEXT_ITEM = 13;
        public const int AXIS_STEP = 14;
        public const int OPTION = 15;
        public const int CHARACTER_MAP_EXPANSION = 16;
        public const int FOR_MEMBER = 17;
        public const int MATCH_PATTERN = 19;
        public const int MISC = 20;
        public const int DYNAMIC_FUNCTION = 21;
        private readonly int kind;
        private readonly string operation;
        private readonly int operand;
        private string errorCode = "XPTY0004"; // default error code for type errors

        public virtual string ErrorCode
        {
            get => errorCode; set
            {
                if (value != null)
                {
                    this.errorCode = value;
                }
            }
        }
        public RoleDiagnostic(int kind, string operation, int operand)
        {
            this.kind = kind;
            this.operation = operation;
            this.operand = operand;
        }

        public RoleDiagnostic(int kind, string operation, int operand, string errorCode)
        {
            this.kind = kind;
            this.operation = operation;
            this.operand = operand;
            this.errorCode = errorCode;
        }

        public virtual bool IsTypeError()
        {
            return !errorCode.StartsWith("FORG", StringComparison.Ordinal) && !errorCode.Equals("XPDY0050");
        }

        public virtual string GetMessage()
        {
            string name = operation;
            switch (kind)
            {
                case FUNCTION:
                    if (name.Equals("saxon:call") || name.Equals("saxon:apply"))
                    {
                        if (operand == 0)
                        {
                            return "target of the dynamic function call";
                        }
                        else
                        {
                            return Ordinal(operand) + " argument of the dynamic function call";
                        }
                    }
                    else
                    {
                        return Ordinal(operand + 1) + " argument of " + ((name.Length == 0) ? "the anonymous function" : name + "()");
                    }

                case BINARY_EXPR:
                    return Ordinal(operand + 1) + " operand of '" + name + '\'';
                case UNARY_EXPR:
                    return "operand of '-'";
                case TYPE_OP:
                    return "value in '" + name + "' expression";
                case VARIABLE:
                    if (name.Equals("saxon:context-item"))
                    {
                        return "context item";
                    }
                    else
                    {
                        return "value of variable $" + name;
                    }

                case INSTRUCTION:
                    int slash = name.IndexOf('/');
                    string attributeName = "";
                    if (slash >= 0)
                    {
                        attributeName = name.Substring(slash + 1);
                        name = name.Substring(0, slash);
                    }

                    return "@" + attributeName + " attribute of " + (name.Equals("LRE") ? "a literal result element" : name);
                case FUNCTION_RESULT:
                    if ((name.Length == 0))
                    {
                        return "result of the anonymous function";
                    }
                    else
                    {
                        return "result of a call to " + name;
                    }

                case TEMPLATE_RESULT:
                    return "result of template " + name;
                case ORDER_BY:
                    return Ordinal(operand + 1) + " sort key";
                case PARAM:
                    return "value of parameter $" + name;
                case UPDATING_EXPR:
                    return "value of the " + Ordinal(operand + 1) + " operand of " + name + " expression";
                case EVALUATE_RESULT:
                    return "result of the expression {" + name + "} evaluated by xsl:evaluate";
                case CONTEXT_ITEM:
                    return "context item";
                case AXIS_STEP:
                    return "context item for the " + AxisInfo.axisName[operand] + " axis";
                case OPTION:
                    return "value of the " + name + " option";
                case CHARACTER_MAP_EXPANSION:
                    return "substitute value for character '" + name + "' in the character map";
                case FOR_MEMBER:
                    return "'for member $" + name + "' expression";
                case MATCH_PATTERN:
                    return "match pattern";
                case DYNAMIC_FUNCTION:
                    return "target of a dynamic function call {" + name + "}";
                case MISC:
                    return operation;
                default:
                    return "";
            }
        }

        public virtual string ComposeRequiredMessage(ItemType requiredItemType)
        {
            return "The required item type of the " + GetMessage() + " is " + requiredItemType;
        }

        public virtual string ComposeErrorMessage(ItemType requiredItemType, ItemType suppliedItemType)
        {
            return ComposeRequiredMessage(requiredItemType) + "; supplied value has item type " + suppliedItemType;
        }

        public virtual string ComposeErrorMessage(ItemType requiredItemType, Expression supplied, TypeHierarchy th)
        {
            if (supplied is Literal)
            {
                string s = ComposeRequiredMessage(requiredItemType);
                string more = SequenceType.MakeSequenceType(requiredItemType, StaticProperty.ALLOWS_ZERO_OR_MORE).ExplainMismatch(((Literal)supplied).GroundedValue, th);
                if (more != null)
                {
                    s = s + ". " + more;
                }

                return s;
            }

            return ComposeRequiredMessage(requiredItemType) + ", but the supplied expression {" + supplied.ToShortString() + "} has item type " + supplied.GetItemType();
        }

        public virtual string ComposeErrorMessage(ItemType requiredItemType, IItem item, TypeHierarchy th)
        {
            StringBuilder message = new StringBuilder(256);
            message.Append(ComposeRequiredMessage(requiredItemType));
            message.Append("; the supplied value ");
            message.Append(Err.Depict(item));
            if ((Genre)requiredItemType.GetGenre() != (Genre)item.GetGenre())
            {
                message.Append(" is ");
                message.Append(Err.DescribeGenre(item.GetGenre()));
            }
            else
            {
                message.Append(" does not match. ");
                if (th != null)
                {
                    string more = requiredItemType.ExplainMismatch(item, th);

                    if (more != null)
                    {
                        message.Append(more);
                    }
                }
            }

            return message.ToString();
        }

        public virtual string ComposeErrorMessage(ItemType requiredItemType, UType suppliedItemType)
        {
            return ComposeRequiredMessage(requiredItemType) + "; supplied value has item type " + suppliedItemType;
        }

        public virtual string Save()
        {
            StringBuilder fsb = new StringBuilder(256);
            fsb.Append(kind + "|");
            fsb.Append(operand + "|");
            fsb.Append(errorCode.Equals("XPTY0004") ? "" : errorCode);
            fsb.Append('|');
            fsb.Append(operation);
            return fsb.ToString();
        }

        public static RoleDiagnostic Reconstruct(string @in)
        {
            int v = @in.IndexOf('|');
            int kind = int.Parse(@in.Substring(0, v), CultureInfo.InvariantCulture);
            int w = @in.IndexOf('|', v + 1);
            int operand = int.Parse(@in.Substring(v + 1, w - v - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/, CultureInfo.InvariantCulture);
            int x = @in.IndexOf('|', w + 1);
            string errorCode = @in.Substring(w + 1, x - w - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
            string operation = @in.Substring(x + 1);
            RoleDiagnostic cd = new RoleDiagnostic(kind, operation, operand);
            if (!(errorCode.Length == 0))
            {
                cd.ErrorCode = errorCode;
            }

            return cd;
        }

        public static string Ordinal(int n)
        {
            switch (n)
            {
                case 1:
                    return "first";
                case 2:
                    return "second";
                case 3:
                    return "third";
                default:
                    if (n >= 21)
                    {
                        switch (n % 10)
                        {
                            case 1:
                                return n + "st";
                            case 2:
                                return n + "nd";
                            case 3:
                                return n + "rd";
                        }
                    }

                    return n + "th";
            }
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Flwor;

using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Expressions
{
    public class OperandRole
    {
        // Bit settings in properties field
        public const int SETS_NEW_FOCUS = 1;
        public const int USES_NEW_FOCUS = 2;
        public const int HIGHER_ORDER = 4;
        public const int IN_CHOICE_GROUP = 8;
        public const int CONSTRAINED_CLASS = 16;
        public const int SINGLETON = 32; // set only where HIGHER_ORDER would otherwise imply repeated evaluation
        public const int HAS_SPECIAL_FOCUS_RULES = 64;
        public static readonly OperandRole SAME_FOCUS_ACTION = new OperandRole(0, OperandUsage.TRANSMISSION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole FOCUS_CONTROLLING_SELECT = new OperandRole(OperandRole.SETS_NEW_FOCUS, OperandUsage.INSPECTION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole FOCUS_CONTROLLED_ACTION = new OperandRole(OperandRole.USES_NEW_FOCUS | OperandRole.HIGHER_ORDER, OperandUsage.TRANSMISSION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole INSPECT = new OperandRole(0, OperandUsage.INSPECTION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole ABSORB = new OperandRole(0, OperandUsage.ABSORPTION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole REPEAT_INSPECT = new OperandRole(OperandRole.HIGHER_ORDER, OperandUsage.INSPECTION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole NAVIGATE = new OperandRole(0, OperandUsage.NAVIGATION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole REPEAT_NAVIGATE = new OperandRole(OperandRole.HIGHER_ORDER, OperandUsage.NAVIGATION, SequenceType.ANY_SEQUENCE);
        public static readonly OperandRole FLWOR_TUPLE_CONSTRAINED = new OperandRole(OperandRole.HIGHER_ORDER | OperandRole.CONSTRAINED_CLASS, OperandUsage.NAVIGATION, SequenceType.ANY_SEQUENCE, (expr) => expr is TupleExpression);
        public static readonly OperandRole SINGLE_ATOMIC = new OperandRole(0, OperandUsage.ABSORPTION, SequenceType.SINGLE_ATOMIC);
        public static readonly OperandRole ATOMIC_SEQUENCE = new OperandRole(0, OperandUsage.ABSORPTION, SequenceType.ATOMIC_SEQUENCE);
        public static readonly OperandRole CONSTRAINED_SINGLE_ATOMIC = new OperandRole(OperandRole.CONSTRAINED_CLASS, OperandUsage.ABSORPTION, SequenceType.SINGLE_ATOMIC);
        public static readonly OperandRole CONSTRAINED_ATOMIC_SEQUENCE = new OperandRole(OperandRole.CONSTRAINED_CLASS, OperandUsage.ABSORPTION, SequenceType.ATOMIC_SEQUENCE);
        public static readonly OperandRole NEW_FOCUS_ATOMIC = new OperandRole(OperandRole.USES_NEW_FOCUS | OperandRole.HIGHER_ORDER, OperandUsage.ABSORPTION, SequenceType.ATOMIC_SEQUENCE);
        public static readonly OperandRole PATTERN = new OperandRole(OperandRole.USES_NEW_FOCUS | OperandRole.HIGHER_ORDER | OperandRole.CONSTRAINED_CLASS, OperandUsage.ABSORPTION, SequenceType.ATOMIC_SEQUENCE, (expr) => expr is Patterns.Pattern);
        public int properties;
        private readonly OperandUsage usage;
        private readonly SequenceType requiredType = SequenceType.ANY_SEQUENCE;
        private Func<Expression, bool> constraint;

        public virtual Func<Expression, bool> Constraint
        {
            get => constraint; set
            {
                this.constraint = value;
            }
        }

        public virtual OperandUsage Usage => usage;
        public OperandRole(int properties, OperandUsage usage)
        {
            this.properties = properties;
            this.usage = usage;
        }

        public OperandRole(int properties, OperandUsage usage, SequenceType requiredType)
        {
            this.properties = properties;
            this.usage = usage;
            this.requiredType = requiredType;
        }

        public OperandRole(int properties, OperandUsage usage, SequenceType requiredType, Func<Expression, bool> constraint)
        {
            this.properties = properties;
            this.usage = usage;
            this.requiredType = requiredType;
            this.constraint = constraint;
        }

        public virtual OperandRole WithConstraint(Func<Expression, bool> constraint)
        {
            return new OperandRole(properties, usage, requiredType, constraint);
        }

        public virtual OperandRole WithConstrainedClass()
        {
            return new OperandRole(properties | CONSTRAINED_CLASS, usage, requiredType, constraint);
        }

        public virtual bool SetsNewFocus()
        {
            return (properties & SETS_NEW_FOCUS) != 0;
        }

        public virtual bool HasSameFocus()
        {
            return (properties & (USES_NEW_FOCUS | HAS_SPECIAL_FOCUS_RULES)) == 0;
        }

        public virtual bool HasSpecialFocusRules()
        {
            return (properties & HAS_SPECIAL_FOCUS_RULES) != 0;
        }

        public virtual bool IsHigherOrder()
        {
            return (properties & HIGHER_ORDER) != 0;
        }

        public virtual bool IsEvaluatedRepeatedly()
        {
            return ((properties & HIGHER_ORDER) != 0) && ((properties & SINGLETON) == 0);
        }

        public virtual bool IsConstrainedClass()
        {
            return (properties & CONSTRAINED_CLASS) != 0;
        }

        public virtual SequenceType GetRequiredType()
        {
            return requiredType;
        }

        public virtual bool IsInChoiceGroup()
        {
            return (properties & IN_CHOICE_GROUP) != 0;
        }

        public static OperandUsage GetTypeDeterminedUsage(ItemType type)
        {
            if (type is IFunctionItemType)
            {
                return OperandUsage.INSPECTION;
            }
            else if (type is IPlainType)
            {
                return OperandUsage.ABSORPTION;
            }
            else
            {
                return OperandUsage.NAVIGATION;
            }
        }

        public virtual OperandRole ModifyProperty(int property, bool on)
        {
            int newProp = on ? (properties | property) : (properties & ~property);
            return new OperandRole(newProp, usage, requiredType);
        }

        public virtual int GetProperties()
        {
            return properties;
        }
    }
}
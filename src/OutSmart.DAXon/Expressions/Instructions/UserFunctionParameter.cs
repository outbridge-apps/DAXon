////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Run-time object representing a formal argument to a user-defined function
    /// </summary>
    public class UserFunctionParameter : ILocalBinding
    {
        private SequenceType requiredType;
        private StructuredQName variableQName;
        private int slotNumber;
        private readonly int referenceCount = 999;
        private bool isIndexed;
        private bool isRequiredParam = true;
        private FunctionStreamability functionStreamability = FunctionStreamability.UNCLASSIFIED;
        private Expression defaultValue; // In 4.0, function parameters can have a default value

        public virtual int LocalSlotNumber => slotNumber;

        public virtual IntegerValue[] IntegerBoundsForVariable => null;

        public virtual Expression DefaultValueExpression
        {
            get => defaultValue; set
            {
                this.defaultValue = value;
            }
        }

        public virtual int ReferenceCount => referenceCount;

        public virtual FunctionStreamability FunctionStreamability
        {
            get => functionStreamability; set
            {
                this.functionStreamability = value;
            }
        }
        public UserFunctionParameter()
        {
        }

        public bool IsGlobal()
        {
            return false;
        }

        public bool IsAssignable()
        {
            return false;
        }

        public virtual void SetRequired(bool required)
        {
            isRequiredParam = required;
        }

        public bool IsRequired()
        {
            return isRequiredParam;
        }

        public virtual void SetSlotNumber(int slot)
        {
            slotNumber = slot;
        }

        public virtual void SetRequiredType(SequenceType type)
        {
            requiredType = type;
        }

        public virtual SequenceType GetRequiredType()
        {
            return requiredType;
        }

        public virtual void SetVariableQName(StructuredQName name)
        {
            variableQName = name;
        }

        public virtual StructuredQName GetVariableQName()
        {
            return variableQName;
        }

        public virtual void AddReference(VariableReference @ref, bool isLoopingReference)
        {
        }

        public virtual void SetIndexedVariable(bool indexed)
        {
            isIndexed = indexed;
        }

        public virtual void SetIndexedVariable()
        {
            SetIndexedVariable(true);
        }

        public virtual bool IsIndexedVariable()
        {
            return isIndexed;
        }

        public virtual ISequence EvaluateVariable(IXPathContext context)
        {
            return context.EvaluateLocalVariable(slotNumber);
        }
    }
}
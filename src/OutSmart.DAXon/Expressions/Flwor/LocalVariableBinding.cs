////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    public class LocalVariableBinding : ILocalBinding
    {
        private StructuredQName variableName;
        private SequenceType requiredType;
        private int slotNumber = -999;
        private int refCount;

        public virtual IntegerValue[] IntegerBoundsForVariable => null;

        public virtual int NominalReferenceCount => refCount;

        public virtual int LocalSlotNumber => slotNumber;
        public LocalVariableBinding(StructuredQName name, SequenceType type)
        {
            variableName = name;
            requiredType = type;
        }

        public virtual LocalVariableBinding Copy()
        {
            LocalVariableBinding lb2 = new LocalVariableBinding(variableName, requiredType);
            lb2.slotNumber = slotNumber;
            lb2.refCount = refCount;
            return lb2;
        }

        public virtual StructuredQName GetVariableQName()
        {
            return variableName;
        }

        public virtual void SetRequiredType(SequenceType type)
        {
            requiredType = type;
        }

        public virtual SequenceType GetRequiredType()
        {
            return requiredType;
        }

        public virtual void AddReference(VariableReference @ref, bool isLoopingReference)
        {
            if (refCount != FilterExpression.FILTERED)
            {
                refCount += (isLoopingReference ? 10 : 1);
            }
        }

        public virtual void SetIndexedVariable()
        {
            refCount = FilterExpression.FILTERED;
        }

        public virtual bool IsIndexedVariable()
        {
            return refCount == FilterExpression.FILTERED;
        }

        public virtual void SetVariableQName(StructuredQName variableName)
        {
            this.variableName = variableName;
        }

        public virtual void SetSlotNumber(int nr)
        {
            slotNumber = nr;
        }

        public virtual ISequence EvaluateVariable(IXPathContext context)
        {
            return context.EvaluateLocalVariable(slotNumber);
        }

        public virtual bool IsAssignable()
        {
            return false;
        }

        public virtual bool IsGlobal()
        {
            return false;
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.XPath
{
    public sealed class XPathVariable : ILocalBinding
    {
        private StructuredQName name;
        private SequenceType requiredType = SequenceType.ANY_SEQUENCE;
        private ISequence defaultValue;
        private int slotNumber;

        public IntegerValue[] IntegerBoundsForVariable => null;

        public int LocalSlotNumber => slotNumber;

        public ISequence DefaultValue { get => defaultValue; set => this.defaultValue = value; }
        private XPathVariable()
        {
        }

        public static XPathVariable Make(StructuredQName name)
        {
            XPathVariable v = new XPathVariable();
            v.name = name;
            return v;
        }

        public bool IsGlobal()
        {
            return false;
        }

        public bool IsAssignable()
        {
            return false;
        }

        public void SetRequiredType(SequenceType requiredType)
        {
            this.requiredType = requiredType;
        }

        public SequenceType GetRequiredType()
        {
            return requiredType;
        }

        public void SetSlotNumber(int slotNumber)
        {
            this.slotNumber = slotNumber;
        }

        public StructuredQName GetVariableQName()
        {
            return name;
        }

        public void AddReference(VariableReference @ref, bool isLoopingReference)
        {
        }

        public ISequence EvaluateVariable(IXPathContext context)
        {
            return context.EvaluateLocalVariable(slotNumber);
        }

        /// <summary>
        /// Say that the bound value has the potential to be indexed
        /// </summary>
        public void SetIndexedVariable()
        {
        }

        /// <summary>
        /// Say that the bound value has the potential to be indexed
        /// </summary>
        public bool IsIndexedVariable()
        {
            return false;
        }
    }
}
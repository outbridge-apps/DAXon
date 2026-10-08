////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Abstract class for fixed and computed attribute constructor expressions
    /// </summary>
    internal abstract class AttributeCreator : SimpleNodeConstructor, IValidatingInstruction
    {
        ISimpleType schemaType;
        private int validationAction;
        private int options = ReceiverOption.NONE;
        private bool _isInstruction;
        public virtual void SetInstruction(bool inst)
        {
            _isInstruction = inst;
        }

        public override bool IsInstruction()
        {
            return _isInstruction;
        }

        public virtual void SetSchemaType(ISimpleType type)
        {
            schemaType = type;
        }

        public ISimpleType GetSchemaType()
        {
            return schemaType;
        }

        public virtual void SetValidationAction(int action)
        {
            validationAction = action;
        }

        public int GetValidationAction()
        {
            return validationAction;
        }

        public virtual void SetOptions(int options)
        {
            this.options = options;
        }

        public virtual void SetRejectDuplicates()
        {
            options |= ReceiverOption.REJECT_DUPLICATES;
        }

        public virtual void SetNoSpecialChars()
        {
            options |= ReceiverOption.NO_SPECIAL_CHARS;
        }

        public virtual int GetOptions()
        {
            return options;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            if (GetValidationAction() == Validation.SKIP)
            {
                p |= StaticProperty.ALL_NODES_UNTYPED;
            }

            return p;
        }

        public override ItemType GetItemType()
        {
            return NodeKindTest.ATTRIBUTE;
        }

        public override void ProcessValue(UnicodeString value, Outputter output, IXPathContext context)
        {
            INodeName attName = EvaluateNodeName(context);
            int opt = GetOptions();
            ISimpleType ann = Validate(attName, value, context);
            if (attName.Equals(StandardNames.XML_ID_NAME))
            {
                value = Whitespace.CollapseWhitespace(value);
            }

            try
            {
                output.Attribute(attName, ann, value.ToString(), GetLocation(), opt);
            }
            catch (XPathException err)
            {
                throw DynamicError(GetLocation(), err, context);
            }
        }

        protected virtual ISimpleType Validate(INodeName attName, UnicodeString value, IXPathContext context)
        {
            ISimpleType ann;

            // we may need to change the namespace prefix if the one we chose is
            // already in use with a different namespace URI: this is done behind the scenes
            // by the ComplexContentOutputter
            ISimpleType schemaType = GetSchemaType();
            int validationAction = GetValidationAction();
            if (schemaType != null)
            {
                ann = schemaType;

                // test whether the value actually conforms to the given type
                ValidationFailure err = schemaType.ValidateContent(value, DummyNamespaceResolver.GetInstance(), context.GetConfiguration().GetConversionRules());
                if (err != null)
                {
                    ValidationFailure ve = new ValidationFailure("Attribute value " + Err.Wrap(value, Err.VALUE) + " does not match the required type " + schemaType.Description + ". " + err.GetMessage());
                    ve.SchemaType = schemaType;
                    ve.SetErrorCode("XTTE1540");
                    throw ve.MakeException();
                }
            }
            else if (validationAction == Validation.STRICT || validationAction == Validation.LAX)
            {
                try
                {
                    Configuration config = context.GetConfiguration();
                    ann = config.ValidateAttribute(attName.GetStructuredQName(), value, validationAction);
                }
                catch (ValidationException e)
                {
                    throw XPathException.MakeXPathException(e).MaybeWithErrorCode(validationAction == Validation.STRICT ? "XTTE1510" : "XTTE1515").WithXPathContext(context).MaybeWithLocation(GetLocation()).AsTypeError();
                }
            }
            else
            {
                ann = BuiltInAtomicType.UNTYPED_ATOMIC;
            }

            return ann;
        }

        protected virtual void ValidateOrphanAttribute(Orphan orphan, IXPathContext context)
        {
            ConversionRules rules = context.GetConfiguration().GetConversionRules();
            ISimpleType schemaType = GetSchemaType();
            int validationAction = GetValidationAction();
            if (schemaType != null)
            {
                ValidationFailure err = schemaType.ValidateContent(orphan.UnicodeStringValue, DummyNamespaceResolver.GetInstance(), rules);
                if (err != null)
                {
                    err.SetMessage("Attribute value " + Err.Wrap(orphan.UnicodeStringValue, Err.VALUE) + " does not the match the required type " + schemaType.Description + ". " + err.GetMessage());
                    err.SetErrorCode("XTTE1555");
                    err.Locator = GetLocation();
                    throw err.MakeException();
                }

                orphan.SetTypeAnnotation(schemaType);
                if (schemaType.IsNamespaceSensitive())
                {
                    throw new XPathException("Cannot validate a parentless attribute whose content is namespace-sensitive", "XTTE1545");
                }
            }
            else if (validationAction == Validation.STRICT || validationAction == Validation.LAX)
            {
                try
                {
                    Controller controller = context.GetController();
                    ISimpleType ann = controller.GetConfiguration().ValidateAttribute(NameOfNode.MakeName(orphan).GetStructuredQName(), orphan.UnicodeStringValue, validationAction);
                    orphan.SetTypeAnnotation(ann);
                }
                catch (ValidationException e)
                {
                    throw XPathException.MakeXPathException(e).WithErrorCode(e.ErrorCodeQName).WithXPathContext(context).WithLocation(GetLocation()).AsTypeError();
                }
            }
        }
        ISchemaType IValidatingInstruction.GetSchemaType() => GetSchemaType();
    }
}


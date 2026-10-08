////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.schema.UserSimpleType;
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using static OutSmart.DAXon.Types.SchemaValidationStatus;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// This class has a singleton instance which represents the XML Schema 1.1 built-in type xs:error.
    /// </summary>
    internal sealed class ErrorType : NodeTest, IAtomicType, IUnionType, IPlainType
    {
        private static readonly ErrorType theInstance = new ErrorType();

        public string Name => "error";

        public NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;

        public string EQName => "Q{" + NamespaceConstant.SCHEMA + "}error";

        public IList<IPlainType> PlainMemberTypes => new List<IPlainType>();

        public int RedefinitionLevel => 0;

        public SchemaValidationStatus ValidationStatus => VALIDATED;

        public ISchemaType BaseType => AnySimpleType.INSTANCE;

        public override int Fingerprint => StandardNames.XS_ERROR;

        public override StructuredQName MatchingNodeName => StandardNames.GetStructuredQName(StandardNames.XS_ERROR);

        public StructuredQName TypeName => new StructuredQName("xs", NamespaceUri.SCHEMA, "error");

        public string Description => "xs:error";

        public string DisplayName => "xs:error";

        public ISchemaType BuiltInBaseType => this;

        public int DerivationMethod => Derivation.DERIVATION_RESTRICTION;

        public int FinalProhibitions => Derivation.DERIVATION_EXTENSION | Derivation.DERIVATION_RESTRICTION | Derivation.DERIVATION_LIST | Derivation.DERIVATION_UNION;

        public int WhitespaceAction => Whitespace.COLLAPSE;

        public override int PrimitiveType => Types.Type.ITEM;

        public override double DefaultPriority => -1000;

        public SequenceType ResultTypeOfCast => SequenceType.OPTIONAL_ITEM;

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        private ErrorType()
        {
        }

        public override Genre GetGenre()
        {
            return Genre.ANY;
        }

        public override UType GetUType()
        {
            return UType.VOID;
        }

        public bool IsBuiltInType()
        {
            return true;
        }

        public string GetSystemId()
        {
            return null;
        }

        public static ErrorType GetInstance()
        {
            return theInstance;
        }

        public bool IsComplexType()
        {
            return false;
        }

        public bool IsSimpleType()
        {
            return true;
        }

        public bool IsSameType(ISchemaType other)
        {
            return other is ErrorType;
        }

        public IAtomicSequence Atomize(NodeInfo node)
        {
            return StringValue.MakeUntypedAtomic(node.UnicodeStringValue);
        }

        public void CheckTypeDerivationIsOK(ISchemaType type, int block)
        {
            if (type == this || type == AnySimpleType.INSTANCE)
            {
                return;
            }

            throw new SchemaException("Type xs:error is not validly derived from " + type.Description);
        }

        public override bool IsAtomicType()
        {
            return false;
        }

        public bool IsIdType()
        {
            return false;
        }

        public bool IsIdRefType()
        {
            return false;
        }

        public bool IsAnonymousType()
        {
            return false;
        }

        public bool IsListType()
        {
            return false;
        }

        public bool IsUnionType()
        {
            return true;
        }

        public IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules)
        {
            throw new ValidationFailure("Cast to xs:error always fails").MakeException();
        }

        public StringConverter GetStringConverter(ConversionRules rules)
        {
            return null;
        }

        public ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules)
        {
            return new ValidationFailure("No content is ever valid against the type xs:error");
        }

        public bool IsNamespaceSensitive()
        {
            return false;
        }

        public int GetBlock()
        {
            return 0;
        }

        public bool AllowsDerivation(int derivation)
        {
            return false;
        }

        public void AnalyzeContentExpression(Expression expression, int kind)
        {
            throw new XPathException("No expression can ever return a value of type xs:error");
        }

        public UnicodeString Preprocess(UnicodeString input)
        {
            return input;
        }

        public UnicodeString Postprocess(UnicodeString input)
        {
            return input;
        }

        public override bool IsPlainType()
        {
            return true;
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            return false;
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return false;
        }

        public override ItemType GetPrimitiveItemType()
        {
            return this;
        }
        IAtomicType IPlainType.GetPrimitiveItemType() => this;

        public override IAtomicType GetAtomizedItemType()
        {
            return BuiltInAtomicType.UNTYPED_ATOMIC;
        }

        public override bool IsAtomizable(TypeHierarchy th)
        {
            return false;
        }

        public override string ToString()
        {
            return "xs:error";
        }

        public ValidationFailure Validate(AtomicValue primValue, UnicodeString lexicalValue, ConversionRules rules)
        {
            return new ValidationFailure("No value is valid against type xs:error");
        }

        public bool IsOrdered(bool optimistic)
        {
            return false;
        }

        public bool IsAbstract()
        {
            return true;
        }

        public bool IsPrimitiveType()
        {
            return false;
        }

        public StructuredQName GetStructuredQName()
        {
            return new StructuredQName("xs", NamespaceUri.SCHEMA, "error");
        }

        public ValidationFailure CheckAgainstFacets(AtomicValue value, ConversionRules rules)
        {
            return null;
        }

        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            return ("Evaluation of the supplied expression will always fail");
        }
    }
}

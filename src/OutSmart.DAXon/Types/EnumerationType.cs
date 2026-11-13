////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Types
{
    public class EnumerationType : IAtomicType
    {
        private readonly HashSet<string> values;

        public virtual StructuredQName TypeName => new StructuredQName("", NamespaceUri.ANONYMOUS, "E" + GetHashCode());

        public virtual double DefaultPriority => 0;

        public virtual IList<IPlainType> PlainMemberTypes => new List<IPlainType>(1) { this };

        public virtual int PrimitiveType => StandardNames.XS_STRING;

        public virtual string BasicAlphaCode => "E";

        public virtual ISchemaType BuiltInBaseType => BuiltInAtomicType.STRING;

        public virtual int WhitespaceAction => 0;

        public virtual string Name => null;

        public virtual NamespaceUri TargetNamespace => null;

        public virtual int Fingerprint => -1;

        public virtual string DisplayName => TypeName.DisplayName;

        public virtual string EQName => null;

        public virtual ISchemaType BaseType => BuiltInAtomicType.STRING;

        public virtual int DerivationMethod => Derivation.DERIVATION_RESTRICTION;

        public virtual int FinalProhibitions => 0;

        public virtual string Description => ToString();

        public virtual SchemaValidationStatus ValidationStatus => SchemaValidationStatus.VALIDATED;

        public virtual int RedefinitionLevel => 0;
        public EnumerationType(HashSet<string> values)
        {
            this.values = values;
        }

        public virtual ValidationFailure Validate(AtomicValue primValue, UnicodeString lexicalValue, ConversionRules rules)
        {
            if (primValue.PrimitiveType == BuiltInAtomicType.STRING && values.Contains(primValue.GetStringValue()))
            {
                return null;
            }
            else
            {
                return new ValidationFailure("The string '" + primValue.GetStringValue() + " is not valid for the enumeration type " + ToString());
            }
        }

        public virtual bool IsOrdered(bool optimistic)
        {
            return true;
        }

        public virtual bool IsAbstract()
        {
            return false;
        }

        public virtual bool IsPrimitiveType()
        {
            return false;
        }

        public virtual bool IsIdType()
        {
            return false;
        }

        public virtual bool IsIdRefType()
        {
            return false;
        }

        public virtual bool IsBuiltInType()
        {
            return false;
        }

        public virtual StringConverter GetStringConverter(ConversionRules rules)
        {
            return new StringToEnumConverter(this);
        }

        public virtual bool Matches(IItem item, TypeHierarchy th)
        {
            return item is AtomicValue && ((AtomicValue)item).PrimitiveType == BuiltInAtomicType.STRING && values.Contains(item.GetStringValue());
        }

        public virtual IAtomicType GetPrimitiveItemType()
        {
            return BuiltInAtomicType.STRING;
        }

        public virtual bool IsPlainType()
        {
            return true;
        }

        public virtual UType GetUType()
        {
            return UType.STRING;
        }

        public virtual IPlainType GetAtomizedItemType()
        {
            return BuiltInAtomicType.STRING;
        }

        public virtual bool IsAtomizable(TypeHierarchy th)
        {
            return true;
        }

        public virtual bool IsAtomicType()
        {
            return true;
        }

        public virtual bool IsListType()
        {
            return false;
        }

        public virtual bool IsUnionType()
        {
            return false;
        }

        public virtual IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules)
        {
            return new StringValue(value);
        }

        public virtual ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules)
        {
            return null;
        }

        public virtual bool IsNamespaceSensitive()
        {
            return false;
        }

        public virtual UnicodeString Preprocess(UnicodeString input)
        {
            return input;
        }

        public virtual UnicodeString Postprocess(UnicodeString input)
        {
            return input;
        }

        public virtual StructuredQName GetStructuredQName()
        {
            return null;
        }

        public virtual bool IsComplexType()
        {
            return false;
        }

        public virtual bool IsSimpleType()
        {
            return true;
        }

        public virtual bool IsAnonymousType()
        {
            return true;
        }

        public virtual int GetBlock()
        {
            return 0;
        }

        public virtual bool AllowsDerivation(int derivation)
        {
            return true;
        }

        public virtual void AnalyzeContentExpression(Expression expression, int kind)
        {
            BuiltInAtomicType.STRING.AnalyzeContentExpression(expression, kind);
        }

        public virtual IAtomicSequence Atomize(NodeInfo node)
        {
            return null;
        }

        public virtual bool IsSameType(ISchemaType other)
        {
            return other is EnumerationType && values.Equals(((EnumerationType)other).values);
        }

        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(256);
            fsb.Append("enum(");
            foreach (string st in values)
            {
                char delim = '"';
                if (st.IndexOf('"') >= 0)
                {
                    if (st.IndexOf('\'') > 0)
                    {
                        delim = '`';
                    }
                    else
                    {
                        delim = '\'';
                    }
                }

                fsb.Append(delim).Append(st).Append(delim).Append(", ");
            }

            fsb.Length = fsb.Length - 2;
            fsb.Append(')');
            return fsb.ToString();
        }

        public virtual void CheckTypeDerivationIsOK(ISchemaType @base, int block)
        {
        }

        public virtual string GetSystemId()
        {
            return null;
        }

        public virtual Genre GetGenre() => Genre.ATOMIC; // enum values are atomic items
        public virtual string ExplainMismatch(IItem item, TypeHierarchy th) => null; // upstream default: no extra explanation (diagnostics must not throw)

        private sealed class StringToEnumConverter : StringConverter
        {
            private readonly EnumerationType enumType;
            public StringToEnumConverter(EnumerationType enumType)
            {
                this.enumType = enumType;
            }

            public override IConversionResult ConvertString(UnicodeString input)
            {
                if (enumType.values.Contains(input.ToString()))
                {
                    return new StringValue(input, enumType);
                }
                else
                {
                    return new ValidationFailure("'" + input + "' is not a valid value for the required enumeration type");
                }
            }
        }
    }
}
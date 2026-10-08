////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.schema.UserSimpleType;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Types
{
    internal sealed class NumericType : LocalUnionType, ISimpleType, IPlainType
    {
        //    }
        private static NumericType THE_INSTANCE;

        public override StructuredQName TypeName => new StructuredQName("xs", NamespaceUri.SCHEMA, "numeric");

        public override string BasicAlphaCode => "A";

        public override IList<IPlainType> PlainMemberTypes => new List<IPlainType>(MemberTypes);

        public override SequenceType ResultTypeOfCast => SequenceType.ATOMIC_SEQUENCE;

        public override int PrimitiveType => BuiltInAtomicType.ANY_ATOMIC.Fingerprint;

        public ISchemaType BuiltInBaseType => AnySimpleType.INSTANCE;

        public int WhitespaceAction => Whitespace.COLLAPSE;

        public string Name => "numeric";

        public NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;

        public int Fingerprint => StandardNames.XS_NUMERIC;

        public string DisplayName => "xs:numeric";

        public string EQName => "Q(" + NamespaceConstant.SCHEMA + "}numeric";

        public ISchemaType BaseType => AnySimpleType.INSTANCE;

        public int DerivationMethod => Derivation.DERIVATION_RESTRICTION;

        public int FinalProhibitions => 0;

        public override string Description => "xs:numeric";

        //    }
        public SchemaValidationStatus ValidationStatus => SchemaValidationStatus.VALIDATED;

        //    }
        public int RedefinitionLevel => 0;

        private NumericType() : base(new List<IAtomicType> { BuiltInAtomicType.DOUBLE, BuiltInAtomicType.FLOAT, BuiltInAtomicType.DECIMAL })
        {
        }
        //    }
        public static NumericType GetInstance()
        {
            lock (typeof(NumericType))
            {
                if (THE_INSTANCE == null)
                {
                    THE_INSTANCE = new NumericType();
                    BuiltInType.Register(StandardNames.XS_NUMERIC, THE_INSTANCE);
                }

                return THE_INSTANCE;
            }
        }

        public override Genre GetGenre()
        {
            return Genre.ATOMIC;
        }

        public static bool IsNumericType(ItemType type)
        {
            return type.IsPlainType() && UType.NUMERIC.Subsumes(type.GetUType());
        }

        public override bool IsPlainType()
        {
            return true;
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            return item is NumericValue;
        }

        public override IAtomicType GetPrimitiveItemType()
        {
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        public override UType GetUType()
        {
            return UType.NUMERIC;
        }

        public override IPlainType GetAtomizedItemType()
        {
            return this;
        }

        public override bool IsAtomicType()
        {
            return false;
        }

        public override bool IsListType()
        {
            return false;
        }

        public override bool IsUnionType()
        {
            return true;
        }

        public override bool IsBuiltInType()
        {
            return true;
        }

        public override IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules)
        {
            try
            {
                double d = StringToDouble.GetInstance().StringToNumber(value);
                return new DoubleValue(d);
            }
            catch (FormatException)
            {
                string message = "Cannot convert string \"" + value + "\" to xs:numeric";
                throw new ValidationFailure(message).MakeException();
            }
        }

        public override ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules)
        {
            string error = StringToDouble.GetInstance().ParseOrError(value, out _);
            return error == null ? null : new ValidationFailure(error);
        }

        public override ValidationFailure CheckAgainstFacets(AtomicValue value, ConversionRules rules)
        {
            return null;
        }

        public override bool IsNamespaceSensitive()
        {
            return false;
        }

        public UnicodeString Preprocess(UnicodeString input)
        {
            return input;
        }

        public UnicodeString Postprocess(UnicodeString input)
        {
            return input;
        }

        public override StructuredQName GetStructuredQName()
        {
            return new StructuredQName("xs", NamespaceUri.SCHEMA, "numeric");
        }

        public bool IsComplexType()
        {
            return false;
        }

        public bool IsSimpleType()
        {
            return true;
        }

        public bool IsAnonymousType()
        {
            return false;
        }

        public int GetBlock()
        {
            return 0;
        }

        public bool AllowsDerivation(int derivation)
        {
            return true;
        }

        public void AnalyzeContentExpression(Expression expression, int kind)
        {
            BuiltInAtomicType.AnalyzeContentExpression(this, expression, kind);
        }

        public IAtomicSequence Atomize(NodeInfo node)
        {
            throw new NotSupportedException(); // nodes are never annotated with a union type
        }

        //    }
        public bool IsSameType(ISchemaType other)
        {
            return other is NumericType;
        }

        public string GetSystemId()
        {
            return null;
        }

        public override bool IsIdType()
        {
            return false;
        }

        public override bool IsIdRefType()
        {
            return false;
        }

        //    }
        public override string ToString()
        {
            return "xs:numeric";
        }

        //    }
        public void CheckTypeDerivationIsOK(ISchemaType @base, int block)
        {
        }
        IAtomicSequence ISimpleType.GetTypedValue(UnicodeString arg0, INamespaceResolver arg1, ConversionRules arg2) => GetTypedValue(arg0, arg1, arg2); // covariant bridge
    }
}


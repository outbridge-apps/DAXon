////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Expressions
{
    public abstract class JPConverter
    {
        private static readonly Dictionary<System.Type, JPConverter> converterMap = new Dictionary<System.Type, JPConverter>();

        private static readonly Dictionary<System.Type, Types.ItemType> itemTypeMap = new Dictionary<System.Type, Types.ItemType>();

        private static readonly Dictionary<System.Type, int> cardinalityMap = new Dictionary<System.Type, int>();

        static JPConverter()

        {

            InitConverterMap();

            InitItemTypeMap();

            InitCardinalityMap();

        }

        private static void InitConverterMap()

        {

            converterMap[typeof(XdmValue)] = new FromXdmValue(AnyItemType.GetInstance(), StaticProperty.ALLOWS_ZERO_OR_MORE);
            converterMap[typeof(XdmItem)] = new FromXdmValue(AnyItemType.GetInstance(), StaticProperty.ALLOWS_ONE);
            converterMap[typeof(XdmAtomicValue)] = new FromXdmValue(BuiltInAtomicType.ANY_ATOMIC, StaticProperty.ALLOWS_ONE);
            converterMap[typeof(XdmNode)] = new FromXdmValue(AnyNodeTest.GetInstance(), StaticProperty.ALLOWS_ONE);
            converterMap[typeof(XdmFunctionItem)] = new FromXdmValue(AnyFunctionType.GetInstance(), StaticProperty.ALLOWS_ONE);
            converterMap[typeof(XdmMap)] = new FromXdmValue(MapType.ANY_MAP_TYPE, StaticProperty.ALLOWS_ONE);
            converterMap[typeof(XdmArray)] = new FromXdmValue(ArrayItemType.GetInstance(), StaticProperty.ALLOWS_ONE);
            converterMap[typeof(XdmEmptySequence)] = new FromXdmValue(ErrorType.GetInstance(), StaticProperty.ALLOWS_ZERO);
            converterMap[typeof(ISequenceIterator)] = FromSequenceIterator.INSTANCE;
            converterMap[typeof(ISequence)] = FromSequence.INSTANCE;
            converterMap[typeof(OneOrMore<>)] = FromSequence.INSTANCE;
            converterMap[typeof(One<>)] = FromSequence.INSTANCE;
            converterMap[typeof(ZeroOrOne<>)] = FromSequence.INSTANCE;
            converterMap[typeof(ZeroOrMore<>)] = FromSequence.INSTANCE;
            converterMap[typeof(string)] = FromString.INSTANCE;
            converterMap[typeof(UnicodeString)] = FromUnicodeString.INSTANCE;
            converterMap[typeof(bool)] = FromBoolean.INSTANCE;
            converterMap[typeof(bool)] = FromBoolean.INSTANCE;
            converterMap[typeof(double)] = FromDouble.INSTANCE;
            converterMap[typeof(double)] = FromDouble.INSTANCE;
            converterMap[typeof(float)] = FromFloat.INSTANCE;
            converterMap[typeof(float)] = FromFloat.INSTANCE;
            converterMap[typeof(BigDecimal)] = FromBigDecimal.INSTANCE;
            converterMap[typeof(BigInteger)] = FromBigInteger.INSTANCE;
            converterMap[typeof(long)] = FromLong.INSTANCE;
            converterMap[typeof(long)] = FromLong.INSTANCE;
            converterMap[typeof(int)] = FromInt.INSTANCE;
            converterMap[typeof(int)] = FromInt.INSTANCE;
            converterMap[typeof(short)] = FromShort.INSTANCE;
            converterMap[typeof(byte)] = FromByte.INSTANCE;
            converterMap[typeof(byte)] = FromByte.INSTANCE;
            converterMap[typeof(char)] = FromCharacter.INSTANCE;
            converterMap[typeof(URI)] = FromURI.INSTANCE;
            converterMap[typeof(Uri)] = FromURI.INSTANCE;
            converterMap[typeof(DateTime)] = FromDate.INSTANCE;
            converterMap[typeof(long[])] = FromLongArray.INSTANCE;
            converterMap[typeof(int[])] = FromIntArray.INSTANCE;
            converterMap[typeof(short[])] = FromShortArray.INSTANCE;
            converterMap[typeof(byte[])] = FromByteArray.INSTANCE;
            converterMap[typeof(char[])] = FromCharArray.INSTANCE;
            converterMap[typeof(double[])] = FromDoubleArray.INSTANCE;
            converterMap[typeof(float[])] = FromFloatArray.INSTANCE;
            converterMap[typeof(bool[])] = FromBooleanArray.INSTANCE;
            converterMap[typeof(Collection<>)] = FromCollection.INSTANCE;
        }

        private static void InitItemTypeMap()

        {

            itemTypeMap[typeof(BooleanValue)] = BuiltInAtomicType.BOOLEAN;
            itemTypeMap[typeof(StringValue)] = BuiltInAtomicType.STRING;
            itemTypeMap[typeof(DoubleValue)] = BuiltInAtomicType.DOUBLE;
            itemTypeMap[typeof(FloatValue)] = BuiltInAtomicType.FLOAT;
            itemTypeMap[typeof(BigDecimalValue)] = BuiltInAtomicType.DECIMAL;
            itemTypeMap[typeof(IntegerValue)] = BuiltInAtomicType.INTEGER;
            itemTypeMap[typeof(DurationValue)] = BuiltInAtomicType.DURATION;
            itemTypeMap[typeof(DayTimeDurationValue)] = BuiltInAtomicType.DAY_TIME_DURATION;
            itemTypeMap[typeof(YearMonthDurationValue)] = BuiltInAtomicType.YEAR_MONTH_DURATION;
            itemTypeMap[typeof(DateTimeValue)] = BuiltInAtomicType.DATE_TIME;
            itemTypeMap[typeof(DateValue)] = BuiltInAtomicType.DATE;
            itemTypeMap[typeof(TimeValue)] = BuiltInAtomicType.TIME;
            itemTypeMap[typeof(GYearValue)] = BuiltInAtomicType.G_YEAR;
            itemTypeMap[typeof(GYearMonthValue)] = BuiltInAtomicType.G_YEAR_MONTH;
            itemTypeMap[typeof(GMonthValue)] = BuiltInAtomicType.G_MONTH;
            itemTypeMap[typeof(GMonthDayValue)] = BuiltInAtomicType.G_MONTH_DAY;
            itemTypeMap[typeof(GDayValue)] = BuiltInAtomicType.G_DAY;
            itemTypeMap[typeof(AnyURIValue)] = BuiltInAtomicType.ANY_URI;
            itemTypeMap[typeof(QNameValue)] = BuiltInAtomicType.QNAME;
            itemTypeMap[typeof(NotationValue)] = BuiltInAtomicType.NOTATION;
            itemTypeMap[typeof(HexBinaryValue)] = BuiltInAtomicType.HEX_BINARY;
            itemTypeMap[typeof(Base64BinaryValue)] = BuiltInAtomicType.BASE64_BINARY;
            itemTypeMap[typeof(NodeInfo)] = AnyNodeTest.GetInstance();
            itemTypeMap[typeof(ITreeInfo)] = NodeKindTest.DOCUMENT;
            itemTypeMap[typeof(MapItem)] = MapType.GetInstance();
            itemTypeMap[typeof(ArrayItem)] = ArrayItemType.GetInstance();
            itemTypeMap[typeof(IFunctionItem)] = AnyFunctionType.GetInstance();
            itemTypeMap[typeof(AtomicValue)] = BuiltInAtomicType.ANY_ATOMIC; //itemTypeMap.put(UntypedAtomicValue.class, BuiltInAtomicType.UNTYPED_ATOMIC);
        }

        private static void InitCardinalityMap()

        {
            cardinalityMap[typeof(ISequence)] = StaticProperty.ALLOWS_ZERO_OR_MORE;
            cardinalityMap[typeof(ZeroOrMore<>)] = StaticProperty.ALLOWS_ZERO_OR_MORE;
            cardinalityMap[typeof(OneOrMore<>)] = StaticProperty.ALLOWS_ONE_OR_MORE;
            cardinalityMap[typeof(One<>)] = StaticProperty.EXACTLY_ONE;
            cardinalityMap[typeof(ZeroOrOne<>)] = StaticProperty.ALLOWS_ZERO_OR_ONE;
            cardinalityMap[typeof(XdmValue)] = StaticProperty.ALLOWS_ZERO_OR_MORE;
            cardinalityMap[typeof(XdmItem)] = StaticProperty.ALLOWS_ZERO_OR_MORE;
            cardinalityMap[typeof(XdmEmptySequence)] = StaticProperty.ALLOWS_ZERO;
        }

        public static JPConverter Allocate(System.Type javaClass, Configuration config)
        {
            if (typeof(System.Xml.XmlQualifiedName).IsAssignableFrom(javaClass))
            {
                return FromQName.INSTANCE;
            }

            if (typeof(ISequence).IsAssignableFrom(javaClass))
            {
                Types.ItemType itemType = itemTypeMap.GetOrDefault(javaClass);
                if (itemType != null)
                {
                    return new FromSequence(itemType, StaticProperty.ALLOWS_ZERO_OR_ONE);
                }
            }

            JPConverter c = converterMap.GetOrDefault(javaClass);
            if (c != null)
            {
                return c;
            }

            if (javaClass.Equals(typeof(object)))
            {
                return FromObject.INSTANCE;
            }

            if (typeof(NodeInfo).IsAssignableFrom(javaClass))
            {

                // probably now redundant
                return new FromSequence(AnyNodeTest.GetInstance(), StaticProperty.ALLOWS_ZERO_OR_ONE);
            }

            if (typeof(ResolvedResource).IsAssignableFrom(javaClass))
            {
                return FromSource.INSTANCE;
            }

            foreach (KeyValuePair<System.Type, JPConverter> e in converterMap)
            {
                if (e.Key.IsAssignableFrom(javaClass))
                {
                    return e.Value;
                }
            }

            IList<IExternalObjectModel> externalObjectModels = config.ExternalObjectModels;
            foreach (IExternalObjectModel model in externalObjectModels)
            {
                try
                {
                    JPConverter converter = model.GetJPConverter(javaClass, config);
                    if (converter != null)
                    {
                        return converter;
                    }
                }
                catch (Exception)
                {
                    config.DeregisterExternalObjectModel(model);
                }
            }

            if (javaClass.IsArray)
            {
                System.Type itemClass = javaClass.GetElementType();
                return new FromObjectArray(Allocate(itemClass, config));
            }

            if (javaClass.Equals(typeof(void)))
            {
                return VoidConverter.INSTANCE;
            }

            JavaExternalObjectType result;
            lock (config.syncLock)
            {
                result = JavaExternalObjectType.Of(javaClass);
            }

            return new ExternalObjectWrapper(result);
        }

        public abstract IGroundedValue Convert(object @object, IXPathContext context);
        public abstract Types.ItemType GetItemType();
        public virtual int GetCardinality()
        {

            // default implementation
            return StaticProperty.EXACTLY_ONE;
        }

        internal sealed class FromObject : JPConverter
        {
            public static readonly FromObject INSTANCE = new FromObject();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                System.Type theClass = @object.GetType();
                JPConverter instanceConverter = Allocate(theClass, context.GetConfiguration());
                if (instanceConverter is FromObject)
                {
                    JavaExternalObjectType result;
                    lock (context.GetConfiguration())
                    {
                        result = JavaExternalObjectType.Of(theClass);
                    }

                    instanceConverter = new ExternalObjectWrapper(result);
                }

                return instanceConverter.Convert(@object, context);
            }

            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromSequenceIterator : JPConverter
        {
            public static readonly FromSequenceIterator INSTANCE = new FromSequenceIterator();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                try
                {
                    return SequenceTool.ToGroundedValue(((ISequenceIterator)@object));
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }

            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromXdmValue : JPConverter
        {
            private readonly Types.ItemType resultType;
            private readonly int cardinality;
            public FromXdmValue(Types.ItemType resultType, int cardinality)
            {
                this.resultType = resultType;
                this.cardinality = cardinality;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return (IGroundedValue)((XdmValue)@object).UnderlyingValue;
            }

            public override Types.ItemType GetItemType()
            {
                return resultType;
            }

            public override int GetCardinality()
            {
                return cardinality;
            }
        }

        internal sealed class FromSequence : JPConverter
        {
            public static readonly FromSequence INSTANCE = new FromSequence(AnyItemType.GetInstance(), StaticProperty.ALLOWS_ZERO_OR_MORE);
            private readonly Types.ItemType resultType;
            private readonly int cardinality;
            public FromSequence(Types.ItemType resultType, int cardinality)
            {
                this.resultType = resultType;
                this.cardinality = cardinality;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return ((ISequence)@object).Materialize();
            }

            public override Types.ItemType GetItemType()
            {
                return resultType;
            }

            public override int GetCardinality()
            {
                return cardinality;
            }
        }

        internal sealed class FromString : JPConverter
        {
            public static readonly FromString INSTANCE = new FromString();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new StringValue((string)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        internal sealed class FromUnicodeString : JPConverter
        {
            public static readonly FromUnicodeString INSTANCE = new FromUnicodeString();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new StringValue((UnicodeString)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        internal sealed class FromBoolean : JPConverter
        {
            public static readonly FromBoolean INSTANCE = new FromBoolean();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return BooleanValue.Get((bool)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.BOOLEAN;
            }
        }

        internal sealed class FromDouble : JPConverter
        {
            public static readonly FromDouble INSTANCE = new FromDouble();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new DoubleValue((double)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DOUBLE;
            }
        }

        internal sealed class FromFloat : JPConverter
        {
            public static readonly FromFloat INSTANCE = new FromFloat();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new FloatValue((float)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.FLOAT;
            }
        }

        internal sealed class FromBigDecimal : JPConverter
        {
            public static readonly FromBigDecimal INSTANCE = new FromBigDecimal();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new BigDecimalValue((BigDecimal)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DECIMAL;
            }
        }

        internal sealed class FromBigInteger : JPConverter
        {
            public static readonly FromBigInteger INSTANCE = new FromBigInteger();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return IntegerValue.MakeIntegerValue((BigInteger)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        internal sealed class FromLong : JPConverter
        {
            public static readonly FromLong INSTANCE = new FromLong();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new Int64Value((long)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        internal sealed class FromInt : JPConverter
        {
            public static readonly FromInt INSTANCE = new FromInt();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new Int64Value((int)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        internal sealed class FromShort : JPConverter
        {
            public static readonly FromShort INSTANCE = new FromShort();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                // Unbox directly: the old (Short)@object reference-cast threw InvalidCastException on a boxed short.
                return new Int64Value((short)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        internal sealed class FromByte : JPConverter
        {
            public static readonly FromByte INSTANCE = new FromByte();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new Int64Value((byte)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        internal sealed class FromCharacter : JPConverter
        {
            public static readonly FromCharacter INSTANCE = new FromCharacter();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new StringValue(@object.ToString());
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        internal sealed class FromQName : JPConverter
        {
            public static readonly FromQName INSTANCE = new FromQName();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                System.Xml.XmlQualifiedName qn = (System.Xml.XmlQualifiedName)@object;
                return new QNameValue("", NamespaceUri.Of(qn.Namespace), qn.Name);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.QNAME;
            }
        }

        internal sealed class FromURI : JPConverter
        {
            public static readonly FromURI INSTANCE = new FromURI();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new AnyURIValue((@object.ToString()));
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.ANY_URI;
            }
        }

        internal sealed class FromDate : JPConverter
        {
            public static readonly FromDate INSTANCE = new FromDate();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                // Unspecified kind is treated as UTC (the shim-Date predecessor was epoch-based UTC).
                DateTime dt = (DateTime)@object;
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }

                return DateTimeValue.FromJavaTime(new DateTimeOffset(dt).ToUnixTimeMilliseconds());
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DATE_TIME;
            }
        }

        internal sealed class ExternalObjectWrapper : JPConverter
        {
            private readonly JavaExternalObjectType resultType;
            public ExternalObjectWrapper(JavaExternalObjectType resultType)
            {
                this.resultType = resultType;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                if (@object == null)
                {
                    return null;
                }
                else if (resultType.JavaClass.IsInstanceOfType(@object))
                {
                    return new ObjectValue<object>(@object, resultType.JavaClass);
                }
                else
                {
                    throw new XPathException("Java external object of type " + @object.GetType().FullName + " is not an instance of the required type " + resultType.JavaClass.FullName, "XPTY0004");
                }
            }

            public override Types.ItemType GetItemType()
            {
                return resultType;
            }
        }

        internal sealed class VoidConverter : JPConverter
        {
            public static readonly VoidConverter INSTANCE = new VoidConverter();
            public VoidConverter()
            {
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return EmptySequence.GetInstance();
            }

            /// <summary>
            /// Deliberately avoid giving type information
            /// </summary>
            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }
        }

        internal sealed class FromCollection : JPConverter
        {
            public static readonly FromCollection INSTANCE = new FromCollection();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IList<IItem> list = new List<IItem>(((Collection<object>)@object).Count);
                foreach (object obj in (Collection<object>)@object)
                {
                    JPConverter itemConverter = Allocate(obj.GetType(), context.GetConfiguration());
                    try
                    {
                        IItem item = SequenceTool.AsItem(itemConverter.Convert(obj, context));
                        if (item != null)
                        {
                            list.Add(item);
                        }
                    }
                    catch (XPathException e)
                    {
                        throw new XPathException("Returned Collection contains an object that cannot be converted to an Item (" + obj.GetType() + "): " + e.Message, DAXonErrorCode.SXJE0051);
                    }
                }

                return new SequenceExtent.Of<IItem>(list);
            }

            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromSource : JPConverter
        {
            public static readonly FromSource INSTANCE = new FromSource();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                ParseOptions options = new ParseOptions();
                Controller controller = context.GetController();
                if (controller != null)
                {
                    options = options.WithSchemaValidationMode(controller.SchemaValidationMode);
                }

                if (@object is ITreeInfo)
                {
                    return ((ITreeInfo)@object).GetRootNode();
                }

                return context.GetConfiguration().BuildDocumentTree((ResolvedResource)@object, options).GetRootNode();
            }

            public override Types.ItemType GetItemType()
            {
                return AnyNodeTest.GetInstance();
            }
        }

        internal sealed class FromLongArray : JPConverter
        {
            public static readonly FromLongArray INSTANCE = new FromLongArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((long[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(((long[])@object)[i], BuiltInAtomicType.LONG);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.LONG;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromIntArray : JPConverter
        {
            public static readonly FromIntArray INSTANCE = new FromIntArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((int[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(((int[])@object)[i], BuiltInAtomicType.INT);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INT;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromShortArray : JPConverter
        {
            public static readonly FromShortArray INSTANCE = new FromShortArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((short[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(((short[])@object)[i], BuiltInAtomicType.SHORT);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.SHORT;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromByteArray : JPConverter
        {
            public static readonly FromByteArray INSTANCE = new FromByteArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((byte[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(255 & (int)((byte[])@object)[i], BuiltInAtomicType.UNSIGNED_BYTE);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.UNSIGNED_BYTE;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromCharArray : JPConverter
        {
            public static readonly FromCharArray INSTANCE = new FromCharArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return StringValue.MakeStringValue(new string((char[])@object));
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        internal sealed class FromDoubleArray : JPConverter
        {
            public static readonly FromDoubleArray INSTANCE = new FromDoubleArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((double[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = new DoubleValue(((double[])@object)[i]);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DOUBLE;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromFloatArray : JPConverter
        {
            public static readonly FromFloatArray INSTANCE = new FromFloatArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((float[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = new DoubleValue(((float[])@object)[i]);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.FLOAT;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromBooleanArray : JPConverter
        {
            public static readonly FromBooleanArray INSTANCE = new FromBooleanArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((bool[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = BooleanValue.Get(((bool[])@object)[i]);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.BOOLEAN;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        internal sealed class FromObjectArray : JPConverter
        {
            private readonly JPConverter itemConverter;
            public FromObjectArray(JPConverter itemConverter)
            {
                this.itemConverter = itemConverter;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                object[] arrayObject = (Object[])@object;
                IList<IItem> newArray = new List<IItem>(arrayObject.Length);
                foreach (object member in arrayObject)
                {
                    if (member != null)
                    {
                        try
                        {
                            IItem newItem = SequenceTool.AsItem(itemConverter.Convert(member, context));
                            if (newItem != null)
                            {
                                newArray.Add(newItem);
                            }
                        }
                        catch (XPathException e)
                        {
                            throw new XPathException("Returned array contains an object that cannot be converted to an Item (" + member.GetType() + "): " + e.Message, DAXonErrorCode.SXJE0051);
                        }
                    }
                    else
                    {
                        throw new XPathException("Returned array contains null values: cannot convert to items", DAXonErrorCode.SXJE0051);
                    }
                }

                return new SequenceExtent.Of<IItem>(newArray);
            }

            public override Types.ItemType GetItemType()
            {
                return itemConverter.GetItemType();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }
    }
}

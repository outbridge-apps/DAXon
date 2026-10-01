////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Events;
using System.Numerics;
namespace OutSmart.DAXon.Expressions
{
    public abstract class PJConverter
    {
        private static readonly Dictionary<System.Type, SequenceType> jpmap = new Dictionary<System.Type, SequenceType>();
        static PJConverter()
        {
            jpmap[typeof(bool)] = SequenceType.SINGLE_BOOLEAN;
            jpmap[typeof(bool)] = SequenceType.OPTIONAL_BOOLEAN;
            jpmap[typeof(string)] = SequenceType.OPTIONAL_STRING;
            jpmap[typeof(string)] = SequenceType.OPTIONAL_STRING;

            // Mappings for long and int are chosen to avoid static type errors when
            // a Java method expecting long or int is called with an integer literal
            jpmap.PutAndGetPrevious(typeof(long), SequenceType.SINGLE_INTEGER);
            jpmap[typeof(long)] = SequenceType.OPTIONAL_INTEGER;
            jpmap[typeof(int)] = SequenceType.SINGLE_INTEGER;
            jpmap[typeof(int)] = SequenceType.OPTIONAL_INTEGER;
            jpmap[typeof(short)] = SequenceType.SINGLE_SHORT;
            jpmap[typeof(short?)] = SequenceType.OPTIONAL_SHORT;
            jpmap[typeof(byte)] = SequenceType.SINGLE_BYTE;
            jpmap[typeof(byte)] = SequenceType.OPTIONAL_BYTE;
            jpmap[typeof(float)] = SequenceType.SINGLE_FLOAT;
            jpmap[typeof(float)] = SequenceType.OPTIONAL_FLOAT;
            jpmap[typeof(double)] = SequenceType.SINGLE_DOUBLE;
            jpmap[typeof(double)] = SequenceType.OPTIONAL_DOUBLE;
            jpmap[typeof(URI)] = SequenceType.OPTIONAL_ANY_URI;
            jpmap[typeof(global::System.Uri)] = SequenceType.OPTIONAL_ANY_URI;
            jpmap[typeof(BigInteger)] = SequenceType.OPTIONAL_INTEGER;
            jpmap[typeof(BigDecimal)] = SequenceType.OPTIONAL_DECIMAL;
            jpmap[typeof(UnicodeString)] = SequenceType.OPTIONAL_STRING;
            jpmap[typeof(StringValue)] = SequenceType.OPTIONAL_STRING;
            jpmap[typeof(BooleanValue)] = SequenceType.OPTIONAL_BOOLEAN;
            jpmap[typeof(DoubleValue)] = SequenceType.OPTIONAL_DOUBLE;
            jpmap[typeof(FloatValue)] = SequenceType.OPTIONAL_FLOAT;
            jpmap[typeof(DecimalValue)] = SequenceType.OPTIONAL_DECIMAL;
            jpmap[typeof(IntegerValue)] = SequenceType.OPTIONAL_INTEGER;
            jpmap[typeof(AnyURIValue)] = SequenceType.OPTIONAL_ANY_URI;
            jpmap[typeof(QNameValue)] = SequenceType.OPTIONAL_QNAME;
            jpmap[typeof(NotationValue)] = SequenceType.OPTIONAL_NOTATION;
            jpmap[typeof(DateValue)] = SequenceType.OPTIONAL_DATE;
            jpmap[typeof(DateTimeValue)] = SequenceType.OPTIONAL_DATE_TIME;
            jpmap[typeof(TimeValue)] = SequenceType.OPTIONAL_TIME;
            jpmap[typeof(DurationValue)] = SequenceType.OPTIONAL_DURATION;
            jpmap[typeof(DayTimeDurationValue)] = SequenceType.OPTIONAL_DAY_TIME_DURATION;
            jpmap[typeof(YearMonthDurationValue)] = SequenceType.OPTIONAL_YEAR_MONTH_DURATION;
            jpmap[typeof(GYearValue)] = SequenceType.OPTIONAL_G_YEAR;
            jpmap[typeof(GYearMonthValue)] = SequenceType.OPTIONAL_G_YEAR_MONTH;
            jpmap[typeof(GMonthValue)] = SequenceType.OPTIONAL_G_MONTH;
            jpmap[typeof(GMonthDayValue)] = SequenceType.OPTIONAL_G_MONTH_DAY;
            jpmap[typeof(GDayValue)] = SequenceType.OPTIONAL_G_DAY;
            jpmap[typeof(Base64BinaryValue)] = SequenceType.OPTIONAL_BASE64_BINARY;
            jpmap[typeof(HexBinaryValue)] = SequenceType.OPTIONAL_HEX_BINARY;
            jpmap[typeof(IFunctionItem)] = SequenceType.OPTIONAL_FUNCTION_ITEM;
            jpmap[typeof(MapItem)] = MapType.OPTIONAL_MAP_ITEM;
            jpmap[typeof(NodeInfo)] = SequenceType.OPTIONAL_NODE;
            jpmap[typeof(ITreeInfo)] = SequenceType.OPTIONAL_DOCUMENT_NODE;
        }

        public static SequenceType GetEquivalentSequenceType(System.Type javaClass)
        {
            if (javaClass.IsArray)
            {
                System.Type memberClass = javaClass.GetElementType();
                if (memberClass == typeof(byte))
                {

                    // special-case byte[] which maps to xs:unsignedByte* - see bugs 3525, 3818
                    return SequenceType.MakeSequenceType(BuiltInAtomicType.UNSIGNED_BYTE, StaticProperty.ALLOWS_ZERO_OR_MORE);
                }
                else
                {
                    SequenceType memberType = GetEquivalentSequenceType(memberClass);
                    if (memberType != null)
                    {
                        return SequenceType.MakeSequenceType(memberType.PrimaryType, StaticProperty.ALLOWS_ZERO_OR_MORE);
                    }
                }
            }

            return jpmap.GetOrDefault(javaClass);
        }

        public abstract object Convert(ISequence value, System.Type targetClass, IXPathContext context);
        public static PJConverter Allocate(Configuration config, ItemType itemType, int cardinality, System.Type targetClass)
        {
            TypeHierarchy th = config.GetTypeHierarchy();
            if (targetClass == typeof(ISequenceIterator))
            {
                return ToSequenceIterator.INSTANCE;
            }

            if (targetClass == typeof(ISequence) || targetClass == typeof(IItem))
            {
                return Identity.INSTANCE;
            }

            if (targetClass == typeof(One<>))
            {
                return ToOne.INSTANCE;
            }

            if (targetClass == typeof(ZeroOrOne<>))
            {
                return ToZeroOrOne.INSTANCE;
            }

            if (targetClass == typeof(OneOrMore<>))
            {
                return ToOneOrMore.INSTANCE;
            }

            if (targetClass == typeof(ZeroOrMore<>))
            {
                return ToZeroOrMore.INSTANCE;
            }

            if (targetClass == typeof(IGroundedValue) | targetClass == typeof(SequenceExtent))
            {
                return ToSequenceExtent.INSTANCE;
            }

            if (!itemType.IsPlainType())
            {
                IList<IExternalObjectModel> externalObjectModels = config.ExternalObjectModels;
                foreach (IExternalObjectModel model in externalObjectModels)
                {
                    try
                    {
                        PJConverter converter = model.GetPJConverter(targetClass);
                        if (converter != null)
                        {
                            return converter;
                        }
                    }
                    catch (Exception e)
                    {
                        config.DeregisterExternalObjectModel(model);
                    }
                }

                if (typeof(NodeInfo).IsAssignableFrom(targetClass))
                {
                    return Identity.INSTANCE;
                }
            }

            if (typeof(Collection<>).IsAssignableFrom(targetClass))
            {
                return ToCollection.INSTANCE;
            }

            if (targetClass.IsArray)
            {
                PJConverter itemConverter = Allocate(config, itemType, StaticProperty.EXACTLY_ONE, targetClass.GetElementType());
                return new ToArray(itemConverter);
            }

            if (!Cardinality.AllowsMany(cardinality))
            {
                if (itemType.IsPlainType())
                {
                    if (itemType == ErrorType.GetInstance())
                    {

                        // supplied value is (); we need to convert it to null; this converter does the job.
                        return StringItemToString.INSTANCE;
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.STRING))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(string) || targetClass == typeof(string))
                        {
                            return StringItemToString.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(StringValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else if (targetClass == typeof(char))
                        {
                            return StringItemToChar.INSTANCE;
                        }
                        else if (targetClass == typeof(UnicodeString))
                        {
                            return StringItemToUnicodeString.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (itemType == BuiltInAtomicType.UNTYPED_ATOMIC)
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(string) || targetClass == typeof(string))
                        {
                            return StringItemToString.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(StringValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            try
                            {
                                System.Reflection.ConstructorInfo constructor = targetClass.GetConstructor(new System.Type[] { typeof(string) });
                                return new AnonymousPJConverter(constructor);
                            }
                            catch (MissingMethodException e)
                            {
                                throw CannotConvert(itemType, targetClass, config);
                            }
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.BOOLEAN))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(bool) || targetClass == typeof(bool))
                        {
                            return BooleanValueToBoolean.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(BooleanValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.INTEGER))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(BigInteger))
                        {
                            return IntegerValueToBigInteger.INSTANCE;
                        }
                        else if (targetClass == typeof(long) || targetClass == typeof(long))
                        {
                            return IntegerValueToLong.INSTANCE;
                        }
                        else if (targetClass == typeof(int) || targetClass == typeof(int))
                        {
                            return IntegerValueToInt.INSTANCE;
                        }
                        else if (targetClass == typeof(short) || targetClass == typeof(short?))
                        {
                            return IntegerValueToShort.INSTANCE;
                        }
                        else if (targetClass == typeof(byte) || targetClass == typeof(byte))
                        {
                            return IntegerValueToByte.INSTANCE;
                        }
                        else if (targetClass == typeof(char))
                        {
                            return IntegerValueToChar.INSTANCE;
                        }
                        else if (targetClass == typeof(double) || targetClass == typeof(double))
                        {
                            return NumericValueToDouble.INSTANCE;
                        }
                        else if (targetClass == typeof(float) || targetClass == typeof(float))
                        {
                            return NumericValueToFloat.INSTANCE;
                        }
                        else if (targetClass == typeof(BigDecimal))
                        {
                            return NumericValueToBigDecimal.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(IntegerValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.DECIMAL))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(BigDecimal))
                        {
                            return NumericValueToBigDecimal.INSTANCE;
                        }
                        else if (targetClass == typeof(double) || targetClass == typeof(double))
                        {
                            return NumericValueToDouble.INSTANCE;
                        }
                        else if (targetClass == typeof(float) || targetClass == typeof(float))
                        {
                            return NumericValueToFloat.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(BigDecimalValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.FLOAT))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(float) || targetClass == typeof(float))
                        {
                            return NumericValueToFloat.INSTANCE;
                        }
                        else if (targetClass == typeof(double) || targetClass == typeof(double))
                        {
                            return NumericValueToDouble.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(FloatValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.DOUBLE))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(double) || targetClass == typeof(double))
                        {
                            return NumericValueToDouble.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(DoubleValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            return Atomic.INSTANCE;
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.ANY_URI))
                    {
                        if (targetClass == typeof(object) || typeof(URI).IsAssignableFrom(targetClass))
                        {
                            return AnyURIValueToURI.INSTANCE;
                        }
                        else if (typeof(global::System.Uri).IsAssignableFrom(targetClass))
                        {
                            return AnyURIValueToSystemUri.INSTANCE;
                        }
                        else if (targetClass == typeof(string) || targetClass == typeof(string))
                        {
                            return StringItemToString.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(AnyURIValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.QNAME))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(System.Xml.XmlQualifiedName))
                        {
                            return QualifiedNameValueToQName.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(QNameValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.NOTATION))
                    {
                        if (targetClass == typeof(object) || targetClass == typeof(System.Xml.XmlQualifiedName))
                        {
                            return QualifiedNameValueToQName.INSTANCE;
                        }
                        else if (targetClass.IsAssignableFrom(typeof(NotationValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.DURATION))
                    {
                        if (targetClass.IsAssignableFrom(typeof(DurationValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.DATE_TIME))
                    {
                        if (targetClass.IsAssignableFrom(typeof(DateTimeValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else if (targetClass == typeof(global::System.DateTime))
                        {
                            return CalendarValueToDateTime.INSTANCE;
                        }
                        else if (targetClass == typeof(global::System.DateTimeOffset))
                        {
                            return CalendarValueToDateTimeOffset.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.DATE))
                    {
                        if (targetClass.IsAssignableFrom(typeof(DateValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else if (targetClass == typeof(global::System.DateTime))
                        {
                            return CalendarValueToDateTime.INSTANCE;
                        }
                        else if (targetClass == typeof(global::System.DateTimeOffset))
                        {
                            return CalendarValueToDateTimeOffset.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.TIME))
                    {
                        if (targetClass.IsAssignableFrom(typeof(TimeValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else if (targetClass == typeof(global::System.DateTime))
                        {
                            return CalendarValueToDateTime.INSTANCE;
                        }
                        else if (targetClass == typeof(global::System.DateTimeOffset))
                        {
                            return CalendarValueToDateTimeOffset.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.G_YEAR))
                    {
                        if (targetClass.IsAssignableFrom(typeof(GYearValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.G_YEAR_MONTH))
                    {
                        if (targetClass.IsAssignableFrom(typeof(GYearMonthValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.G_MONTH))
                    {
                        if (targetClass.IsAssignableFrom(typeof(GMonthValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.G_MONTH_DAY))
                    {
                        if (targetClass.IsAssignableFrom(typeof(GMonthDayValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.G_DAY))
                    {
                        if (targetClass.IsAssignableFrom(typeof(GDayValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.BASE64_BINARY))
                    {
                        if (targetClass.IsAssignableFrom(typeof(Base64BinaryValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else if (th.IsSubType(itemType, BuiltInAtomicType.HEX_BINARY))
                    {
                        if (targetClass.IsAssignableFrom(typeof(HexBinaryValue)))
                        {
                            return Identity.INSTANCE;
                        }
                        else
                        {
                            throw CannotConvert(itemType, targetClass, config);
                        }
                    }
                    else
                    {
                        return Atomic.INSTANCE;
                    }
                }
                else if (itemType is JavaExternalObjectType)
                {
                    return UnwrapExternalObject.INSTANCE;
                }
                else if (itemType is ErrorType)
                {
                    return ToNull.INSTANCE;
                }
                else if (itemType is NodeTest)
                {
                    if (typeof(NodeInfo).IsAssignableFrom(targetClass))
                    {
                        return Identity.INSTANCE;
                    }
                    else
                    {
                        return General.INSTANCE;
                    }
                }
                else
                {

                    // ItemType is item()
                    return General.INSTANCE;
                }
            }
            else
            {

                // Cardinality allows many (but target type is not a collection)
                return General.INSTANCE;
            }
        }

        private static XPathException CannotConvert(ItemType source, System.Type target, Configuration config)
        {
            return new XPathException("Cannot convert from " + source + " to " + target.FullName);
        }

        public static PJConverter AllocateNodeListCreator(Configuration config, object node)
        {
            IList<IExternalObjectModel> externalObjectModels = config.ExternalObjectModels;
            foreach (IExternalObjectModel model in externalObjectModels)
            {
                PJConverter converter = model.GetNodeListCreator(node);
                if (converter != null)
                {
                    return converter;
                }
            }

            return ToCollection.INSTANCE;
        }

        private sealed class AnonymousPJConverter : PJConverter
        {

            private readonly System.Reflection.ConstructorInfo constructor;
            public AnonymousPJConverter(System.Reflection.ConstructorInfo constructor)
            {
                this.constructor = constructor;
            }
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                try
                {
                    return constructor.Invoke(new object[] { value.Head().GetStringValue() });
                }
                catch (MissingMethodException e)
                {
                    throw new XPathException(e?.Message);
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new XPathException(e?.Message);
                }
                catch (System.Reflection.TargetInvocationException e)
                {
                    // ConstructorInfo.Invoke wraps a constructor-thrown exception in TargetInvocationException
                    // (the BCL equivalent of java.lang.reflect.InvocationTargetException).
                    throw new XPathException("Cannot convert untypedAtomic to " + targetClass.FullName + ": " + (e.InnerException ?? e).Message, "FORG0001");
                }
            }
        }

        internal sealed class ToSequenceIterator : PJConverter
        {
            public static readonly ToSequenceIterator INSTANCE = new ToSequenceIterator();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                return value.Iterate();
            }
        }

        internal sealed class ToNull : PJConverter
        {
            public static readonly ToNull INSTANCE = new ToNull();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                return null;
            }
        }

        internal sealed class ToSequenceExtent : PJConverter
        {
            public static readonly ToSequenceExtent INSTANCE = new ToSequenceExtent();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                return value.Materialize();
            }
        }

        internal sealed class ToCollection : PJConverter
        {
            public static readonly ToCollection INSTANCE = new ToCollection();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IList<object> list;
                if (targetClass.IsAssignableFrom(typeof(List<object>)))
                {
                    list = new List<object>(100);
                }
                else
                {
                    try
                    {
                        list = (Collection<object>)global::System.Activator.CreateInstance(targetClass);
                    }
                    catch (MissingMethodException e)
                    {
                        throw new XPathException("Cannot instantiate collection class " + targetClass).WithXPathContext(context);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        throw new XPathException("Cannot access collection class " + targetClass).WithXPathContext(context);
                    }
                }

                Configuration config = context.GetConfiguration();
                ISequenceIterator iter = value.Iterate();
                IItem it;
                while ((it = iter.Next()) != null)
                {
                    if (it is AtomicValue)
                    {
                        PJConverter pj = Allocate(config, ((AtomicValue)it).GetItemType(), StaticProperty.EXACTLY_ONE, typeof(object));
                        list.Add(pj.Convert(it, typeof(object), context));
                    }
                    else if (it is IVirtualNode)
                    {
                        list.Add(((IVirtualNode)it).RealNode);
                    }
                    else
                    {
                        list.Add(it);
                    }
                }

                return list;
            }
        }

        /// <summary>
        /// Converter for use when the target class is an array
        /// </summary>
        internal sealed class ToArray : PJConverter
        {
            private readonly PJConverter itemConverter;
            public ToArray(PJConverter itemConverter)
            {
                this.itemConverter = itemConverter;
            }

            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                if (value is IAnyExternalObject && targetClass.IsAssignableFrom(((IAnyExternalObject)value).WrappedObject.GetType()))
                {
                    return ((IAnyExternalObject)value).WrappedObject;
                }

                System.Type componentClass = targetClass.GetElementType();
                IList<object> list = new List<object>(20);
                ISequenceIterator iter = value.Iterate();
                for (IItem item; (item = iter.Next()) != null;)
                {
                    object obj = itemConverter.Convert(item, componentClass, context);
                    if (obj != null)
                    {
                        list.Add(obj);
                    }
                }

                object array = System.Array.CreateInstance((System.Type)componentClass, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    ((System.Array)array).SetValue(list[i], i);
                }

                return array; //return list.toArray((Object[])array);
            }
        }

        internal sealed class ToOne : PJConverter
        {
            public static readonly ToOne INSTANCE = new ToOne();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {

                // Assume all the type checking has already been done
                return new One<object>(value.Head());
            }
        }

        internal sealed class ToZeroOrOne : PJConverter
        {
            public static readonly ToZeroOrOne INSTANCE = new ToZeroOrOne();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {

                // Assume all the type checking has already been done
                return new ZeroOrOne<object>(value.Head());
            }
        }

        internal sealed class ToOneOrMore : PJConverter
        {
            public static readonly ToOneOrMore INSTANCE = new ToOneOrMore();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                return OneOrMore<object>.MakeOneOrMore(value);
            }
        }

        internal sealed class ToZeroOrMore : PJConverter
        {
            public static readonly ToZeroOrMore INSTANCE = new ToZeroOrMore();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                return ZeroOrMore<object>.FromSequenceIterator<object>(value.Iterate());
            }
        }

        internal sealed class Identity : PJConverter
        {
            public static readonly Identity INSTANCE = new Identity();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                if (value is Closure)
                {
                    value = ((Closure)value).Reduce();
                }

                if (value is ZeroOrOne<object>)
                {
                    value = (ISequence)((ZeroOrOne<object>)value).Head();
                }

                if (value is IVirtualNode)
                {
                    object obj = ((IVirtualNode)value).RealNode;
                    if (targetClass.IsAssignableFrom(obj.GetType()))
                    {
                        return obj;
                    }
                }

                if (targetClass.IsAssignableFrom(value.GetType()))
                {
                    return value;
                }
                else
                {
                    IGroundedValue gv = value.Materialize();
                    if (targetClass.IsAssignableFrom(gv.GetType()))
                    {
                        return gv;
                    }

                    gv = gv.Reduce();
                    if (targetClass.IsAssignableFrom(gv.GetType()))
                    {
                        return gv;
                    }

                    if (gv.GetLength() == 0)
                    {
                        return null;
                    }
                    else
                    {
                        throw new XPathException("Cannot convert value " + value.GetType() + " of type " + SequenceTool.GetItemType(value, context.GetConfiguration().GetTypeHierarchy()) + " to class " + targetClass.FullName);
                    }
                }
            }
        }

        internal sealed class UnwrapExternalObject : PJConverter
        {
            public static readonly UnwrapExternalObject INSTANCE = new UnwrapExternalObject();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IItem head = value.Head();
                if (head == null)
                {
                    return null;
                }

                if (!(head is IAnyExternalObject))
                {
                    if (typeof(ISequence).IsAssignableFrom(targetClass))
                    {
                        head = new ObjectValue<object>(value, targetClass);
                    }
                    else
                    {
                        throw new XPathException("Expected external object of class " + targetClass.FullName + ", got " + head.GetType());
                    }
                }

                object obj = ((IAnyExternalObject)head).WrappedObject;
                if (!targetClass.IsAssignableFrom(obj.GetType()))
                {
                    throw new XPathException("External object has wrong class (is " + obj.GetType().FullName + ", expected " + targetClass.FullName + ")");
                }

                return obj;
            }
        }

        internal sealed class StringItemToString : PJConverter
        {
            public static readonly StringItemToString INSTANCE = new StringItemToString();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IItem first = value.Head();
                return first == null ? null : first.GetStringValue();
            }
        }

        internal sealed class StringItemToUnicodeString : PJConverter
        {
            public static readonly StringItemToUnicodeString INSTANCE = new StringItemToUnicodeString();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IItem first = value.Head();
                return first == null ? null : first.UnicodeStringValue;
            }
        }

        internal sealed class StringItemToChar : PJConverter
        {
            public static readonly StringItemToChar INSTANCE = new StringItemToChar();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IItem first = value.Head();
                if (first == null)
                {
                    return null;
                }

                string str = first.GetStringValue();
                if (str.Length == 1)
                {
                    return str[0];
                }
                else
                {
                    throw new XPathException("Cannot convert xs:string to Java char unless length is 1").WithXPathContext(context).WithErrorCode(DAXonErrorCode.SXJE0005);
                }
            }
        }

        internal sealed class BooleanValueToBoolean : PJConverter
        {
            public static readonly BooleanValueToBoolean INSTANCE = new BooleanValueToBoolean();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                BooleanValue bv = (BooleanValue)value.Head();
                return bv.GetBooleanValue();
            }
        }

        internal sealed class IntegerValueToBigInteger : PJConverter
        {
            public static readonly IntegerValueToBigInteger INSTANCE = new IntegerValueToBigInteger();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IntegerValue val = (IntegerValue)value.Head();
                return val == null ? null : val.AsBigInteger();
            }
        }

        internal sealed class IntegerValueToLong : PJConverter
        {
            public static readonly IntegerValueToLong INSTANCE = new IntegerValueToLong();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IntegerValue iv = (IntegerValue)value.Head();
                return iv.LongValue();
            }
        }

        internal sealed class IntegerValueToInt : PJConverter
        {
            public static readonly IntegerValueToInt INSTANCE = new IntegerValueToInt();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IntegerValue iv = (IntegerValue)value.Head();
                return (int)iv.LongValue();
            }
        }

        internal sealed class IntegerValueToShort : PJConverter
        {
            public static readonly IntegerValueToShort INSTANCE = new IntegerValueToShort();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IntegerValue iv = (IntegerValue)value.Head();
                return (short)iv.LongValue();
            }
        }

        internal sealed class IntegerValueToByte : PJConverter
        {
            public static readonly IntegerValueToByte INSTANCE = new IntegerValueToByte();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IntegerValue iv = (IntegerValue)value.Head();
                return (byte)iv.LongValue();
            }
        }

        internal sealed class IntegerValueToChar : PJConverter
        {
            public static readonly IntegerValueToChar INSTANCE = new IntegerValueToChar();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                IntegerValue iv = (IntegerValue)value.Head();
                return (char)iv.LongValue();
            }
        }

        internal sealed class NumericValueToBigDecimal : PJConverter
        {
            public static readonly NumericValueToBigDecimal INSTANCE = new NumericValueToBigDecimal();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                NumericValue nv = (NumericValue)value.Head();
                return nv == null ? null : nv.GetDecimalValue();
            }
        }

        internal sealed class NumericValueToDouble : PJConverter
        {
            public static readonly NumericValueToDouble INSTANCE = new NumericValueToDouble();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                NumericValue nv = (NumericValue)value.Head();
                return nv.GetDoubleValue();
            }
        }

        internal sealed class NumericValueToFloat : PJConverter
        {
            public static readonly NumericValueToFloat INSTANCE = new NumericValueToFloat();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                NumericValue nv = (NumericValue)value.Head();
                return nv.GetFloatValue();
            }
        }

        internal sealed class AnyURIValueToURI : PJConverter
        {
            public static readonly AnyURIValueToURI INSTANCE = new AnyURIValueToURI();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                AnyURIValue av = (AnyURIValue)value.Head();
                try
                {
                    return av == null ? null : new URI(((AnyURIValue)value).GetStringValue());
                }
                catch (URISyntaxException err)
                {
                    throw new XPathException("The anyURI value '" + value + "' is not an acceptable Java URI");
                }
            }
        }

        internal sealed class AnyURIValueToSystemUri : PJConverter
        {
            public static readonly AnyURIValueToSystemUri INSTANCE = new AnyURIValueToSystemUri();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                AnyURIValue av = (AnyURIValue)value.Head();
                try
                {
                    return av == null ? null : new global::System.Uri(((AnyURIValue)value).GetStringValue());
                }
                catch (UriFormatException err)
                {
                    throw new XPathException("The anyURI value '" + value + "' is not an acceptable absolute URI");
                }
            }
        }

        internal sealed class QualifiedNameValueToQName : PJConverter
        {
            public static readonly QualifiedNameValueToQName INSTANCE = new QualifiedNameValueToQName();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                QualifiedNameValue qv = (QualifiedNameValue)value.Head();
                return qv == null ? null : qv.ToXmlQualifiedName();
            }
        }

        internal sealed class CalendarValueToDateTime : PJConverter
        {
            public static readonly CalendarValueToDateTime INSTANCE = new CalendarValueToDateTime();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                CalendarValue cv = (CalendarValue)value.Head();
                return cv == null ? (object)null : cv.ToSystemDateTimeUtc();
            }
        }

        internal sealed class CalendarValueToDateTimeOffset : PJConverter
        {
            public static readonly CalendarValueToDateTimeOffset INSTANCE = new CalendarValueToDateTimeOffset();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                CalendarValue cv = (CalendarValue)value.Head();
                return cv == null ? (object)null : cv.ToSystemDateTimeOffset();
            }
        }

        //    public static class Atomization extends PJConverter {
        //
        //        public static final Atomization INSTANCE = new Atomization();
        //
        //        public Object convert(ISequence value, Class targetClass, IXPathContext context) throws XPathException {
        //            List<AtomicValue> val = new List<AtomicValue>();
        //            ISequenceIterator @base = value.iterate();
        //                IItem item = atomized.next();
        //                if (item == null) {
        //                    break;
        //                }
        //            if (val.size() == 1) {
        //                return val.get(0);
        //            } else {
        //                return
        //
        //
        internal sealed class Atomic : PJConverter
        {
            public static readonly Atomic INSTANCE = new Atomic();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {

                // TODO: not really worth separating from General
                AtomicValue item = (AtomicValue)value.Head();
                if (item == null)
                {
                    return null;
                }

                Configuration config = context.GetConfiguration();
                PJConverter converter = Allocate(config, item.GetItemType(), StaticProperty.EXACTLY_ONE, targetClass);
                return converter.Convert(item, targetClass, context);
            }
        }

        internal sealed class General : PJConverter
        {
            public static readonly General INSTANCE = new General();
            public override object Convert(ISequence value, System.Type targetClass, IXPathContext context)
            {
                Configuration config = context.GetConfiguration();
                IGroundedValue gv = value.Materialize();
                PJConverter converter = Allocate(config, SequenceTool.GetItemType(gv, config.GetTypeHierarchy()), SequenceTool.GetCardinality(gv), targetClass);
                if (converter is General)
                {
                    converter = Identity.INSTANCE;
                }

                return converter.Convert(gv, targetClass, context);
            }
        }
    }
}

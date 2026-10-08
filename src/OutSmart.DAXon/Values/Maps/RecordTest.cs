////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values.Maps
{
    public class RecordTest : AnyFunctionType, IRecordType
    {
        public static RecordTest VALUE_RECORD = NonExtensible(new Field("value", SequenceType.ANY_SEQUENCE, false));
        public static RecordTest KEY_VALUE_RECORD = NonExtensible(new Field("key", SequenceType.ATOMIC_SEQUENCE, false), new Field("value", SequenceType.ANY_SEQUENCE, false));
        private readonly Dictionary<string, SequenceType> fieldTypes = new Dictionary<string, SequenceType>();
        private readonly HashSet<string> optionalFields = new HashSet<string>();
        private bool _extensible;

        public IEnumerable<string> FieldNames => fieldTypes.Keys;

        public override SequenceType[] ArgumentTypes => new SequenceType[]
            {
                SequenceType.SINGLE_ATOMIC
            };

        public override SequenceType ResultType
        {
            get
            {
                if (_extensible)
                {
                    return SequenceType.ANY_SEQUENCE;
                }
                else
                {
                    ItemType resultType = null;
                    bool allowsMany = false;
                    foreach (KeyValuePair<string, SequenceType> field in fieldTypes)
                    {
                        if (resultType == null)
                        {
                            resultType = field.Value.PrimaryType;
                        }
                        else
                        {
                            resultType = Types.Type.GetCommonSuperType(resultType, field.Value.PrimaryType);
                        }

                        allowsMany = allowsMany || Cardinality.AllowsMany(field.Value.GetCardinality());
                    }

                    return SequenceType.MakeSequenceType(resultType, allowsMany ? StaticProperty.ALLOWS_ZERO_OR_MORE : StaticProperty.ALLOWS_ZERO_OR_ONE);
                }
            }
        }

        public override double DefaultPriority
        {
            get
            {

                // TODO: this algorithm means that adding fields to the record type reduces its priority, which is wrong
                double prio = 1;
                foreach (SequenceType st in fieldTypes.Values)
                {
                    prio *= st.PrimaryType.GetNormalizedDefaultPriority();
                }

                return _extensible ? 0.5 + prio / 2 : prio;
            }
        }

        public override string BasicAlphaCode => "FM";

        public RecordTest()
        {
        }

        public RecordTest(IList<string> names, IList<SequenceType> types, IList<string> optionalFieldNames, bool extensible)
        {
            SetDetails(names, types, optionalFieldNames, extensible);
        }

        public static RecordTest Extensible(params Field[] fields)
        {
            return MakeRecordTest(true, fields);
        }

        public static RecordTest NonExtensible(params Field[] fields)
        {
            return MakeRecordTest(false, fields);
        }

        private static RecordTest MakeRecordTest(bool extensible, params Field[] fields)
        {
            IList<string> fieldNames = new List<string>(fields.Length);
            IList<string> optionalFieldNames = new List<string>(fields.Length);
            IList<SequenceType> fieldTypes = new List<SequenceType>(fields.Length);
            foreach (Field field in fields)
            {
                fieldNames.Add(field.name);
                fieldTypes.Add(field.type);
                if (field.optional)
                {
                    optionalFieldNames.Add(field.name);
                }
            }

            return new RecordTest(fieldNames, fieldTypes, optionalFieldNames, extensible);
        }

        public virtual void SetDetails(IList<string> names, IList<SequenceType> types, IList<string> optionalFieldNames, bool extensible)
        {
            for (int i = 0; i < names.Count; i++)
            {
                fieldTypes[names[i]] = types[i];
            }

            optionalFields.AddRange(optionalFieldNames);
            this._extensible = extensible;
        }

        public override Genre GetGenre()
        {
            return Genre.MAP;
        }

        public override bool IsMapType()
        {
            return true;
        }

        public override bool IsArrayType()
        {
            return false;
        }

        public SequenceType GetFieldType(string field)
        {
            return fieldTypes.GetOrDefault(field);
        }

        public bool IsOptionalField(string field)
        {
            return optionalFields.Contains(field);
        }

        public bool IsExtensible()
        {
            return _extensible;
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            if (!(item is MapItem))
            {
                return false;
            }

            MapItem map = (MapItem)item;
            foreach (KeyValuePair<string, SequenceType> field in fieldTypes)
            {
                IGroundedValue val = map[new StringValue(field.Key)];
                if (val == null)
                {
                    if (!IsOptionalField(field.Key))
                    {
                        return false;
                    }
                }
                else if (!field.Value.Matches(val, th))
                {
                    return false;
                }
            }

            if (!_extensible)
            {
                IAtomicIterator keyIter = map.Keys();
                AtomicValue key;
                while ((key = keyIter.Next()) != null)
                {
                    if (!(key is StringValue) || !fieldTypes.ContainsKey(key.GetStringValue()))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public virtual int GetArity()
        {
            return 1;
        }

        public override string ToString()
        {
            return MakeString(st => st.ToString());
        }

        public string ToExportString()
        {
            return MakeString(st => st.ToExportString());
        }

        private string MakeString(Func<SequenceType, string> show)
        {
            StringBuilder sb = new StringBuilder(100);
            sb.Append("record(");
            bool first = true;
            foreach (KeyValuePair<string, SequenceType> field in fieldTypes)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(", ");
                }

                if (NameChecker.IsValidNCName(field.Key))
                {
                    sb.Append(field.Key);
                }
                else
                {
                    sb.Append('"').Append(field.Key).Append('"');
                }

                if (IsOptionalField(field.Key))
                {
                    sb.Append('?');
                }

                sb.Append(" as ");
                if (field.Value.PrimaryType == this)
                {
                    sb.Append("..").Append(Cardinality.GetOccurrenceIndicator(field.Value.GetCardinality()));
                }
                else
                {
                    sb.Append(show(field.Value));
                }
            }

            if (IsExtensible())
            {
                sb.Append(", *");
            }

            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>
        /// Test whether this function type equals another function type
        /// </summary>
        public override bool Equals(object other)
        {
            return this == other || other is RecordTest && _extensible == ((RecordTest)other)._extensible && fieldTypes.Equals(((RecordTest)other).fieldTypes) && optionalFields.Equals(((RecordTest)other).optionalFields);
        }

        public override int GetHashCode()
        {

            // Need to avoid infinite recursion for self-reference fields
            int h = 0x27ca481f;
            foreach (KeyValuePair<string, SequenceType> entry in fieldTypes)
            {
                h ^= entry.Key.GetHashCode();
                if (entry.Value.PrimaryType == this)
                {
                    h ^= 0x05050505;
                }
                else
                {
                    h ^= entry.Value.GetHashCode();
                }
            }

            return h;
        }

        public override Affinity Relationship(IFunctionItemType other, TypeHierarchy th)
        {
            if (other == AnyFunctionType.GetInstance())
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (other is RecordTest)
            {
                return RecordTypeRelationship((RecordTest)other, th);
            }
            else if (other == MapType.ANY_MAP_TYPE)
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (other.IsArrayType())
            {
                return Affinity.DISJOINT;
            }
            else if (other is MapType)
            {
                return RecordToMapRelationship((MapType)other, th);
            }
            else
            {
                Affinity rel;
                rel = new SpecificFunctionType(ArgumentTypes, ResultType).Relationship(other, th);
                return rel;
            }
        }

        private Affinity RecordToMapRelationship(MapType other, TypeHierarchy th)
        {
            IAtomicType recordKeyType = IsExtensible() ? BuiltInAtomicType.ANY_ATOMIC : BuiltInAtomicType.STRING;
            Affinity keyRel = th.Relationship(recordKeyType, other.KeyType);
            if (keyRel == Affinity.DISJOINT)
            {
                return Affinity.DISJOINT;
            }


            // Handle map(xxx, item()*)
            if (other.ValueType.PrimaryType.Equals(AnyItemType.GetInstance()) && other.ValueType.GetCardinality() == StaticProperty.ALLOWS_ZERO_OR_MORE)
            {
                if (keyRel == Affinity.SUBSUMED_BY || keyRel == Affinity.SAME_TYPE)
                {
                    return Affinity.SUBSUMED_BY;
                }
                else
                {
                    return Affinity.OVERLAPS;
                }
            }
            else if (IsExtensible())
            {
                return Affinity.OVERLAPS;
            }
            else
            {

                // The type of every field in the record must be a subtype of the map value type
                foreach (SequenceType entry in fieldTypes.Values)
                {
                    Affinity rel = th.SequenceTypeRelationship(entry, other.ValueType);
                    if (!(rel == Affinity.SUBSUMED_BY || rel == Affinity.SAME_TYPE))
                    {
                        return Affinity.OVERLAPS;
                    }
                }

                return Affinity.SUBSUMED_BY;
            }
        }

        private Affinity RecordTypeRelationship(RecordTest other, TypeHierarchy th)
        {
            HashSet<string> keys = new HashSet<string>(fieldTypes.Keys);
            keys.AddRange(other.fieldTypes.Keys);
            bool foundSubsuming = false;
            bool foundSubsumed = false;
            bool foundOverlap = false;
            if (IsExtensible())
            {
                if (!other.IsExtensible())
                {
                    foundSubsuming = true;
                }
            }
            else if (other.IsExtensible())
            {
                foundSubsumed = true;
            }

            foreach (string key in keys)
            {
                SequenceType t1 = fieldTypes.GetOrDefault(key);
                SequenceType t2 = other.fieldTypes.GetOrDefault(key);
                if (t1 == null)
                {
                    if (IsExtensible())
                    {
                        foundSubsuming = true;
                    }
                    else if (Cardinality.AllowsZero(t2.GetCardinality()))
                    {
                        foundOverlap = true;
                    }
                    else
                    {
                        return Affinity.DISJOINT;
                    }
                }
                else if (t2 == null)
                {
                    if (other.IsExtensible())
                    {
                        foundSubsumed = true;
                    }
                    else if (Cardinality.AllowsZero(t1.GetCardinality()))
                    {
                        foundOverlap = true;
                    }
                    else
                    {
                        return Affinity.DISJOINT;
                    }
                }
                else
                {
                    Affinity a = th.SequenceTypeRelationship(t1, t2);
                    switch (a)
                    {
                        case Affinity.SAME_TYPE:
                            break;
                        case Affinity.SUBSUMED_BY:
                            foundSubsumed = true;
                            break;
                        case Affinity.SUBSUMES:
                            foundSubsuming = true;
                            break;
                        case Affinity.OVERLAPS:
                            foundOverlap = true;
                            break;
                        case Affinity.DISJOINT:
                            return Affinity.DISJOINT;
                    }
                }
            }

            if (foundOverlap || (foundSubsumed && foundSubsuming))
            {
                return Affinity.OVERLAPS;
            }
            else if (foundSubsuming)
            {
                return Affinity.SUBSUMES;
            }
            else if (foundSubsumed)
            {
                return Affinity.SUBSUMED_BY;
            }
            else
            {
                return Affinity.SAME_TYPE;
            }
        }

        public string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            if (item is MapItem)
            {
                foreach (KeyValuePair<string, SequenceType> entry in fieldTypes)
                {
                    string key = entry.Key;
                    SequenceType required = entry.Value;
                    IGroundedValue value = ((MapItem)item)[new StringValue(key)];
                    if (value == null)
                    {
                        if (!Cardinality.AllowsZero(required.GetCardinality()) && !IsOptionalField(key))
                        {
                            return ("Field " + key + " is absent; it must have a value");
                        }
                    }
                    else
                    {
                        if (!required.Matches(value, th))
                        {
                            string s = "Field " + key + " has value " + Err.DepictSequence(value) + " which does not match the required type " + required.ToString();
                            string more = required.ExplainMismatch(value, th);
                            if (more != null)
                            {
                                s += ". " + more;
                            }

                            return (s);
                        }
                    }
                }

                if (!_extensible)
                {
                    IAtomicIterator keyIter = ((MapItem)item).Keys();
                    AtomicValue key;
                    while ((key = keyIter.Next()) != null)
                    {
                        if (!(key is StringValue))
                        {
                            return ("Undeclared field " + key + " is present, but it is not a string, and the record type is not extensible");
                        }
                        else if (!fieldTypes.ContainsKey(key.GetStringValue()))
                        {
                            return ("Undeclared field " + key + " is present, but the record type is not extensible");
                        }
                    }
                }
            }

            return null;
        }

        public override Expression MakeFunctionSequenceCoercer(Expression exp, Func<RoleDiagnostic> role, bool allow40)
        {
            return new SpecificFunctionType(ArgumentTypes, ResultType).MakeFunctionSequenceCoercer(exp, role, false);
        }
        public class Field
        {
            public string name;
            public SequenceType type;
            public bool optional;
            public Field(string name, SequenceType type, bool optional)
            {
                this.name = name;
                this.type = type;
                this.optional = optional;
            }
        }
    }
}

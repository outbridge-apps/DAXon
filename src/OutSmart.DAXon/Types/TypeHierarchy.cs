////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Collections;
using static OutSmart.DAXon.Types.Affinity;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Caching;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Types
{
    public class TypeHierarchy
    {
        // Bounded and concurrent. Concurrent because there is ONE TypeHierarchy per Configuration,
        // i.e. per Processor, and Relationship() writes to it from every compile - compiles do run
        // concurrently on a shared Processor (the documented host wrapper's Sheets.GetOrAdd can
        // call the factory on two threads at once, and xsl:evaluate / fn:transform compile
        // mid-transform), so a plain Dictionary was open to a torn resize, whose net472 symptom is
        // not a crash but an endless loop inside FindEntry. Bounded (round A4) because it is a memo
        // of a PURE function: a miss only costs a recompute, never a wrong answer, and dropping an
        // XsltExecutable did not release its entries - so a host compiling throwaway variants grew
        // this map for the Processor's life.
        private readonly ClockCache<ItemTypePair, Affinity> map;
        protected Configuration config;

        public virtual ItemType GenericFunctionItemType => AnyFunctionType.GetInstance();

        /// <summary>
        /// Cap on memoized type-pair relationships. The memo is compile-driven - a realistic
        /// stylesheet queries a few dozen distinct pairs - so this holds the whole working set
        /// of any plausible run and only bites a host that compiles thousands of DISTINCT sheets,
        /// which is exactly the case that used to accumulate for the Processor's life.
        /// </summary>
        public const int MemoCapacity = 4096;

        /// <summary>Memoized type-pair relationships currently held. Diagnostic.</summary>
        public int MemoCount => map.Count;

        public TypeHierarchy(Configuration config)
        {
            this.config = config;
            map = new ClockCache<ItemTypePair, Affinity>(MemoCapacity);
        }

        public static ISchemaType GetNearestNamedType(ISchemaType type)
        {
            while (type.IsAnonymousType())
            {
                type = type.BaseType;
            }

            return type;
        }

        public virtual IGroundedValue ApplyFunctionConversionRules(ISequence value, Values.SequenceType requiredType, Func<RoleDiagnostic> roleSupplier, ILocation locator)
        {
            try
            {
                IGroundedValue groundedValue = value.Materialize();
                if (requiredType.Matches(groundedValue, this))
                {
                    return groundedValue;
                }

                ItemType suppliedItemType = SequenceTool.GetItemType(value, this);
                ISequenceIterator iterator = groundedValue.Iterate();
                ItemType requiredItemType = requiredType.PrimaryType;
                if (requiredItemType.IsPlainType())
                {

                    // step 1: apply atomization if necessary
                    if (!suppliedItemType.IsPlainType())
                    {
                        try
                        {
                            iterator = Atomizer.GetAtomizingIterator(iterator, false);
                        }
                        catch (XPathException e)
                        {
                            RoleDiagnostic role = roleSupplier();
                            ValidationFailure vf = new ValidationFailure("Failed to atomize the " + role.GetMessage() + ": " + e.Message);
                            vf.SetErrorCode("XPTY0117");
                            throw vf.MakeException();
                        }

                        suppliedItemType = suppliedItemType.GetAtomizedItemType();
                    }


                    // step 2: convert untyped atomic values to target item type
                    if (Relationship(suppliedItemType, BuiltInAtomicType.UNTYPED_ATOMIC) != DISJOINT && !IsSubType(BuiltInAtomicType.UNTYPED_ATOMIC, requiredItemType))
                    {
                        bool nsSensitive = ((ISimpleType)requiredItemType).IsNamespaceSensitive();
                        IItemMappingFunction converter;
                        if (nsSensitive)
                        {
                            converter = ItemMapper.Of((item) =>
                            {
                                if (item is AtomicValue && ((AtomicValue)item).IsUntypedAtomic())
                                {
                                    RoleDiagnostic role = roleSupplier();
                                    ValidationFailure vf = new ValidationFailure("Failed to convert the " + role.GetMessage() + ": " + "Implicit conversion of untypedAtomic value to " + requiredItemType + " is not allowed");
                                    vf.SetErrorCode("XPTY0117");
                                    throw vf.MakeException();
                                }
                                else
                                {
                                    return item;
                                }
                            });
                        }
                        else if (((ISimpleType)requiredItemType).IsUnionType())
                        {
                            ConversionRules rules = config.GetConversionRules();
                            converter = ItemMapper.Of((item) =>
                            {
                                if (item is AtomicValue && ((AtomicValue)item).IsUntypedAtomic())
                                {
                                    try
                                    {
                                        return ((ISimpleType)requiredItemType).GetTypedValue(item.UnicodeStringValue, null, rules).Head();
                                    }
                                    catch (ValidationException ve)
                                    {
                                        throw ve.WithErrorCode("XPTY0004");
                                    }
                                }
                                else
                                {
                                    return item;
                                }
                            });
                        }
                        else
                        {
                            converter = ItemMapper.Of((item) =>
                            {
                                if (item is AtomicValue && ((AtomicValue)item).IsUntypedAtomic())
                                {
                                    return (IItem)Converter.Convert((StringValue)item, (IAtomicType)requiredItemType, config.GetConversionRules());
                                }
                                else
                                {
                                    return item;
                                }
                            });
                        }

                        iterator = new ItemMappingIterator(iterator, converter, true);
                    }


                    // step 3: apply numeric promotion
                    if (requiredItemType.Equals(BuiltInAtomicType.DOUBLE))
                    {
                        IItemMappingFunction promoter = ItemMapper.Of((item) =>
                        {
                            if (item is NumericValue)
                            {
                                return new DoubleValue(((NumericValue)item).GetDoubleValue());
                            }
                            else
                            {
                                throw new XPathException("Failed to convert the " + roleSupplier().GetMessage() + ": " + "Cannot promote non-numeric value to xs:double", "XPTY0004");
                            }
                        });
                        iterator = new ItemMappingIterator(iterator, promoter, true);
                    }
                    else if (requiredItemType.Equals(BuiltInAtomicType.FLOAT))
                    {
                        IItemMappingFunction promoter = ItemMapper.Of((item) =>
                        {
                            if (item is DoubleValue)
                            {
                                RoleDiagnostic role = roleSupplier();
                                throw new XPathException("Failed to convert the " + role.GetMessage() + ": " + "Cannot promote xs:double value to xs:float", "XPTY0004");
                            }
                            else if (item is NumericValue)
                            {
                                return new FloatValue((float)((NumericValue)item).GetDoubleValue());
                            }
                            else
                            {
                                RoleDiagnostic role = roleSupplier();
                                throw new XPathException("Failed to convert the " + role.GetMessage() + ": " + "Cannot promote non-numeric value to xs:float", "XPTY0004");
                            }
                        });
                        iterator = new ItemMappingIterator(iterator, promoter, true);
                    }


                    // step 4: apply URI-to-string promotion
                    if (requiredItemType.Equals(BuiltInAtomicType.STRING) && Relationship(suppliedItemType, BuiltInAtomicType.ANY_URI) != DISJOINT)
                    {
                        IItemMappingFunction promoter = ItemMapper.Of((item) =>
                        {
                            if (item is AnyURIValue)
                            {
                                return ((AnyURIValue)item).ConvertToString();
                            }
                            else
                            {
                                return item;
                            }
                        });
                        iterator = new ItemMappingIterator(iterator, promoter, true);
                    }
                }


                // step 5: apply function coercion
                iterator = ApplyFunctionCoercion(iterator, suppliedItemType, requiredItemType, locator);

                // Add a check that the values conform to the required type
                Affinity relation = Relationship(suppliedItemType, requiredItemType);
                if (!(relation == SAME_TYPE || relation == SUBSUMED_BY))
                {
                    ItemTypeCheckingFunction itemChecker = new ItemTypeCheckingFunction(requiredItemType, roleSupplier, locator, config);
                    iterator = new ItemMappingIterator(iterator, itemChecker, true);
                }

                if (requiredType.GetCardinality() != StaticProperty.ALLOWS_ZERO_OR_MORE)
                {
                    iterator = new CardinalityCheckingIterator(iterator, requiredType.GetCardinality(), roleSupplier, locator);
                }

                return SequenceTool.ToGroundedValue(iterator);
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        protected virtual ISequenceIterator ApplyFunctionCoercion(ISequenceIterator iterator, ItemType suppliedItemType, ItemType requiredItemType, ILocation locator)
        {
            if (requiredItemType is IFunctionItemType && !((IFunctionItemType)requiredItemType).IsMapType() && !((IFunctionItemType)requiredItemType).IsArrayType() && !(Relationship(requiredItemType, suppliedItemType) == Affinity.SUBSUMES))
            {
                if (requiredItemType == AnyFunctionType.GetInstance())
                {

                    // no action (the type checking is added later)
                    return iterator;
                }
                else
                {
                    FunctionSequenceCoercer.Coercer coercer = new FunctionSequenceCoercer.Coercer((SpecificFunctionType)requiredItemType, config, locator, false);
                    return new ItemMappingIterator(iterator, coercer, true);
                }
            }
            else
            {
                return iterator;
            }
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual bool IsSubType(ItemType subtype, ItemType supertype)
        {
            Affinity relation = Relationship(subtype, supertype);
            return relation == SAME_TYPE || relation == SUBSUMED_BY;
        }

        public virtual Affinity Relationship(ItemType t1, ItemType t2)
        {
            if (t1 == null)
                throw new NullReferenceException();
            if (t2 == null)
                throw new NullReferenceException();
            t1 = Stabilize(t1);
            t2 = Stabilize(t2);
            if (t1.Equals(t2))
            {
                return SAME_TYPE;
            }


            // Before we look in the cache, which involves computing hash keys, check for some simple and common cases
            if (t2 is AnyItemType)
            {
                return SUBSUMED_BY;
            }

            if (t1 is AnyItemType)
            {
                return SUBSUMES;
            }

            if (t1 is BuiltInAtomicType && t2 is BuiltInAtomicType)
            {
                if (t1.BasicAlphaCode.StartsWith(t2.BasicAlphaCode, StringComparison.Ordinal))
                {
                    return SUBSUMED_BY;
                }
                else if (t2.BasicAlphaCode.StartsWith(t1.BasicAlphaCode, StringComparison.Ordinal))
                {
                    return SUBSUMES;
                }
                else
                {
                    return DISJOINT;
                }
            }

            if (t1 is ErrorType)
            {
                return SUBSUMED_BY;
            }

            if (t2 is ErrorType)
            {
                return SUBSUMES;
            }

            ItemTypePair pair = new ItemTypePair(t1, t2);
            if (map.TryGet(pair, out Affinity cached))
            {
                return cached;
            }

            // Two threads may compute the same pair at once; ComputeRelationship is a pure function
            // of the pair, so either result is the same and last-write-wins is correct.
            Affinity affinity = ComputeRelationship(t1, t2);
            map.Set(pair, affinity);
            return affinity;
        }

        private static ItemType Stabilize(ItemType @in)
        {
            if (@in is SameNameTest)
            {

                // we don't want to put a SameNameTest in the cache because it locks down the referenced document
                return ((SameNameTest)@in).EquivalentNameTest;
            }
            else
            {
                return @in;
            }
        }

        private Affinity ComputeRelationship(ItemType t1, ItemType t2)
        {

            RequireTrueItemType(t1);
            RequireTrueItemType(t2);
            try
            {
                if (t1 == t2)
                {
                    return SAME_TYPE;
                }

                if (t1 is AnyItemType)
                {
                    if (t2 is AnyItemType)
                    {
                        return SAME_TYPE;
                    }
                    else
                    {
                        return SUBSUMES;
                    }
                }
                else if (t2 is AnyItemType)
                {
                    return SUBSUMED_BY;
                }
                else if (t1.IsPlainType())
                {
                    if (t2 is NodeTest || t2 is IFunctionItemType || t2 is JavaExternalObjectType)
                    {
                        return DISJOINT;
                    }
                    else if (t1 == BuiltInAtomicType.ANY_ATOMIC && t2.IsPlainType())
                    {
                        return SUBSUMES;
                    }
                    else if (t2 == BuiltInAtomicType.ANY_ATOMIC)
                    {
                        return SUBSUMED_BY;
                    }
                    else if (t1 is IAtomicType && t2 is IAtomicType)
                    {
                        if (((IAtomicType)t1).Fingerprint == ((IAtomicType)t2).Fingerprint)
                        {
                            return SAME_TYPE;
                        }

                        IAtomicType t = (IAtomicType)t2;
                        while (true)
                        {
                            if (((IAtomicType)t1).Fingerprint == t.Fingerprint)
                            {
                                return SUBSUMES;
                            }

                            ISchemaType st = t.BaseType;
                            if (st is IAtomicType)
                            {
                                t = (IAtomicType)st;
                            }
                            else
                            {
                                break;
                            }
                        }

                        t = (IAtomicType)t1;
                        while (true)
                        {
                            if (t.Fingerprint == ((IAtomicType)t2).Fingerprint)
                            {
                                return SUBSUMED_BY;
                            }

                            ISchemaType st = t.BaseType;
                            if (st is IAtomicType)
                            {
                                t = (IAtomicType)st;
                            }
                            else
                            {
                                break;
                            }
                        }

                        return DISJOINT;
                    }
                    else if (!t1.IsAtomicType() && t2.IsPlainType())
                    {

                        // relationship(union, atomic) or relationship(union, union)
                        HashSet<IPlainType> s1 = ToSet(((IPlainType)t1).PlainMemberTypes);
                        HashSet<IPlainType> s2 = ToSet(((IPlainType)t2).PlainMemberTypes);
                        if (!UnionOverlaps(s1, s2))
                        {
                            return DISJOINT;
                        }

                        bool gt = s1.ContainsAll(s2);
                        bool lt = s2.ContainsAll(s1);
                        if (gt && lt)
                        {
                            return SAME_TYPE;
                        }
                        else if (gt)
                        {
                            return SUBSUMES;
                        }
                        else if (lt)
                        {
                            return SUBSUMED_BY;
                        }
                        else if (UnionSubsumes(s1, s2))
                        {
                            return SUBSUMES;
                        }
                        else if (UnionSubsumes(s2, s1))
                        {
                            return SUBSUMED_BY;
                        }
                        else
                        {
                            return OVERLAPS;
                        }
                    }
                    else if (t1 is IAtomicType)
                    {

                        // relationship (atomic, union)
                        Affinity r = Relationship(t2, t1);
                        return InverseRelationship(r);
                    }
                    else
                    {

                        // all options exhausted
                        throw new InvalidOperationException();
                    }
                }
                else if (t1 is NodeTest)
                {
                    if (t2.IsPlainType() || t2 is IFunctionItemType)
                    {
                        return DISJOINT;
                    }
                    else
                    {

                        // both types are NodeTests
                        if (t1 is AnyNodeTest)
                        {
                            if (t2 is AnyNodeTest)
                            {
                                return SAME_TYPE;
                            }
                            else
                            {
                                return SUBSUMES;
                            }
                        }
                        else if (t2 is AnyNodeTest)
                        {
                            return SUBSUMED_BY;
                        }
                        else if (t2 is ErrorType)
                        {
                            return DISJOINT;
                        }
                        else
                        {

                            // first find the relationship between the node kinds allowed
                            Affinity nodeKindRelationship;
                            UType m1 = t1.GetUType();
                            UType m2 = t2.GetUType();
                            if (!m1.Overlaps(m2))
                            {
                                return DISJOINT;
                            }
                            else if (m1.Equals(m2))
                            {
                                nodeKindRelationship = SAME_TYPE;
                            }
                            else if (m2.Subsumes(m1))
                            {
                                nodeKindRelationship = SUBSUMED_BY;
                            }
                            else if (m1.Subsumes(m2))
                            {
                                nodeKindRelationship = SUBSUMES;
                            }
                            else
                            {
                                nodeKindRelationship = OVERLAPS;
                            }


                            // Now find the relationship between the node names allowed.  See bug 3713
                            Affinity nodeNameRelationship;
                            IntSet on1 = ((NodeTest)t1).RequiredNodeNames;
                            IntSet on2 = ((NodeTest)t2).RequiredNodeNames;
                            if (t1 is IQNameTest && t2 is IQNameTest)
                            {
                                nodeNameRelationship = NameTestRelationship((IQNameTest)t1, (IQNameTest)t2);
                            }
                            else if (on1 != null && on1 is IntUniversalSet)
                            {
                                if (on2 != null && on2 is IntUniversalSet)
                                {
                                    nodeNameRelationship = SAME_TYPE;
                                }
                                else
                                {
                                    nodeNameRelationship = SUBSUMES;
                                }
                            }
                            else if (on2 != null && on2 is IntUniversalSet)
                            {
                                nodeNameRelationship = SUBSUMED_BY;
                            }
                            else if (!(on1 != null && on2 != null))
                            {
                                nodeNameRelationship = t1.Equals(t2) ? SAME_TYPE : OVERLAPS;
                            }
                            else
                            {
                                IntSet n1 = on1;
                                IntSet n2 = on2;
                                if (n1.ContainsAll(n2))
                                {
                                    if (n1.Count == n2.Count)
                                    {
                                        nodeNameRelationship = SAME_TYPE;
                                    }
                                    else
                                    {
                                        nodeNameRelationship = SUBSUMES;
                                    }
                                }
                                else if (n2.ContainsAll(n1))
                                {
                                    nodeNameRelationship = SUBSUMED_BY;
                                }
                                else if (IntHashSet.ContainsSome(n1, n2))
                                {
                                    nodeNameRelationship = OVERLAPS;
                                }
                                else
                                {
                                    nodeNameRelationship = DISJOINT;
                                }
                            }


                            // now find the relationship between the content types allowed
                            Affinity contentRelationship = ComputeContentRelationship(t1, t2, on1, on2);

                            // now analyse the three different relationships
                            if (nodeKindRelationship == SAME_TYPE && nodeNameRelationship == SAME_TYPE && contentRelationship == SAME_TYPE)
                            {
                                return SAME_TYPE;
                            }
                            else if ((nodeKindRelationship == SAME_TYPE || nodeKindRelationship == SUBSUMES) && (nodeNameRelationship == SAME_TYPE || nodeNameRelationship == SUBSUMES) && (contentRelationship == SAME_TYPE || contentRelationship == SUBSUMES))
                            {
                                return SUBSUMES;
                            }
                            else if ((nodeKindRelationship == SAME_TYPE || nodeKindRelationship == SUBSUMED_BY) && (nodeNameRelationship == SAME_TYPE || nodeNameRelationship == SUBSUMED_BY) && (contentRelationship == SAME_TYPE || contentRelationship == SUBSUMED_BY))
                            {
                                return SUBSUMED_BY;
                            }
                            else if (nodeNameRelationship == DISJOINT || contentRelationship == DISJOINT)
                            {
                                return DISJOINT;
                            }
                            else
                            {
                                return OVERLAPS;
                            }
                        }
                    }
                }
                else if (t1 is AnyExternalObjectType)
                {
                    if (!(t2 is AnyExternalObjectType))
                    {
                        return DISJOINT;
                    }

                    if (t1 is JavaExternalObjectType)
                    {
                        if (t2 == AnyExternalObjectType.THE_INSTANCE)
                        {
                            return SUBSUMED_BY;
                        }
                        else if (t2 is JavaExternalObjectType)
                        {
                            return ((JavaExternalObjectType)t1).GetRelationship((JavaExternalObjectType)t2);
                        }
                        else
                        {
                            return DISJOINT;
                        }
                    }

                    if (t2 is JavaExternalObjectType)
                    {
                        return SUBSUMES;
                    }
                    else
                    {
                        return DISJOINT;
                    }
                }
                else
                {

                    // t1 is a IFunctionItemType
                    if (t1 is MapType && t2 is MapType)
                    {
                        if (t1 == MapType.EMPTY_MAP_TYPE)
                        {
                            return SUBSUMED_BY;
                        }
                        else if (t2 == MapType.EMPTY_MAP_TYPE)
                        {
                            return SUBSUMES;
                        }

                        if (t1 == MapType.ANY_MAP_TYPE)
                        {
                            return SUBSUMES;
                        }
                        else if (t2 == MapType.ANY_MAP_TYPE)
                        {
                            return SUBSUMED_BY;
                        }

                        IPlainType k1 = ((MapType)t1).KeyType;
                        IPlainType k2 = ((MapType)t2).KeyType;
                        Values.SequenceType v1 = ((MapType)t1).ValueType;
                        Values.SequenceType v2 = ((MapType)t2).ValueType;
                        Affinity keyRel = Relationship(k1, k2);
                        Affinity valueRel = SequenceTypeRelationship(v1, v2);
                        Affinity rel = CombineRelationships(keyRel, valueRel);
                        if (rel == SAME_TYPE || rel == SUBSUMES || rel == SUBSUMED_BY)
                        {
                            return rel;
                        } // For other relationships, it's more complex because of the need to compare as function type,
                        // so just fall through
                    }

                    if (t2 is IFunctionItemType)
                    {
                        Affinity signatureRelationship = ((IFunctionItemType)t1).Relationship((IFunctionItemType)t2, this);
                        if (signatureRelationship == DISJOINT)
                        {
                            return DISJOINT;
                        }
                        else
                        {
                            Affinity assertionRelationship = SAME_TYPE;
                            AnnotationList first = ((IFunctionItemType)t1).AnnotationAssertions;
                            AnnotationList second = ((IFunctionItemType)t2).AnnotationAssertions;
                            HashSet<NamespaceUri> namespaces = new HashSet<NamespaceUri>();
                            foreach (Annotation a in first)
                            {
                                namespaces.Add(a.AnnotationQName.GetNamespaceUri());
                            }

                            foreach (Annotation a in second)
                            {
                                namespaces.Add(a.AnnotationQName.GetNamespaceUri());
                            }

                            foreach (NamespaceUri ns in namespaces)
                            {
                                IFunctionAnnotationHandler handler = config.GetFunctionAnnotationHandler(ns);
                                if (handler != null)
                                {
                                    Affinity localRel = SAME_TYPE;
                                    AnnotationList firstFiltered = first.FilterByNamespace(ns);
                                    AnnotationList secondFiltered = second.FilterByNamespace(ns);
                                    if (firstFiltered.IsEmpty())
                                    {
                                        if (secondFiltered.IsEmpty())
                                        {
                                        }
                                        else
                                        {
                                            localRel = SUBSUMES;
                                        }
                                    }
                                    else
                                    {
                                        if (secondFiltered.IsEmpty())
                                        {
                                            localRel = SUBSUMED_BY;
                                        }
                                        else
                                        {
                                            localRel = handler.Relationship(firstFiltered, secondFiltered);
                                        }
                                    }

                                    assertionRelationship = CombineRelationships(assertionRelationship, localRel);
                                }
                            }

                            return CombineRelationships(signatureRelationship, assertionRelationship);
                        }
                    }
                    else
                    {
                        return DISJOINT;
                    }
                }
            }
            catch (MissingComponentException)
            {
                return OVERLAPS;
            }
        }

        private static void RequireTrueItemType(ItemType t)
        {
            if (t == null)
                throw new NullReferenceException();
            if (t is IUnionType && !t.IsPlainType())
            {
                throw new InvalidOperationException(t + " is a non-pure union type");
            }
        }

        private static Affinity NameTestRelationship(IQNameTest t1, IQNameTest t2)
        {
            if (t1.Equals(t2))
            {
                return SAME_TYPE;
            }

            if (t2 is NameTest)
            {
                return t1.Matches(((NameTest)t2).MatchingNodeName) ? SUBSUMES : DISJOINT;
            }

            if (t1 is NameTest)
            {
                return t2.Matches(((NameTest)t1).MatchingNodeName) ? SUBSUMED_BY : DISJOINT;
            }

            if (t2 is SameNameTest)
            {
                return t1.Matches(((SameNameTest)t2).MatchingNodeName) ? SUBSUMES : DISJOINT;
            }

            if (t1 is SameNameTest)
            {
                return t2.Matches(((SameNameTest)t1).MatchingNodeName) ? SUBSUMED_BY : DISJOINT;
            }

            if (t1 is NamespaceTest && t2 is NamespaceTest)
            {
                return DISJOINT;
            }

            if (t1 is LocalNameTest && t2 is LocalNameTest)
            {
                return DISJOINT;
            }

            return OVERLAPS;
        }

        private static Affinity CombineRelationships(Affinity rel1, Affinity rel2)
        {
            if (rel1 == SAME_TYPE && rel2 == SAME_TYPE)
            {
                return SAME_TYPE;
            }
            else if ((rel1 == SAME_TYPE || rel1 == SUBSUMES) && (rel2 == SAME_TYPE || rel2 == SUBSUMES))
            {
                return SUBSUMES;
            }
            else if ((rel1 == SAME_TYPE || rel1 == SUBSUMED_BY) && (rel2 == SAME_TYPE || rel2 == SUBSUMED_BY))
            {
                return SUBSUMED_BY;
            }
            else if (rel1 == DISJOINT || rel2 == DISJOINT)
            {
                return DISJOINT;
            }
            else
            {
                return OVERLAPS;
            }
        }

        private static HashSet<X> ToSet<X>(IEnumerable<X> @in)
        {
            HashSet<X> s = new HashSet<X>();
            foreach (X x in @in)
            {
                s.Add(x);
            }

            return s;
        }

        private bool UnionSubsumes(HashSet<IPlainType> s1, HashSet<IPlainType> s2)
        {

            // s1 subsumes s2 if every t2 in s2 is subsumed by some t1 in s1 (we'll discount the possibility
            // of some t2 in s2 being subsumed by a combination of multiple types in s1)
            foreach (IPlainType t2 in s2)
            {
                bool t2isSubsumed = false;
                foreach (IPlainType t1 in s1)
                {
                    Affinity rel = Relationship(t1, t2);
                    if (rel == SUBSUMES || rel == SAME_TYPE)
                    {
                        t2isSubsumed = true;
                        break;
                    }
                }

                if (!t2isSubsumed)
                {
                    return false;
                }
            }

            return true;
        }

        private bool UnionOverlaps(HashSet<IPlainType> s1, HashSet<IPlainType> s2)
        {
            foreach (IPlainType t2 in s2)
            {
                foreach (IPlainType t1 in s1)
                {
                    Affinity rel = Relationship(t1, t2);
                    if (rel != DISJOINT)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected virtual Affinity ComputeContentRelationship(ItemType t1, ItemType t2, IntSet n1, IntSet n2)
        {
            Affinity contentRelationship;
            if (t1 is DocumentNodeTest)
            {
                if (t2 is DocumentNodeTest)
                {
                    contentRelationship = Relationship(((DocumentNodeTest)t1).ElementTest, ((DocumentNodeTest)t2).ElementTest);
                }
                else
                {
                    contentRelationship = SUBSUMED_BY;
                }
            }
            else if (t2 is DocumentNodeTest)
            {
                contentRelationship = SUBSUMES;
            }
            else
            {
                ISchemaType s1 = ((NodeTest)t1).ContentType;
                ISchemaType s2 = ((NodeTest)t2).ContentType;
                contentRelationship = SchemaTypeRelationship(s1, s2);
            }

            bool nillable1 = ((NodeTest)t1).IsNillable();
            bool nillable2 = ((NodeTest)t2).IsNillable();

            // Adjust the results to take nillability into account
            // Note: although nodes cannot be nilled in a non-schema-aware environment,
            // nillability still affects the relationships between types, for example
            // element(e) and element(e, xs:anyType): see xslt3 test higher-order-functions-034.
            if (nillable1 != nillable2)
            {
                switch (contentRelationship)
                {
                    case SUBSUMES:
                        if (nillable2)
                        {
                            contentRelationship = OVERLAPS;
                        }

                        break;
                    case SUBSUMED_BY:
                        if (nillable1)
                        {
                            contentRelationship = OVERLAPS;
                        }

                        break;
                    case SAME_TYPE:
                        if (nillable1)
                        {
                            contentRelationship = SUBSUMES;
                        }
                        else
                        {
                            contentRelationship = SUBSUMED_BY;
                        }

                        break;
                    default:
                        break;
                }
            }

            return contentRelationship;
        }

        public virtual Affinity SequenceTypeRelationship(Values.SequenceType s1, Values.SequenceType s2)
        {
            int c1 = s1.GetCardinality();
            int c2 = s2.GetCardinality();
            Affinity cardRel;
            if (c1 == c2)
            {
                cardRel = SAME_TYPE;
            }
            else if (Cardinality.Subsumes(c1, c2))
            {
                cardRel = SUBSUMES;
            }
            else if (Cardinality.Subsumes(c2, c1))
            {
                cardRel = SUBSUMED_BY;
            }
            else if (c1 == StaticProperty.EMPTY && !Cardinality.AllowsZero(c2))
            {
                return DISJOINT;
            }
            else if (c2 == StaticProperty.EMPTY && !Cardinality.AllowsZero(c1))
            {
                return DISJOINT;
            }
            else
            {
                cardRel = OVERLAPS;
            }

            Affinity itemRel = Relationship(s1.PrimaryType, s2.PrimaryType);
            if (itemRel == DISJOINT)
            {
                return DISJOINT;
            }

            if (cardRel == SAME_TYPE || cardRel == itemRel)
            {
                return itemRel;
            }

            if (itemRel == SAME_TYPE)
            {
                return cardRel;
            }

            return OVERLAPS;
        }

        public virtual Affinity SchemaTypeRelationship(ISchemaType s1, ISchemaType s2)
        {
            if (s1.IsSameType(s2))
            {
                return SAME_TYPE;
            }

            if (s1 is AnyType)
            {
                return SUBSUMES;
            }

            if (s2 is AnyType)
            {
                return SUBSUMED_BY;
            }

            if (s1 is Untyped && (s2 == BuiltInAtomicType.ANY_ATOMIC || s2 == BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                return OVERLAPS;
            }

            if (s2 is Untyped && (s1 == BuiltInAtomicType.ANY_ATOMIC || s1 == BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                return OVERLAPS;
            }

            if (s1 is IPlainType && ((IPlainType)s1).IsPlainType() && s2 is IPlainType && ((IPlainType)s2).IsPlainType())
            {
                return Relationship((ItemType)s1, (ItemType)s2); // See bug 4007. Technically, this isn't quite right. If U is union(X,Y), and V is union(X,Y,Z),
                // then itemType-subtype(U, V) is true (XPath31 2.5.6.2 rule 2), but derives-from(U, V) is false.
                // We're computing the derives-from relationship here (for example, to assess whether element(*, U)
                // is substitutable for element(*, V) in a function signature), and by delegating to test the
                // item type relationship, we are returning true for this case when it should be false.
                // It's not clear whether this difference in the spec is intentional, and it doesn't cause
                // any test cases to fail, so I decided to leave it.  I don't think it causes any problems
                // with type safety, because elements and attributes validated against union(X, Y) will have
                // a type annotation of either X or Y, which means they will be accepted as instances of
                // element(*, union(X,Y,Z)): that @is, the instances of element(*, union(X,Y)) are indeed
                // a subset of the instances of element(*, union(X,Y,Z)).               MHK 2018-11-08.
            }

            ISchemaType t1 = s1;
            while ((t1 = t1.BaseType) != null)
            {
                if (t1.IsSameType(s2))
                {
                    return SUBSUMED_BY;
                }
            }

            ISchemaType t2 = s2;
            while ((t2 = t2.BaseType) != null)
            {
                if (t2.IsSameType(s1))
                {
                    return SUBSUMES;
                }
            }

            return DISJOINT;
        }

        public static Affinity InverseRelationship(Affinity relation)
        {
            switch (relation)
            {
                case SAME_TYPE:
                    return SAME_TYPE;
                case SUBSUMES:
                    return SUBSUMED_BY;
                case SUBSUMED_BY:
                    return SUBSUMES;
                case OVERLAPS:
                    return OVERLAPS;
                case DISJOINT:
                    return DISJOINT;
                default:
                    throw new ArgumentException();
            }
        }

        private sealed class ItemTypePair
        {
            readonly ItemType s;
            readonly ItemType t;
            public ItemTypePair(ItemType s, ItemType t)
            {
                this.s = s;
                this.t = t;
            }

            public override int GetHashCode()
            {
                return s.GetHashCode() ^ t.GetHashCode();
            }

            /// <summary>
            /// Indicates whether some other object is "equal to" this one.
            /// </summary>
            public override bool Equals(object obj)
            {
                if (obj is ItemTypePair)
                {
                    ItemTypePair pair = (ItemTypePair)obj;
                    return s.Equals(pair.s) && t.Equals(pair.t);
                }
                else
                {
                    return false;
                }
            }
        }
    }
}

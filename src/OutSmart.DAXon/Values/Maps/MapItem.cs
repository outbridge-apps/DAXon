////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// Interface supported by different implementations of an XDM map item
    /// </summary>
    public abstract class MapItem : IFunctionItem
    {
        // PHASE7_INDEXER_MAPITEM
        public IGroundedValue this[AtomicValue key] { get { return Get(key); } }
        public int Count { get { return Size(); } }
        public abstract UType KeyUType { get; }

        public virtual OperandRole[] OperandRoles => new OperandRole[]
            {
                OperandRole.SINGLE_ATOMIC
            };

        public virtual IFunctionItemType FunctionItemType => MapType.ANY_MAP_TYPE;

        public virtual string Description => ToShortString();

        public virtual UnicodeString UnicodeStringValue
        {
            get
            {
                throw new UncheckedXPathException(new XPathException("A map has no string value", "FOTY0014"));
            }
        }

        public virtual ISequenceIterator TypedValue
        {
            get
            {
                throw new XPathException("A map has no typed value");
            }
        }
        public abstract IGroundedValue Get(AtomicValue key);
        public abstract int Size();
        public virtual bool IsEmpty()
        {
            return Size() == 0;
        }

        public abstract IAtomicIterator Keys();
        public abstract IEnumerable<KeyValuePair> KeyValuePairs();
        public virtual ISequenceIterator Entries()
        {
            return (ISequenceIterator)(new SequenceIteratorOverJavaIterator<KeyValuePair>(KeyValuePairs().GetEnumerator(), (entry) => new SingleEntryMap(entry.key, entry.value)));
        }

        public abstract MapItem AddEntry(AtomicValue key, IGroundedValue value);
        public abstract MapItem Remove(AtomicValue key);
        public abstract bool Conforms(IPlainType keyType, SequenceType valueType, TypeHierarchy th);
        public abstract ItemType GetItemType(TypeHierarchy th);
        public virtual string ToShortString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("map{");
            int count = Size();
            if (count == 0)
            {
                sb.Append('}');
            }
            else if (count <= 5)
            {
                int pos = 0;
                foreach (KeyValuePair pair in KeyValuePairs())
                {
                    if (pos++ > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(Err.Depict(pair.key)).Append(':').Append(Err.DepictSequence(pair.value));
                }

                sb.Append('}');
            }
            else
            {
                sb.Append("(:size ").Append(count).Append(":)}");
            }

            return sb.ToString();
        }

        public virtual Genre GetGenre()
        {
            return Genre.MAP;
        }

        public virtual bool IsArray()
        {
            return false;
        }

        public virtual bool IsMap()
        {
            return true;
        }

        public virtual AnnotationList GetAnnotations()
        {
            return AnnotationList.EMPTY;
        }

        public virtual IAtomicSequence Atomize()
        {
            throw new XPathException("Cannot atomize a map (" + ToShortString() + ")", "FOTY0013");
        }

        public static bool IsKnownToConform(ISequence value, ItemType itemType)
        {

            // Problem is we don't have access to a TypeHierarchy object...
            if (itemType == AnyItemType.GetInstance())
            {
                return true;
            }

            try
            {
                // Map values are overwhelmingly single items; skip the iterator ceremony for them.
                if (value is IItem one)
                {
                    if (one is AtomicValue oneAtomic)
                    {
                        return itemType is IAtomicType oneTarget && Types.Type.IsSubType(oneAtomic.GetItemType(), oneTarget);
                    }

                    if (one is NodeInfo oneNode)
                    {
                        return itemType is NodeTest oneTest && oneTest.Test(oneNode);
                    }

                    return false;
                }

                ISequenceIterator iter = value.Iterate();
                for (IItem item; (item = iter.Next()) != null;)
                {
                    if (item is AtomicValue)
                    {
                        if (itemType is IAtomicType)
                        {
                            if (!Types.Type.IsSubType(((AtomicValue)item).GetItemType(), (IAtomicType)itemType))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (item is NodeInfo)
                    {
                        if (itemType is NodeTest)
                        {
                            if (!((NodeTest)itemType).Test((NodeInfo)item))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {

                        // functions, maps, arrays: give up (this is only an optimization)
                        return false;
                    }
                }

                return true;
            }
            catch (UncheckedXPathException e)
            {
                return false;
            }
        }

        public static ItemType GetItemTypeOfSequence(ISequence val)
        {
            try
            {
                IItem first = val.Head();
                if (first == null)
                {
                    return AnyItemType.GetInstance();
                }
                else
                {
                    ItemType type;
                    if (first is AtomicValue)
                    {
                        type = ((AtomicValue)first).GetItemType();
                    }
                    else if (first is NodeInfo)
                    {
                        type = NodeKindTest.MakeNodeKindTest(((NodeInfo)first).GetNodeKind());
                    }
                    else
                    {
                        type = AnyFunctionType.GetInstance();
                    }

                    if (IsKnownToConform(val, type))
                    {
                        return type;
                    }
                    else
                    {
                        return AnyItemType.GetInstance();
                    }
                }
            }
            catch (XPathException e)
            {
                return AnyItemType.GetInstance();
            }
        }

        public virtual StructuredQName GetFunctionName()
        {
            return null;
        }

        public virtual int GetArity()
        {
            return 1;
        }

        public virtual IXPathContext MakeNewContext(IXPathContext callingContext, IContextOriginator originator)
        {
            return callingContext;
        }

        public virtual ISequence Call(IXPathContext context, ISequence[] args)
        {
            AtomicValue key = (AtomicValue)args[0].Head();
            ISequence value = Get(key);
            if (value == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                return value;
            }
        }

        public virtual bool DeepEquals(IFunctionItem other, IXPathContext context, IAtomicComparer comparer, int flags)
        {
            if (other is MapItem && ((MapItem)other).Count == Size())
            {
                IAtomicIterator keyIter = Keys();
                AtomicValue key;
                int steps = 0;
                while ((key = keyIter.Next()) != null)
                {
                    if ((++steps & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    ISequence thisValue = Get(key);
                    ISequence otherValue = ((MapItem)other)[key];
                    if (otherValue == null)
                    {
                        return false;
                    }

                    if (!DAXonDeepEqual.DeepEqual(otherValue.Iterate(), thisValue.Iterate(), comparer, context, flags))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public virtual bool DeepEqual40(IFunctionItem other, IXPathContext context, DeepEqual.DeepEqualOptions options)
        {
            if (other is MapItem && ((MapItem)other).Count == Size())
            {
                IAtomicIterator keyIter = Keys();
                AtomicValue key;
                int steps = 0;
                while ((key = keyIter.Next()) != null)
                {
                    if ((++steps & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    ISequence thisValue = Get(key);
                    ISequence otherValue = ((MapItem)other)[key];
                    if (otherValue == null)
                    {
                        return false;
                    }

                    if (!DeepEqual.DeepEqualFn(otherValue.Iterate(), thisValue.Iterate(), context, options))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public virtual MapItem ItemAt(int n)
        {
            return n == 0 ? this : null;
        }

        public virtual bool EffectiveBooleanValue()
        {
            throw new XPathException("A map item has no effective boolean value");
        }

        public static string MapToString(MapItem map)
        {
            // Host-facing full dump (nothing in the engine calls it): unlike ToShortString it
            // keeps every entry, but the DEPTH must still be bounded - the nesting is the
            // value's, and a debugger evaluating ToString on attacker JSON must not die.
            Err.EnterDepiction();
            try
            {
                if (Err.DepictionTooDeep)
                {
                    return "map{...}";
                }

                StringBuilder buffer = new StringBuilder(256);
                buffer.Append("map{");
                foreach (KeyValuePair pair in map.KeyValuePairs())
                {
                    if (buffer.Length > 4)
                    {
                        buffer.Append(',');
                    }

                    buffer.Append(pair.key.ToString());
                    buffer.Append(':');
                    buffer.Append(pair.value.ToString());
                }

                buffer.Append('}');
                return buffer.ToString();
            }
            finally
            {
                Err.LeaveDepiction();
            }
        }

        /// <summary>
        /// Export information about this function item to the export() or explain() output
        /// </summary>
        public virtual void Export(ExpressionPresenter @out)
        {
            @out.StartElement("map");
            @out.EmitAttribute("size", "" + Size());
            foreach (KeyValuePair kvp in KeyValuePairs())
            {
                Literal.ExportAtomicValue(kvp.key, @out);
                Literal.ExportValue(kvp.value, @out);
            }

            @out.EndElement();
        }

        /// <summary>
        /// Export information about this function item to the export() or explain() output
        /// </summary>
        public virtual bool IsTrustedResultType()
        {
            return true;
        }
        IItem IGroundedValue.ItemAt(int arg0) => ItemAt(arg0);
        public virtual ISequenceIterator Iterate() => new SingletonIterator(this);
        public virtual IItem Head() => this;
        public virtual IGroundedValue Subsequence(int arg0, int arg1) => (arg0 <= 0 && (long)arg0 + arg1 > 0) ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance(); // singleton item (upstream GroundedValue default)
        public virtual int GetLength() => 1;
        public virtual string GetStringValue() => throw new UncheckedXPathException(new XPathException("The string value of a map is not defined", "FOTY0014"));
        IItem IItem.ItemAt(int arg0) => ItemAt(arg0);
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual bool IsSequenceVariadic() => false; // upstream FunctionItem default
        public virtual IGroundedValue Reduce() => this;
        public virtual bool IsStreamed() => false; // upstream NodeInfo/Item default
        public virtual IGroundedValue Materialize() => this;
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}


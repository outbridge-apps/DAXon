////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Arrays
{
    /// <summary>
    /// An abstract implementation of XDM array items, containing methods that can be implemented generically.
    /// </summary>
    internal abstract class AbstractArrayItem : ArrayItem
    {
        private SequenceType memberType = null; // computed on demand
        public override OperandRole[] OperandRoles => new OperandRole[]
            {
                OperandRole.SINGLE_ATOMIC
            };

        public override IFunctionItemType FunctionItemType => ArrayItemType.ANY_ARRAY_TYPE;

        public override string Description => "array";

        public override UnicodeString UnicodeStringValue
        {
            get
            {
                throw new UncheckedXPathException(new XPathException("An array has no string value", "FOTY0014"));
            }
        }

        public override IAtomicSequence Atomize()
        {
            // Nested arrays recurse back through here, one level per member. The depth is the
            // input's, not the stylesheet's: ~8KB of attacker JSON atomized by a fixed sheet.
            StackGuard.Probe();
            IList<AtomicValue> list = new List<AtomicValue>(ArrayLength());
            foreach (IGroundedValue seq in Members())
            {
                SequenceTool.Supply(seq.Iterate(), (item) =>
                {
                    IAtomicSequence atoms = item.Atomize();
                    foreach (AtomicValue atom in atoms)
                    {
                        list.Add(atom);
                    }
                });
            }

            return new AtomicArray(list);
        }

        public override AnnotationList GetAnnotations()
        {
            return AnnotationList.EMPTY;
        }

        public override StructuredQName GetFunctionName()
        {
            return null;
        }

        public override int GetArity()
        {
            return 1;
        }

        public override IXPathContext MakeNewContext(IXPathContext callingContext, IContextOriginator originator)
        {
            return callingContext;
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            IntegerValue subscript = (IntegerValue)args[0].Head();
            return Get(ArrayFunctionSet.CheckSubscript(subscript, ArrayLength()) - 1);
        }

        public override bool DeepEquals(IFunctionItem other, IXPathContext context, IAtomicComparer comparer, int flags)
        {
            if (other is ArrayItem)
            {
                ArrayItem that = (ArrayItem)other;
                if (this.ArrayLength() != that.ArrayLength())
                {
                    return false;
                }

                for (int i = 0; i < this.ArrayLength(); i++)
                {
                    if ((i & 1023) == 1023)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    if (!DAXonDeepEqual.DeepEqual(this[i].Iterate(), that[i].Iterate(), comparer, context, flags))
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool DeepEqual40(IFunctionItem other, IXPathContext context, DeepEqual.DeepEqualOptions options)
        {
            if (other is ArrayItem)
            {
                ArrayItem that = (ArrayItem)other;
                if (this.ArrayLength() != that.ArrayLength())
                {
                    return false;
                }

                for (int i = 0; i < this.ArrayLength(); i++)
                {
                    if ((i & 1023) == 1023)
                    {
                        Core.Controller.CheckActiveTimeout();
                    }

                    if (!DeepEqual.DeepEqualFn(this[i].Iterate(), that[i].Iterate(), context, options))
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool EffectiveBooleanValue()
        {
            throw new XPathException("Effective boolean value is not defined for arrays", "FORG0006");
        }

        /// <summary>
        /// Output information about this function item to the diagnostic explain() output
        /// </summary>
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("array");
            @out.EmitAttribute("size", ArrayLength() + "");
            foreach (IGroundedValue mem in Members())
            {
                Literal.ExportValue(mem, @out);
            }

            @out.EndElement();
        }

        /// <summary>
        /// Output information about this function item to the diagnostic explain() output
        /// </summary>
        public override bool IsTrustedResultType()
        {
            return false;
        }

        /// <summary>
        /// Output a string representation of the array, suitable for diagnostics
        /// </summary>
        public override string ToString()
        {
            // Host-facing full dump (nothing in the engine calls it): unlike ToShortString it
            // keeps every member, but the DEPTH must still be bounded - the nesting is the
            // value's, and a debugger evaluating ToString on attacker JSON must not die.
            Err.EnterDepiction();
            try
            {
                if (Err.DepictionTooDeep)
                {
                    return "[...]";
                }

                StringBuilder buffer = new StringBuilder(256);
                buffer.Append('[');
                foreach (IGroundedValue seq in Members())
                {
                    if (buffer.Length > 1)
                    {
                        buffer.Append(", ");
                    }

                    buffer.Append(seq.ToString());
                }

                buffer.Append(']');
                return buffer.ToString();
            }
            finally
            {
                Err.LeaveDepiction();
            }
        }

        /// <summary>
        /// Output a string representation of the array, suitable for diagnostics
        /// </summary>
        public override SequenceType GetMemberType(TypeHierarchy th)
        {

            //try {
            if (memberType == null)
            {
                if (IsEmpty())
                {
                    memberType = SequenceType.MakeSequenceType(ErrorType.GetInstance(), StaticProperty.EXACTLY_ONE);
                }
                else
                {
                    ItemType contentType = null;
                    int contentCard = StaticProperty.EXACTLY_ONE;
                    foreach (IGroundedValue s in Members())
                    {
                        if (contentType == null)
                        {
                            contentType = SequenceTool.GetItemType(s, th);
                            contentCard = SequenceTool.GetCardinality(s);
                        }
                        else
                        {
                            contentType = Types.Type.GetCommonSuperType(contentType, SequenceTool.GetItemType(s, th));
                            contentCard = Cardinality.Union(contentCard, SequenceTool.GetCardinality(s));
                        }
                    }

                    memberType = SequenceType.MakeSequenceType(contentType, contentCard);
                }
            }

            return memberType; //        } catch (XPathException e) {
        }
    }
}

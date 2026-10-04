////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
using OutSmart.DAXon.Serialization;
namespace OutSmart.DAXon.Expressions
{
    public class Literal : Expression
    {
        private readonly IGroundedValue value;

        public virtual IGroundedValue GroundedValue => value;

        public override int NetCost => 0;

        public override IntegerValue[] IntegerBounds
        {
            get
            {
                if (value is IntegerValue)
                {
                    return new IntegerValue[]
                    {
                    (IntegerValue)value,
                    (IntegerValue)value
                    };
                }
                else if (value is IntegerRange range)
                {
                    // lowest first: a range that counts down starts at its highest
                    return new IntegerValue[]
                    {
                    Int64Value.MakeIntegerValue(Math.Min(range.Start, range.End)),
                    Int64Value.MakeIntegerValue(Math.Max(range.Start, range.End))
                    };
                }
                else
                {
                    return null;
                }
            }
        }

        public override int Dependencies => 0;

        public override int ImplementationMethod => ITERATE_METHOD | PROCESS_METHOD | EVALUATE_METHOD;

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override string ExpressionName => "literal";

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override string StreamerName => "Literal";
        public Literal(IGroundedValue value)
        {
            this.value = value.Reduce();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            return this;
        }

        public override ItemType GetItemType()
        {

            // Avoid getting the configuration if we can: it's a common source of NPE's
            if (value is AtomicValue)
            {
                return ((AtomicValue)value).GetItemType();
            }
            else if (value.GetLength() == 0)
            {
                return ErrorType.GetInstance();
            }
            else
            {
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                return SequenceTool.GetItemType(value, th);
            }
        }

        public virtual bool IsInstance(SequenceType req, TypeHierarchy th)
        {
            int requiredCardinality = req.GetCardinality();
            int count = value.GetLength();
            if (!Cardinality.Allows(requiredCardinality, count))
            {
                return false;
            }

            ItemType requiredType = req.PrimaryType;
            if (value is IntegerRange range)
            {
                // Every item of a range is an Int64Value with the same type annotation, so one
                // endpoint decides the match for all of them — checking item by item walked the
                // whole range (potentially int.MaxValue items) during type-checking.
                return requiredType.Matches(Int64Value.MakeIntegerValue(range.Start), th)
                    && requiredType.Matches(Int64Value.MakeIntegerValue(range.End), th);
            }

            foreach (IItem item in value.AsIterable())
            {
                if (!requiredType.Matches(item, th))
                {
                    return false;
                }
            }

            return true;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            if (value.GetLength() == 0)
            {
                return UType.VOID;
            }
            else if (value is AtomicValue)
            {
                return ((AtomicValue)value).GetUType();
            }
            else if (value is IFunctionItem)
            {
                return UType.FUNCTION;
            }
            else
            {
                return base.GetStaticUType(contextItemType);
            }
        }

        protected override int ComputeCardinality()
        {
            if (value.GetLength() == 0)
            {
                return StaticProperty.EMPTY;
            }
            else if (value is AtomicValue)
            {
                return StaticProperty.EXACTLY_ONE;
            }

            ISequenceIterator iter = value.Iterate();
            IItem next = iter.Next();
            if (next == null)
            {
                return StaticProperty.EMPTY;
            }
            else
            {
                if (iter.Next() != null)
                {
                    return StaticProperty.ALLOWS_MANY;
                }
                else
                {
                    return StaticProperty.EXACTLY_ONE;
                }
            }
        }

        protected override int ComputeSpecialProperties()
        {
            if (value.GetLength() == 0)
            {

                // An empty sequence has all special properties except "has side effects".
                return StaticProperty.SPECIAL_PROPERTY_MASK & ~StaticProperty.HAS_SIDE_EFFECTS;
            }

            return StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        public override bool SupportsLazyEvaluation()
        {
            return false;
        }

        public override bool IsVacuousExpression()
        {
            return value.GetLength() == 0;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Literal l2 = new Literal(value);
            ExpressionTool.CopyLocationInfo(this, l2);
            return l2;
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            if (IsEmptySequence(this))
            {
                return new NodeTestPattern(ErrorType.GetInstance());
            }
            else
            {
                return base.ToPattern(config);
            }
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            return pathMapNodeSet;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return value.Iterate();
        }

        public virtual ISequenceIterator Iterate()
        {
            return value.Iterate();
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return value.Head();
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            if (value is IItem)
            {
                output.Append((IItem)value, GetLocation(), ReceiverOption.ALL_NAMESPACES);
            }
            else
            {
                SequenceTool.Supply(value.Iterate(), (it) => output.Append(it, GetLocation(), ReceiverOption.ALL_NAMESPACES));
            }
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override UnicodeString EvaluateAsString(IXPathContext context)
        {
            AtomicValue value = (AtomicValue)EvaluateItem(context);
            if (value == null)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return value.UnicodeStringValue;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return value.EffectiveBooleanValue();
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override bool Equals(object obj)
        {
            if (!(obj is Literal))
            {
                return false;
            }

            IGroundedValue v0 = value;
            IGroundedValue v1 = ((Literal)obj).value;
            ISequenceIterator i0 = v0.Iterate();
            ISequenceIterator i1 = v1.Iterate();
            while (true)
            {
                IItem m0 = i0.Next();
                IItem m1 = i1.Next();
                if (m0 == null && m1 == null)
                {
                    return true;
                }

                if (m0 == null || m1 == null)
                {
                    return false;
                }

                if (m0 == m1)
                {
                    continue;
                }

                bool n0 = m0 is NodeInfo;
                bool n1 = m1 is NodeInfo;
                if (n0 != n1)
                {
                    return false;
                }

                if (n0)
                {
                    if (m0.Equals(m1))
                    {
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }

                bool a0 = m0 is AtomicValue;
                bool a1 = m1 is AtomicValue;
                if (a0 != a1)
                {
                    return false;
                }

                if (a0)
                {
                    if (((AtomicValue)m0).IsIdentical((AtomicValue)m1) && ((AtomicValue)m0).GetItemType() == ((AtomicValue)m1).GetItemType())
                    {
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }


                // don't attempt to compare functions, maps, and arrays
                return false;
            }
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        protected override int ComputeHashCode()
        {
            if (value is IAtomicSequence)
            {
                return SimpleTypeComparison.GetInstance().Hash((IAtomicSequence)value); // TODO: why this comparator - what are we using this hash code for?
            }
            else
            {
                return base.ComputeHashCode();
            }
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override string ToString()
        {
            return value.ToString();
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override void Export(ExpressionPresenter @out)
        {
            ExportValue(value, @out);
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static void ExportValue(ISequence value, ExpressionPresenter @out)
        {
            if (value.Head() == null)
            {
                @out.StartElement("empty");
                @out.EndElement();
            }
            else if (value is AtomicValue)
            {
                ExportAtomicValue((AtomicValue)value, @out);
            }
            else if (value is IntegerRange)
            {
                @out.StartElement("range");
                @out.EmitAttribute("from", "" + ((IntegerRange)value).Start);
                @out.EmitAttribute("to", "" + ((IntegerRange)value).End);
                @out.EndElement();
            }
            else if (value is NodeInfo)
            {
                @out.StartElement("node");
                int nodeKind = ((NodeInfo)value).GetNodeKind();
                @out.EmitAttribute("kind", nodeKind + "");
                if (@out.GetOptions().explaining)
                {
                    string name = ((NodeInfo)value).DisplayName;
                    if (!(name.Length == 0))
                    {
                        @out.EmitAttribute("name", name);
                    }
                }
                else
                {
                    switch (nodeKind)
                    {
                        case Types.Type.DOCUMENT:
                        case Types.Type.ELEMENT:
                            StringWriter sw = new StringWriter();
                            Properties props = new Properties();
                            props.SetProperty("method", "xml");
                            props.SetProperty("indent", "no");
                            props.SetProperty("omit-xml-declaration", "yes");
                            QueryResult.Serialize(((NodeInfo)value), new StreamResult((TextWriter)sw), props);
                            @out.EmitAttribute("content", sw.ToString());
                            @out.EmitAttribute("baseUri", ((NodeInfo)value).GetBaseURI());
                            break;
                        case Types.Type.TEXT:
                        case Types.Type.COMMENT:
                            @out.EmitAttribute("content", ((NodeInfo)value).GetStringValue());
                            break;
                        case Types.Type.ATTRIBUTE:
                        case Types.Type.NAMESPACE:
                        case Types.Type.PROCESSING_INSTRUCTION:
                            StructuredQName name = NameOfNode.MakeName(((NodeInfo)value)).GetStructuredQName();
                            if (!(name.GetLocalPart().Length == 0))
                            {
                                @out.EmitAttribute("localName", name.GetLocalPart());
                            }

                            if (!(name.GetPrefix().Length == 0))
                            {
                                @out.EmitAttribute("prefix", name.GetPrefix());
                            }

                            if (!name.HasURI(NamespaceUri.NULL))
                            {
                                @out.EmitAttribute("ns", name.GetNamespaceUri().ToString());
                            }

                            @out.EmitAttribute("content", ((NodeInfo)value).GetStringValue());
                            break;
                        default:
                            break;
                    }
                }

                @out.EndElement();
            }
            else if (value is MapItem)
            {
                @out.StartElement("map");
                @out.EmitAttribute("size", "" + ((MapItem)value).Count);
                foreach (OutSmart.DAXon.Values.Maps.KeyValuePair kvp in ((MapItem)value).KeyValuePairs())
                {
                    ExportAtomicValue(kvp.key, @out);
                    ExportValue(kvp.value, @out);
                }

                @out.EndElement();
            }
            else if (value is IFunctionItem)
            {
                ((IFunctionItem)value).Export(@out);
            }
            else if (value is IAnyExternalObject)
            {
                if (@out.GetOptions().explaining)
                {
                    @out.StartElement("externalObject");
                    @out.EmitAttribute("class", ((IAnyExternalObject)value).WrappedObject.GetType().FullName);
                    @out.EndElement();
                }
                else
                {
                    throw new XPathException("Cannot export a stylesheet containing literal values bound to external Java objects", DAXonErrorCode.SXST0070);
                }
            }
            else
            {
                @out.StartElement("literal");
                if (value is IGroundedValue)
                {
                    @out.EmitAttribute("count", ((IGroundedValue)value).GetLength() + "");
                }

                SequenceTool.Supply(value.Iterate(), (it) => ExportValue(it, @out));
                @out.EndElement();
            }
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static void ExportAtomicValue(AtomicValue value, ExpressionPresenter @out)
        {
            if (@out.GetOptions().target == "JS")
            {
                value.CheckValidInJavascript();
            }

            IAtomicType type = value.GetItemType();
            string val = value.GetStringValue();
            if (type.Equals(BuiltInAtomicType.STRING))
            {
                @out.StartElement("str");
                @out.EmitAttribute("val", val);
                @out.EndElement();
            }
            else if (type.Equals(BuiltInAtomicType.INTEGER))
            {
                @out.StartElement("int");
                @out.EmitAttribute("val", val);
                @out.EndElement();
            }
            else if (type.Equals(BuiltInAtomicType.DECIMAL))
            {
                @out.StartElement("dec");
                @out.EmitAttribute("val", val);
                @out.EndElement();
            }
            else if (type.Equals(BuiltInAtomicType.DOUBLE))
            {
                @out.StartElement("dbl");
                @out.EmitAttribute("val", val);
                @out.EndElement();
            }
            else if (type.Equals(BuiltInAtomicType.BOOLEAN))
            {
                @out.StartElement(((BooleanValue)value).EffectiveBooleanValue() ? "true" : "false");
                @out.EndElement();
            }
            else if (value is QualifiedNameValue)
            {
                @out.StartElement("qName");
                @out.EmitAttribute("pre", ((QualifiedNameValue)value).GetPrefix());
                @out.EmitAttribute("uri", ((QualifiedNameValue)value).GetNamespaceURI().ToString());
                @out.EmitAttribute("loc", ((QualifiedNameValue)value).LocalName);
                if (!type.Equals(BuiltInAtomicType.QNAME))
                {
                    @out.EmitAttribute("type", type.EQName);
                }

                @out.EndElement();
            }
            else
            {
                @out.StartElement("atomic");
                @out.EmitAttribute("val", val);
                @out.EmitAttribute("type", AlphaCode.FromItemType(type));
                @out.EndElement();
            }
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override string ToShortString()
        {
            if (value.GetLength() == 0)
            {
                return "()";
            }
            else if (value.GetLength() == 1)
            {
                return value.ToShortString();
            }
            else if (value.GetLength() == 2)
            {
                return "(" + value.Head().ToShortString() + ", " + value.ItemAt(1).ToShortString() + ")";
            }
            else
            {
                return "(" + value.Head().ToShortString() + ", " + value.ItemAt(1).ToShortString() + ", ...{" + value.GetLength() + "})";
            }
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static bool IsAtomic(Expression exp)
        {
            return exp is Literal && ((Literal)exp).GroundedValue is AtomicValue;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static bool IsEmptySequence(Expression exp)
        {
            return exp is Literal && ((Literal)exp).GroundedValue.GetLength() == 0;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static bool IsConstantBoolean(Expression exp, bool value)
        {
            if (exp is Literal)
            {
                IGroundedValue b = ((Literal)exp).GroundedValue;
                return b is BooleanValue && ((BooleanValue)b).GetBooleanValue() == value;
            }

            return false;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static bool HasEffectiveBooleanValue(Expression exp, bool value)
        {
            if (exp is Literal)
            {
                try
                {
                    return value == ((Literal)exp).GroundedValue.EffectiveBooleanValue();
                }
                catch (XPathException err)
                {
                    return false;
                }
            }

            return false;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static bool IsConstantOne(Expression exp)
        {
            if (exp is Literal)
            {
                IGroundedValue v = ((Literal)exp).GroundedValue;
                return v is Int64Value && ((Int64Value)v).LongValue() == 1;
            }

            return false;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static bool IsConstantZero(Expression exp)
        {
            if (exp is Literal)
            {
                IGroundedValue v = ((Literal)exp).GroundedValue;
                return v is Int64Value && ((Int64Value)v).LongValue() == 0;
            }

            return false;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override bool IsSubtreeExpression()
        {
            return true;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static Literal MakeEmptySequence()
        {
            return new Literal(EmptySequence.GetInstance());
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static Literal MakeLiteral(IGroundedValue value)
        {
            value = value.Reduce();
            if (value is StringValue)
            {
                return new StringLiteral((StringValue)value);
            }
            else if (value is IFunctionItem && !(value is MapItem || value is ArrayItem))
            {
                return new FunctionLiteral((IFunctionItem)value);
            }
            else
            {
                return new Literal(value);
            }
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public static Literal MakeLiteral(IGroundedValue value, Expression origin)
        {
            Literal lit = MakeLiteral(value);

            ExpressionTool.CopyLocationInfo(origin, lit);
            return lit;
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        public override Elaborator GetElaborator()
        {
            return new LiteralElaborator();
        }

        /*
      * Evaluate an expression as a String. This function must only be called in contexts
      * where it is known that the expression will return a single string (or where an empty sequence
      * is to be treated as a zero-length string). Implementations should not attempt to convert
      * the result to a string, other than converting () to "". This method is used mainly to
      * evaluate expressions produced by compiling an attribute value template.
      *
      * @exception OutSmart.DAXon.Transformation.XPathException if any dynamic error occurs evaluating the
      *     expression
      * @exception InvalidCastException if the result type of the
      *     expression is not xs:string?
      * @param context The context in which the expression is to be evaluated
      * @return the value of the expression, evaluated in the current context.
      *     The expression must return a string or (); if the value of the
      *     expression is (), this method returns "".
      */
        internal sealed class LiteralElaborator : PullElaborator
        {
            public override ISequenceEvaluator Eagerly()
            {
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                return new LiteralEvaluator(value);
            }

            public override ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
            {
                return Eagerly();
            }

            public override IPullEvaluator ElaborateForPull()
            {
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                return (context) => value.Iterate();
            }

            private static readonly IPushEvaluator EMPTY_PUSH = (@out, context) => null;

            public override IPushEvaluator ElaborateForPush()
            {
                Literal expr = (Literal)GetExpression();
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                if (value.GetLength() == 0)
                {

                    // empty-sequence content (e.g. an empty literal result element): true no-op,
                    // not a per-call foreach over an empty iterable
                    return EMPTY_PUSH;
                }

                if (value is IItem)
                {
                    return (@out, context) =>
                    {
                        @out.Append((IItem)value, expr.GetLocation(), ReceiverOption.ALL_NAMESPACES);
                        return null;
                    };
                }
                else
                {
                    return (@out, context) =>
                    {
                        foreach (IItem item in value.AsIterable())
                        {
                            @out.Append(item, expr.GetLocation(), ReceiverOption.ALL_NAMESPACES);
                        }

                        return null;
                    };
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                IItem item = value.Head();
                return (context) => item;
            }

            public override IBooleanEvaluator ElaborateForBoolean()
            {
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                try
                {
                    bool ebv = value.EffectiveBooleanValue();
                    return (context) => ebv;
                }
                catch (XPathException e)
                {
                    return (context) =>
                    {
                        throw e;
                    };
                }
            }

            public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
            {
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                try
                {
                    UnicodeString str = value.UnicodeStringValue;
                    return (context) => str;
                }
                catch (XPathException e)
                {
                    return (context) =>
                    {
                        throw e;
                    };
                }
            }

            public override IStringEvaluator ElaborateForString(bool zeroLengthWhenAbsent)
            {
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                try
                {
                    string str = value.GetStringValue();
                    return (context) => str;
                }
                catch (XPathException e)
                {
                    return (context) =>
                    {
                        throw e;
                    };
                }
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                IGroundedValue value = ((Literal)GetExpression()).GroundedValue;
                if (value.GetLength() == 0)
                {
                    return (context, pul) =>
                    {
                    };
                }
                else
                {
                    return base.ElaborateForUpdate();
                }
            }
        }
    }
}

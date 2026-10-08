////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal abstract class ElementCreator : ParentNodeConstructor
    {

        public bool bequeathNamespacesToChildren = true;
        public bool inheritNamespacesFromParent = true;
        public override int ImplementationMethod => Expression.PROCESS_METHOD;

        public override string StreamerName => "ElementCreator";
        public ElementCreator()
        {
        }

        public override ItemType GetItemType()
        {
            return NodeKindTest.ELEMENT;
        }

        public override int GetCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties() | StaticProperty.SINGLE_DOCUMENT_NODESET;
            if (GetValidationAction() == Validation.STRIP)
            {
                p |= StaticProperty.ALL_NODES_UNTYPED;
            }

            return p;
        }

        public override void SuppressValidation(int parentValidationMode)
        {
            if (GetValidationAction() == parentValidationMode && GetSchemaType() == null)
            {

                // TODO: is this safe? e.g. if the child has validation=strict but matches a skip wildcard in the parent
                SetValidationAction(Validation.PRESERVE, null);
            }
        }

        protected override void CheckContentSequence(IStaticContext env)
        {
            Operand[] components;
            if (GetContentExpression() is Block)
            {
                components = ((Block)GetContentExpression()).GetOperanda();
            }
            else
            {
                components = new Operand[]
                {
                    contentOp
                };
            }

            bool foundChild = false;
            bool foundPossibleChild = false;
            foreach (Operand o in components)
            {
                Expression component = o.GetChildExpression();
                ItemType it = component.GetItemType();
                if (it.IsAtomicType())
                {
                    foundChild = true;
                }
                else if (it is IFunctionItemType && !(it is ArrayItemType))
                {
                    string which = it is MapType ? "map" : "function";
                    XPathException de = new XPathException("Cannot add a " + which + " as a child of a constructed element");
                    de.SetErrorCode(IsXSLT() ? "XTDE0450" : "XQTY0105");
                    de.SetLocator(component.GetLocation());
                    de.SetIsTypeError(true);
                    throw de;
                }
                else if (it is NodeTest)
                {
                    bool maybeEmpty = Cardinality.AllowsZero(component.GetCardinality());
                    UType possibleNodeKinds = it.GetUType();
                    if (possibleNodeKinds.Overlaps(UType.TEXT))
                    {

                        // the text node might turn out to be zero-length. If that's a possibility,
                        // then we only issue a warning. Also, we need to completely ignore a known
                        // zero-length text node, which is included to prevent space-separation
                        // in an XQuery construct like <a>{@x}{@y}</b>
                        if (component is ValueOf && ((ValueOf)component).Select is StringLiteral)
                        {
                            string value = ((StringLiteral)((ValueOf)component).Select).Stringify();
                            if ((value.Length == 0))
                            {
                            }
                            else
                            {
                                foundChild = true;
                            }
                        }
                        else
                        {
                            foundPossibleChild = true;
                        }
                    }
                    else if (!possibleNodeKinds.Overlaps(UType.CHILD_NODE_KINDS))
                    {
                        if (maybeEmpty)
                        {
                            foundPossibleChild = true;
                        }
                        else
                        {
                            foundChild = true;
                        }
                    }
                    else if (foundChild && possibleNodeKinds == UType.ATTRIBUTE && !maybeEmpty)
                    {
                        XPathException de = new XPathException("Cannot create an attribute node after creating a child of the containing element");
                        de.SetErrorCode(IsXSLT() ? "XTDE0410" : "XQTY0024");
                        de.SetLocator(component.GetLocation());
                        throw de;
                    }
                    else if (foundChild && possibleNodeKinds == UType.NAMESPACE && !maybeEmpty)
                    {
                        XPathException de = new XPathException("Cannot create a namespace node after creating a child of the containing element");
                        de.SetErrorCode(IsXSLT() ? "XTDE0410" : "XQTY0024");
                        de.SetLocator(component.GetLocation());
                        throw de;
                    }
                    else if ((foundChild || foundPossibleChild) && possibleNodeKinds == UType.ATTRIBUTE)
                    {
                        env.IssueWarning("Creating an attribute here will fail if previous instructions create any children", DAXonErrorCode.SXWN9030, component.GetLocation());
                    }
                    else if ((foundChild || foundPossibleChild) && possibleNodeKinds == UType.NAMESPACE)
                    {
                        env.IssueWarning("Creating a namespace node here will fail if previous instructions create any children", DAXonErrorCode.SXWN9030, component.GetLocation());
                    }
                }
            }
        }

        protected virtual void ExportValidationAndType(ExpressionPresenter @out)
        {
            if (GetValidationAction() != Validation.SKIP && GetValidationAction() != Validation.BY_TYPE)
            {
                @out.EmitAttribute("validation", Validation.Describe(GetValidationAction()));
            }

            if (GetValidationAction() == Validation.BY_TYPE)
            {
                ISchemaType type = GetSchemaType();
                if (type != null)
                {
                    @out.EmitAttribute("type", type.GetStructuredQName());
                }
            }
        }

        protected virtual string GetInheritanceFlags()
        {
            string flags = "";
            if (!inheritNamespacesFromParent)
            {
                flags += "P";
            }

            if (!bequeathNamespacesToChildren)
            {
                flags += "C";
            }

            if (preservingTypes)
            {
                flags += "V";
            }

            return flags;
        }
    }
}

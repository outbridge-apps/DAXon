////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class ParserExtension
    {
        public ParserExtension()
        {
        }

        public virtual void NeedExtension(XPathParser p, string what)
        {
            p.Grumble(what + " require support for Saxon extensions, available in Saxon-PE or higher");
        }

        private void NeedUpdate(XPathParser p, string what)
        {
            p.Grumble(what + " requires support for XQuery Update, available in Saxon-EE or higher");
        }

        public virtual void HandleExternalFunctionDeclaration(XQueryParser p, XQueryFunction func)
        {
            NeedExtension(p, "External function declarations");
        }

        public virtual ItemType ParseExtendedItemType(XPathParser p)
        {
            return null;
        }

        public virtual Expression ParseTypePattern(XPathParser p)
        {
            NeedExtension(p, "type-based patterns");
            return null;
        }

        public virtual void ParseItemTypeDeclaration(XQueryParser p)
        {
            NeedExtension(p, "Item type declarations");
        }

        public virtual void ParseRevalidationDeclaration(XQueryParser p)
        {
            NeedUpdate(p, "A revalidation declaration");
        }

        public virtual void ParseUpdatingFunctionDeclaration(XQueryParser p)
        {
            NeedUpdate(p, "An updating function");
        }

        public virtual Expression ParseExtendedExprSingle(XPathParser p)
        {
            return null;
        }

        internal sealed class TemporaryXSLTVariableBinding : ILocalBinding
        {
            public SourceBinding declaration;

            public int LocalSlotNumber => 0;

            public IntegerValue[] IntegerBoundsForVariable => null;
            public TemporaryXSLTVariableBinding(SourceBinding decl)
            {
                this.declaration = decl;
            }

            public SequenceType GetRequiredType()
            {
                return declaration.GetInferredType(true);
            }

            public ISequence EvaluateVariable(IXPathContext context)
            {
                throw new NotSupportedException();
            }

            public bool IsGlobal()
            {
                return false;
            }

            public bool IsAssignable()
            {
                return false;
            }

            public StructuredQName GetVariableQName()
            {
                return declaration.VariableQName;
            }

            public void AddReference(VariableReference @ref, bool isLoopingReference)
            {
            }

            public void SetIndexedVariable()
            {
            }

            public bool IsIndexedVariable()
            {
                return false;
            }
        }
    }
}
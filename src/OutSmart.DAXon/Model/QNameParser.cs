////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Model
{
    public class QNameParser
    {
        private INamespaceResolver resolver;
        private bool acceptEQName = false;
        private string errorOnBadSyntax = "XPST0003";
        private string errorOnUnresolvedPrefix = "XPST0081";
        private XQueryParser.Unescaper unescaper = null;
        public QNameParser(INamespaceResolver resolver)
        {
            this.resolver = resolver;
        }

        public virtual QNameParser WithNamespaceResolver(INamespaceResolver resolver)
        {
            QNameParser qp2 = Copy();
            qp2.resolver = resolver;
            return qp2;
        }

        public virtual QNameParser WithAcceptEQName(bool acceptEQName)
        {
            if (acceptEQName == this.acceptEQName)
            {
                return this;
            }

            QNameParser qp2 = Copy();
            qp2.acceptEQName = acceptEQName;
            return qp2;
        }

        public virtual QNameParser WithErrorOnBadSyntax(string code)
        {
            if (code.Equals(errorOnBadSyntax))
            {
                return this;
            }

            QNameParser qp2 = Copy();
            qp2.errorOnBadSyntax = code;
            return qp2;
        }

        public virtual QNameParser WithErrorOnUnresolvedPrefix(string code)
        {
            if (code.Equals(errorOnUnresolvedPrefix))
            {
                return this;
            }

            QNameParser qp2 = Copy();
            qp2.errorOnUnresolvedPrefix = code;
            return qp2;
        }

        public virtual QNameParser WithUnescaper(XQueryParser.Unescaper unescaper)
        {
            QNameParser qp2 = Copy();
            qp2.unescaper = unescaper;
            return qp2;
        }

        private QNameParser Copy()
        {
            QNameParser qp2 = new QNameParser(resolver);
            qp2.acceptEQName = acceptEQName;
            qp2.errorOnBadSyntax = errorOnBadSyntax;
            qp2.errorOnUnresolvedPrefix = errorOnUnresolvedPrefix;
            qp2.unescaper = unescaper;
            return qp2;
        }

        public virtual StructuredQName Parse(string lexicalName, NamespaceUri defaultNS)
        {
            lexicalName = Whitespace.Trim(lexicalName);
            if (acceptEQName && lexicalName.Length >= 4 && lexicalName[0] == 'Q' && lexicalName[1] == '{')
            {
                int endBrace = lexicalName.IndexOf('}');
                if (endBrace < 0)
                {
                    throw new XPathException("Invalid EQName: closing brace not found", errorOnBadSyntax);
                }
                else if (endBrace == lexicalName.Length - 1)
                {
                    throw new XPathException("Invalid EQName: local part is missing", errorOnBadSyntax);
                }

                string uri = Whitespace.CollapseWhitespace(lexicalName.Substring(2, endBrace - 2) /*Java substring(begin,END) -> C# (start,LENGTH)*/);
                if (uri.Contains("{"))
                {
                    throw new XPathException("Invalid EQName: URI contains opening brace", errorOnBadSyntax);
                }

                if (unescaper != null && uri.Contains("&"))
                {
                    uri = unescaper.Unescape(uri).ToString();
                }

                if (uri.Equals(NamespaceConstant.XMLNS))
                {
                    throw new XPathException("The string '" + NamespaceConstant.XMLNS + "' cannot be used as a namespace URI", "XQST0070");
                }

                string local = lexicalName.Substring(endBrace + 1);
                CheckLocalName(local);
                return new StructuredQName("", NamespaceUri.Of(uri), local);
            }

            try
            {
                string[] parts = NameChecker.GetQNameParts(lexicalName);
                CheckLocalName(parts[1]);
                if ((parts[0].Length == 0))
                {
                    return new StructuredQName("", defaultNS, parts[1]);
                }

                NamespaceUri uri = resolver.GetURIForPrefix(parts[0], false);
                if (uri == null)
                {
                    throw new XPathException("Namespace prefix '" + parts[0] + "' has not been declared", errorOnUnresolvedPrefix);
                }

                return new StructuredQName(parts[0], uri, parts[1]);
            }
            catch (QNameException e)
            {
                throw new XPathException(e.GetMessage(), errorOnBadSyntax);
            }
        }

        private void CheckLocalName(string local)
        {
            if (!NameChecker.IsValidNCName(local))
            {
                throw new XPathException("Invalid EQName: local part is not a valid NCName", errorOnBadSyntax);
            }
        }
    }
}
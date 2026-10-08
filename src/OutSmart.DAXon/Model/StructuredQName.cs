////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Model
{
    public class StructuredQName : IIdentityComparable
    {
        private readonly string prefix;
        private readonly NamespaceUri uri;
        private readonly string local;
        private int cachedHashCode = -1;

        public virtual string DisplayName
        {
            get
            {
                if ((prefix.Length == 0))
                {
                    return local;
                }
                else
                {
                    return prefix + ":" + local;
                }
            }
        }

        public virtual string ClarkName
        {
            get
            {
                if (uri == NamespaceUri.NULL)
                {
                    return local;
                }
                else
                {
                    return "{" + uri + "}" + local;
                }
            }
        }

        public virtual string EQName
        {
            get
            {
                if (uri == NamespaceUri.NULL)
                {
                    return "Q{}" + local;
                }
                else
                {
                    return "Q{" + uri + "}" + local;
                }
            }
        }
        public StructuredQName(string prefix, NamespaceUri uri, string localName)
        {
            this.prefix = prefix == null ? "" : prefix;
            this.uri = uri;
            this.local = localName;
        }

        public StructuredQName(string prefix, string uri, string localName)
        {
            this.prefix = prefix == null ? "" : prefix;
            this.uri = NamespaceUri.Of(uri);
            this.local = localName;
        }

        public static StructuredQName FromClarkName(string expandedName)
        {
            string @namespace;
            string localName;
            if (expandedName.StartsWith("Q{", StringComparison.Ordinal))
            {
                expandedName = expandedName.Substring(1);
            }

            if (expandedName[0] == '{')
            {
                int closeBrace = expandedName.IndexOf('}');
                if (closeBrace < 0)
                {
                    throw new ArgumentException("No closing '}' in Clark name");
                }

                @namespace = expandedName.Substring(1, closeBrace - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                if (closeBrace == expandedName.Length)
                {
                    throw new ArgumentException("Missing local part in Clark name");
                }

                localName = expandedName.Substring(closeBrace + 1);
            }
            else
            {
                @namespace = "";
                localName = expandedName;
            }

            return new StructuredQName("", NamespaceUri.Of(@namespace), localName);
        }

        public static StructuredQName FromLexicalQName(string lexicalName, bool useDefault, bool allowEQName, INamespaceResolver resolver)
        {
            lexicalName = Whitespace.Trim(lexicalName);
            if (allowEQName && lexicalName.Length >= 4 && lexicalName[0] == 'Q' && lexicalName[1] == '{')
            {
                string name = lexicalName.ToString();
                int endBrace = name.IndexOf('}', 2);
                if (endBrace < 0)
                {
                    throw new XPathException("Invalid EQName: closing brace not found", "FOCA0002");
                }
                else if (endBrace == name.Length - 1)
                {
                    throw new XPathException("Invalid EQName: local part is missing", "FOCA0002");
                }

                string uri = name.Substring(2, endBrace - 2); // Java substring(begin,end) -> C# (start,LENGTH)
                if (uri.IndexOf('{', 0) >= 0)
                {
                    throw new XPathException("Namespace URI must not contain '{'", "FOCA0002");
                }

                string local = name.Substring(endBrace + 1); // Java substring(begin,end) -> C# to-end overload
                if (!NameChecker.IsValidNCName(StringTool.CodePoints(local)))
                {
                    throw new XPathException("Invalid EQName: local part is not a valid NCName", "FOCA0002");
                }

                return new StructuredQName("", NamespaceUri.Of(uri), local);
            }

            try
            {
                string[] parts = NameChecker.GetQNameParts(lexicalName);
                NamespaceUri uri = resolver.GetURIForPrefix(parts[0], useDefault);
                if (uri == null)
                {
                    if (NameChecker.IsValidNCName(parts[0]))
                    {
                        throw new XPathException("Namespace prefix '" + parts[0] + "' has not been declared", "FONS0004");
                    }
                    else
                    {
                        throw new XPathException("Invalid namespace prefix '" + parts[0] + "'", "FOCA0002");
                    }
                }

                return new StructuredQName(parts[0], uri, parts[1]);
            }
            catch (QNameException e)
            {
                throw new XPathException(e.GetMessage(), "FOCA0002");
            }
        }

        public static StructuredQName FromEQName(string eqName)
        {
            eqName = Whitespace.Trim(eqName);
            if (eqName.Length >= 4 && eqName.StartsWith("Q{", StringComparison.Ordinal))
            {
                int endBrace = eqName.IndexOf('}');
                if (endBrace < 0)
                {
                    throw new ArgumentException("Invalid EQName: closing brace not found");
                }
                else if (endBrace == eqName.Length - 1)
                {
                    throw new ArgumentException("Invalid EQName: local part is missing");
                }

                string uri = eqName.Substring(2, endBrace - 2); /*Java substring(begin,END) -> C# (start,LENGTH)*/
                if (uri.IndexOf('{') >= 0)
                {
                    throw new ArgumentException("Invalid EQName: open brace in URI part");
                }

                string local = eqName.Substring(endBrace + 1);
                return new StructuredQName("", NamespaceUri.Of(uri), local);
            }
            else
            {
                return new StructuredQName("", NamespaceUri.NULL, eqName);
            }
        }

        public virtual string GetPrefix()
        {
            return prefix;
        }

        public virtual NamespaceUri GetNamespaceUri()
        {
            return this.uri;
        }

        public virtual string GetURI()
        {
            return this.uri.ToString();
        }

        public virtual bool HasURI(NamespaceUri uri)
        {
            return this.uri == uri;
        }

        public virtual string GetLocalPart()
        {
            return local;
        }

        public virtual StructuredQName GetStructuredQName()
        {
            return this;
        }

        public override string ToString()   // override (was a hide): string concat printed the class name
        {
            return DisplayName;
        }

        public override bool Equals(object other)
        {
            if (this == other)
            {
                return true;
            }

            if (other is StructuredQName)
            {
                return local.Equals(((StructuredQName)other).local) && uri == ((StructuredQName)other).uri;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            if (cachedHashCode == -1)
            {
                return cachedHashCode = 0x5004a00b ^ local.GetHashCode() ^ uri.GetHashCode();
            }
            else
            {
                return cachedHashCode;
            }
        }

        public static int ComputeHashCode(NamespaceUri uri, string local)
        {
            return 0x5004a00b ^ local.GetHashCode() ^ uri.GetHashCode();
        }

        public virtual System.Xml.XmlQualifiedName ToXmlQualifiedName()
        {
            return new System.Xml.XmlQualifiedName(GetLocalPart(), GetNamespaceUri().ToString());
        }

        public virtual NamespaceBinding GetNamespaceBinding()
        {
            return new NamespaceBinding(GetPrefix(), GetNamespaceUri());
        }

        public virtual bool IsIdentical(IIdentityComparable other)
        {
            return Equals(other) && ((StructuredQName)other).GetPrefix().Equals(GetPrefix());
        }

        public virtual int IdentityHashCode()
        {
            return GetHashCode() ^ GetPrefix().GetHashCode();
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Text;
namespace OutSmart.DAXon.Types
{
    public class JavaExternalObjectType : ExternalObjectType
    {
        // Concurrent: shared across every Processor in the process; two threads first wrapping the
        // same (or different) CLR types at once must not race a plain Dictionary's resize.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, JavaExternalObjectType> cache
            = new System.Collections.Concurrent.ConcurrentDictionary<System.Type, JavaExternalObjectType>();
        protected System.Type javaClass;

        public override string Name => ClassNameToQName(javaClass.FullName).GetLocalPart();

        public override string TargetNamespace => NamespaceConstant.JAVA_TYPE;

        public override StructuredQName TypeName => ClassNameToQName(javaClass.FullName);

        public virtual System.Type JavaClass => javaClass;

        public virtual string DisplayName => "java-type:" + javaClass.FullName;
        private JavaExternalObjectType(System.Type javaClass)
        {
            this.javaClass = javaClass;
        }

        public static JavaExternalObjectType Of(System.Type javaClass)
        {
            return cache.GetOrAdd(javaClass, k => new JavaExternalObjectType(k));
        }

        public override ItemType GetPrimitiveItemType()
        {
            return new JavaExternalObjectType(typeof(object));
        }

        public virtual Affinity GetRelationship(JavaExternalObjectType other)
        {
            System.Type j2 = other.javaClass;
            if (javaClass.Equals(j2))
            {
                return Affinity.SAME_TYPE;
            }
            else if (javaClass.IsAssignableFrom(j2))
            {
                return Affinity.SUBSUMES;
            }
            else if (j2.IsAssignableFrom(javaClass))
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (javaClass.IsInterface || j2.IsInterface)
            {
                return Affinity.OVERLAPS; // there may be an overlap, we play safe
            }
            else
            {
                return Affinity.DISJOINT;
            }
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            if (item.GetGenre() == Genre.EXTERNAL)
            {
                object obj = ((ObjectValue<object>)item).GetObject();
                return javaClass.IsAssignableFrom(obj.GetType());
            }

            return false;
        }

        public override string ToString()
        {
            return ClassNameToQName(javaClass.FullName).EQName;
        }

        public override int GetHashCode()
        {
            return javaClass.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is JavaExternalObjectType && javaClass == ((JavaExternalObjectType)obj).javaClass;
        }

        public static string ClassNameToLocalName(string className)
        {
            return className.Replace('$', '-').Replace("[", "_-");
        }

        public static string LocalNameToClassName(string className)
        {
            StringBuilder fsb = new StringBuilder(className.Length);
            bool atStart = true;
            for (int i = 0; i < className.Length; i++)
            {
                char c = className[i];
                if (atStart)
                {
                    if (c == '_' && i + 1 < className.Length && className[i + 1] == '-')
                    {
                        fsb.Append('[');
                        i++;
                    }
                    else
                    {
                        atStart = false;
                        fsb.Append(c == '-' ? '$' : c);
                    }
                }
                else
                {
                    fsb.Append(c == '-' ? '$' : c);
                }
            }

            return fsb.ToString();
        }

        /// <summary>
        /// Static method to get the QName corresponding to a Java class name
        /// </summary>
        public static StructuredQName ClassNameToQName(string className)
        {
            return new StructuredQName("jt", NamespaceUri.JAVA_TYPE, ClassNameToLocalName(className));
        }
    }
}
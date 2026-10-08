////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Transformation
{
    public class SymbolicName
    {
        private readonly int kind;
        private readonly StructuredQName name;

        public virtual int ComponentKind => kind;

        public virtual StructuredQName ComponentName => name;

        /// <summary>
        /// Get a short name suitable for use in messages
        /// </summary>
        public virtual string ShortName => name.DisplayName;
        public SymbolicName(int kind, StructuredQName name)
        {
            this.kind = kind;
            this.name = name;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return kind << 16 ^ name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is SymbolicName && ((SymbolicName)obj).kind == this.kind && ((SymbolicName)obj).name.Equals(this.name);
        }

        public override string ToString()
        {

            return StandardNames.GetLocalName(kind) + " " + name.DisplayName;
        }

        /// <summary>
        /// Subclass of SymbolicName used for function names (including the arity)
        /// </summary>
        public class F : SymbolicName
        {
            int arity;

            /// <summary>
            /// Get a short name suitable for use in messages
            /// </summary>
            public override string ShortName => base.ShortName + "#" + arity;
            public F(StructuredQName name, int arity) : base(StandardNames.XSL_FUNCTION, name)
            {
                this.arity = arity;
            }

            public virtual int GetArity()
            {
                return arity;
            }

            /// <summary>
            /// Returns a hash code value for the object.
            /// </summary>
            public override int GetHashCode()
            {
                return base.GetHashCode() ^ arity;
            }

            public override bool Equals(object obj)
            {
                return obj is F && base.Equals(obj) && ((F)obj).arity == this.arity;
            }

            public override string ToString()
            {

                return base.ToString() + "#" + arity;
            }
        }
    }
}

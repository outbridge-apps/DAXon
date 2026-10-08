////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Transformation
{
    public class ComponentTest
    {
        private readonly int componentKind;
        private readonly IQNameTest nameTest;
        private readonly int arity;

        public virtual int ComponentKind => componentKind;

        public virtual IQNameTest QNameTest => nameTest;

        public virtual SymbolicName SymbolicNameIfExplicit
        {
            get
            {
                if (nameTest is NameTest)
                {
                    if (componentKind == StandardNames.XSL_FUNCTION)
                    {
                        return new SymbolicName.F(((NameTest)nameTest).MatchingNodeName, arity);
                    }
                    else
                    {
                        return new SymbolicName(componentKind, ((NameTest)nameTest).MatchingNodeName);
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        public ComponentTest(int componentKind, IQNameTest nameTest, int arity)
        {
            this.componentKind = componentKind;
            this.nameTest = nameTest;
            this.arity = arity;
        }

        public virtual int GetArity()
        {
            return arity;
        }

        public virtual bool IsPartialWildcard()
        {
            return nameTest is LocalNameTest || nameTest is NamespaceTest;
        }

        public virtual bool Matches(Actor component)
        {
            return Matches(component.GetSymbolicName());
        }

        public virtual bool Matches(SymbolicName sn)
        {
            return (componentKind == -1 || sn.ComponentKind == componentKind) && nameTest.Matches(sn.ComponentName) && !((componentKind == StandardNames.XSL_FUNCTION) && arity != -1 && arity != ((SymbolicName.F)sn).GetArity());
        }

        public override bool Equals(object other)
        {
            return other is ComponentTest && ((ComponentTest)other).componentKind == componentKind && ((ComponentTest)other).arity == arity && ((ComponentTest)other).nameTest.Equals(nameTest);
        }

        public override int GetHashCode()
        {
            return componentKind ^ arity ^ nameTest.GetHashCode();
        }
    }
}
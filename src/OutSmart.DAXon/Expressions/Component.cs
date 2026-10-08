////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Expressions
{
    public class Component
    {
        protected Actor actor;
        private Visibility visibility;
        private IList<ComponentBinding> bindings = new List<ComponentBinding>();
        private StylesheetPackage containingPackage;
        private StylesheetPackage declaringPackage;
        private VisibilityProvenance provenance;
        private Component baseComponent;

        public virtual IList<ComponentBinding> ComponentBindings
        {
            get => bindings; set
            {
                this.bindings = value;
            }
        }

        public virtual StylesheetPackage DeclaringPackage => declaringPackage;

        public virtual StylesheetPackage ContainingPackage => containingPackage;

        public virtual Component BaseComponent
        {
            get => baseComponent; set
            {
                baseComponent = value;
            }
        }

        public virtual int ComponentKind
        {
            get
            {
                if (actor is NamedTemplate)
                {
                    return StandardNames.XSL_TEMPLATE;
                }
                else if (actor is GlobalVariable)
                {
                    return StandardNames.XSL_VARIABLE;
                }
                else if (actor is IFunctionItem)
                {
                    return StandardNames.XSL_FUNCTION;
                }
                else if (actor is AttributeSet)
                {
                    return StandardNames.XSL_ATTRIBUTE_SET;
                }
                else if (actor is Mode)
                {
                    return StandardNames.XSL_MODE;
                }
                else
                {
                    return -1;
                }
            }
        }
        private Component()
        {
        }

        public static Component MakeComponent(Actor actor, Visibility visibility, VisibilityProvenance provenance, StylesheetPackage containingPackage, StylesheetPackage declaringPackage)
        {
            Component c;
            if (actor is Mode)
            {
                c = new M();
            }
            else
            {
                c = new Component();
            }

            c.actor = actor;
            c.visibility = visibility;
            c.provenance = provenance;
            c.containingPackage = containingPackage;
            c.declaringPackage = declaringPackage;
            return c;
        }

        public virtual void SetVisibility(Visibility visibility, VisibilityProvenance provenance)
        {
            this.visibility = visibility;
            this.provenance = provenance;
        }

        public virtual Visibility GetVisibility()
        {
            return visibility;
        }

        public virtual VisibilityProvenance GetVisibilityProvenance()
        {
            return provenance;
        }

        public virtual bool IsHiddenAbstractComponent()
        {
            return visibility == Visibility.HIDDEN && baseComponent != null && baseComponent.GetVisibility() == Visibility.ABSTRACT;
        }

        public virtual Actor GetActor()
        {
            return actor;
        }

        public virtual void Export(ExpressionPresenter @out, Dictionary<Component, int> componentIdMap, Dictionary<StylesheetPackage, int> packageIdMap)
        {
            @out.StartElement("co");
            int id = ObtainComponentId(this, componentIdMap);
            @out.EmitAttribute("id", "" + id);
            if (provenance != VisibilityProvenance.DEFAULTED)
            {
                @out.EmitAttribute("vis", GetVisibility().ToString());
            }

            string refs = ListComponentReferences(componentIdMap);
            @out.EmitAttribute("binds", refs);
            if (baseComponent != null && GetActor() == baseComponent.GetActor())
            {
                int baseId = ObtainComponentId(baseComponent, componentIdMap);
                @out.EmitAttribute("base", "" + baseId);
                @out.EmitAttribute("dpack", packageIdMap.GetOrDefault(declaringPackage) + "");
            }
            else
            {
                actor.Export(@out);
            }

            @out.EndElement();
        }

        public virtual string ListComponentReferences(Dictionary<Component, int> componentIdMap)
        {
            StringBuilder fsb = new StringBuilder(128);
            foreach (ComponentBinding @ref in ComponentBindings)
            {
                Component target = @ref.GetTarget();
                int targetId = ObtainComponentId(target, componentIdMap);
                if (fsb.Length != 0)
                {
                    fsb.Append(' ');
                }

                fsb.Append("" + targetId);
            }

            return fsb.ToString();
        }

        private int ObtainComponentId(Component component, Dictionary<Component, int> componentIdMap)
        {
            int id = componentIdMap.GetOrDefault(component, int.MinValue);
            if (id == int.MinValue)
            {
                id = componentIdMap.Count;
                componentIdMap[component] = id;
            }

            return id;
        }

        public class M : Component
        {
            public new Mode GetActor()
            {
                return (Mode)base.GetActor();
            }

            public virtual void SetActor(Mode m)
            {
                this.actor = m;
            }
        }
    }
}
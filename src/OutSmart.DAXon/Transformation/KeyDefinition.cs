////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// Corresponds to a single xsl:key declaration.
    /// </summary>
    public class KeyDefinition : Actor, IContextOriginator
    {
        private readonly SymbolicName symbolicName;
        private readonly Patterns.Pattern match; // the match pattern
        private BuiltInAtomicType useType; // the type of the values returned by the atomized use expression
        private readonly IStringCollator collation; // the collating sequence, when type=string
        private readonly string collationName; // the collation URI
        private bool backwardsCompatible = false;
        private bool strictComparison = false;
        private bool convertUntypedToOther = false;
        private bool rangeKey = false;
        private bool composite = false;
        private IPullEvaluator useExpressionEvaluator;

        public virtual BuiltInAtomicType IndexedItemType
        {
            get
            {
                if (useType == null)
                {
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else
                {
                    return useType;
                }
            }
            set
            {
                useType = value;
            }
        }

        /*&& map.getNumberOfVariables() > 0 */
        public virtual Patterns.Pattern Match => match;

        /*&& map.getNumberOfVariables() > 0 */
        public virtual Expression Use => GetBody();

        /*&& map.getNumberOfVariables() > 0 */
        public virtual string CollationName => collationName;

        /*&& map.getNumberOfVariables() > 0 */
        public virtual IStringCollator Collation => collation;
        public KeyDefinition(SymbolicName symbolicName, Patterns.Pattern match, Expression use, string collationName, IStringCollator collation)
        {
            this.symbolicName = symbolicName;
            this.match = match;
            SetBody(use);
            this.collation = collation;
            this.collationName = collationName;
        }

        public override SymbolicName GetSymbolicName()
        {
            return symbolicName;
        }

        public virtual void SetRangeKey(bool rangeKey)
        {
            this.rangeKey = rangeKey;
        }

        public virtual bool IsRangeKey()
        {
            return rangeKey;
        }

        public virtual void SetComposite(bool composite)
        {
            this.composite = composite;
        }

        public virtual bool IsComposite()
        {
            return composite;
        }

        public virtual void SetBackwardsCompatible(bool bc)
        {
            backwardsCompatible = bc;
        }

        public virtual bool IsBackwardsCompatible()
        {
            return backwardsCompatible;
        }

        public virtual void SetStrictComparison(bool strict)
        {
            strictComparison = strict;
        }

        public virtual bool IsStrictComparison()
        {
            return strictComparison;
        }

        public virtual void SetConvertUntypedToOther(bool convertToOther)
        {
            convertUntypedToOther = convertToOther;
        }

        public virtual bool IsConvertUntypedToOther()
        {
            return convertUntypedToOther;
        }

        public override void SetStackFrameMap(SlotManager map)
        {
            if (map != null)
            {
                base.SetStackFrameMap(map);
            }
        }

        /*&& map.getNumberOfVariables() > 0 */
        public override void AllocateAllBindingSlots(StylesheetPackage pack)
        {
            base.AllocateAllBindingSlots(pack);
            AllocateBindingSlotsRecursive(pack, this, match, DeclaringComponent.ComponentBindings);
        }

        /*&& map.getNumberOfVariables() > 0 */
        public virtual void SetLocation(ILocation loc)
        {
            SetSystemId(loc.GetSystemId());
            SetLineNumber(loc.GetLineNumber());
            SetColumnNumber(loc.GetColumnNumber());
        }

        /*&& map.getNumberOfVariables() > 0 */
        public virtual IPullEvaluator ObtainUseEvaluator()
        {
            lock (syncLock)
            {
                if (useExpressionEvaluator == null)
                {
                    useExpressionEvaluator = GetBody().MakeElaborator().ElaborateForPull();
                }

                return useExpressionEvaluator;
            }
        }

        /*&& map.getNumberOfVariables() > 0 */
        public virtual StructuredQName GetObjectName()
        {
            return symbolicName.ComponentName;
        }

        /*&& map.getNumberOfVariables() > 0 */
        public virtual void Export(ExpressionPresenter @out, bool reusable, Dictionary<Component, int> componentIdMap)
        {
            @out.StartElement("key");
            @out.EmitAttribute("name", GetObjectName());
            if (!NamespaceConstant.CODEPOINT_COLLATION_URI.Equals(collationName))
            {
                @out.EmitAttribute("collation", collationName);
            }

            @out.EmitAttribute("line", GetLineNumber() + "");
            @out.EmitAttribute("module", GetSystemId());
            if (GetStackFrameMap() != null && GetStackFrameMap().NumberOfVariables != 0)
            {
                @out.EmitAttribute("slots", GetStackFrameMap().NumberOfVariables + "");
            }

            if (componentIdMap != null)
            {
                @out.EmitAttribute("binds", "" + DeclaringComponent.ListComponentReferences(componentIdMap));
            }

            string flags = "";
            if (backwardsCompatible)
            {
                flags += "b";
            }

            if (IsRangeKey())
            {
                flags += "r";
                @out.EmitAttribute("range", "1");
            }

            if (match.GetUType().Overlaps(UType.ATTRIBUTE))
            {
                flags += "a";
            }

            if (match.GetUType().Overlaps(UType.NAMESPACE))
            {
                flags += "n";
            }

            if (composite)
            {
                flags += "c";
            }

            if (reusable)
            {
                flags += "u";
            }

            if (convertUntypedToOther)
            {
                flags += "v";
            }

            if (strictComparison)
            {
                flags += "s";
            }

            if (flags != "")
            {
                @out.EmitAttribute("flags", flags);
            }

            Match.Export(@out);
            GetBody().Export(@out);
            @out.EndElement();
        }

        /*&& map.getNumberOfVariables() > 0 */
        public override void Export(ExpressionPresenter presenter)
        {
            throw new NotSupportedException();
        }
    }
}

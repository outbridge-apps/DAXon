////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.PostureAndSweep;
//import com.saxonica.ee.stream.Streamability;
//import com.saxonica.ee.stream.Sweep;
//import com.saxonica.ee.trans.ContextItemStaticInfoEE;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:accumulator elements in a stylesheet (XSLT 3.0).
    /// </summary>
    internal sealed class XSLAccumulator : StyleElement, IStylesheetComponent
    {
        private readonly Accumulator accumulator = new Accumulator();
        private SlotManager slotManager;

        public SequenceType ResultType => accumulator.GetType();
        public Actor GetActor()
        {
            if (accumulator.DeclaringComponent == null)
            {
                accumulator.MakeDeclaringComponent(Visibility.PRIVATE, ContainingPackage);
            }

            return accumulator;
        }

        public SymbolicName GetSymbolicName()
        {
            StructuredQName qname = accumulator.AccumulatorName;
            return qname == null ? null : new SymbolicName(StandardNames.XSL_ACCUMULATOR, null);
        }

        public void CheckCompatibility(Component component)
        {
        }

        public override bool IsDeclaration()
        {
            return true;
        }

        private void PrepareSimpleAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (f.Equals("name"))
                {
                    string name = Whitespace.Trim(value);
                    accumulator.AccumulatorName = MakeQName(name, null, "name");
                }
                else if (f.Equals("streamable"))
                {
                    accumulator.SetDeclaredStreamable(false);
                    bool streamable = ProcessStreamableAtt(value);
                    accumulator.SetDeclaredStreamable(streamable);
                }
                else if (attName.HasURI(NamespaceUri.SAXON) && attName.GetLocalPart().Equals("trace"))
                {
                    if (IsExtensionAttributeAllowed(attName.DisplayName))
                    {
                        accumulator.SetTracing(ProcessBooleanAttribute("saxon:trace", value));
                    }
                }
                else
                {
                }
            }

            if (accumulator.AccumulatorName == null)
            {
                ReportAbsence("name");

                // recovery: bug 5585
                accumulator.AccumulatorName = new StructuredQName("anon", NamespaceConstant.ANONYMOUS, "acc" + GetHashCode());
            }
        }

        public override void PrepareAttributes()
        {
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                if (f.Equals("name"))
                {
                }
                else if (f.Equals("streamable"))
                {
                }
                else if (f.Equals("initial-value"))
                {
                    accumulator.InitialValueExpression = MakeExpression(value, att);
                }
                else if (f.Equals("as"))
                {
                    try
                    {
                        SequenceType requiredType = MakeSequenceType(value);
                        accumulator.SetType(requiredType);
                    }
                    catch (XPathException e)
                    {
                        CompileErrorInAttribute(e, "as");
                    }
                }
                else if (attName.HasURI(NamespaceUri.SAXON) && attName.GetLocalPart().Equals("trace"))
                {
                    if (IsExtensionAttributeAllowed(attName.DisplayName))
                    {
                        accumulator.SetTracing(ProcessBooleanAttribute("saxon:trace", value));
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                } // TODO: add saxon:as
            }

            if (accumulator.GetType() == null)
            {
                accumulator.SetType(SequenceType.ANY_SEQUENCE);
            }

            if (accumulator.InitialValueExpression == null)
            {
                ReportAbsence("initial-value");
                StringLiteral zls = new StringLiteral(StringValue.EMPTY_STRING);
                zls.SetRetainedStaticContext(MakeRetainedStaticContext());
                accumulator.InitialValueExpression = zls;
            }
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            Configuration config = compilation.GetConfiguration();

            // Prepare the initial value expression
            {
                accumulator.SetPackageData(compilation.GetPackageData());
                accumulator.ObtainDeclaringComponent(decl.SourceElement);
                Expression init = accumulator.InitialValueExpression;
                ExpressionVisitor visitor = ExpressionVisitor.Make(GetStaticContext());
                init = init.TypeCheck(visitor, config.DefaultContextItemStaticInfo);
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:accumulator-rule/select", 0);
                init = config.GetTypeChecker(false).StaticTypeCheck(init, accumulator.GetType(), role, visitor);
                init = init.Optimize(visitor, config.DefaultContextItemStaticInfo);
                SlotManager stackFrameMap = config.MakeSlotManager();
                ExpressionTool.AllocateSlots(init, 0, stackFrameMap);
                accumulator.SlotManagerForInitialValueExpression = stackFrameMap;
                CheckInitialStreamability();
                accumulator.InitialValueExpression = init;
                accumulator.AddChildExpression(init);
            }


            // Prepare the new-value (select) expressions
            int position = 0;
            foreach (NodeInfo curr in Children(new TypeIsInstancePredicate(typeof(XSLAccumulatorRule))))
            {
                XSLAccumulatorRule rule = (XSLAccumulatorRule)curr;
                Patterns.Pattern pattern = rule.Match;
                Expression newValueExp = rule.GetNewValueExpression(compilation, decl);
                ExpressionVisitor visitor = ExpressionVisitor.Make(GetStaticContext());
                newValueExp = newValueExp.TypeCheck(visitor, config.MakeContextItemStaticInfo(pattern.GetItemType(), false));
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:accumulator-rule/select", 0);
                newValueExp = config.GetTypeChecker(false).StaticTypeCheck(newValueExp, accumulator.GetType(), role, visitor);
                newValueExp = newValueExp.Optimize(visitor, GetConfiguration().MakeContextItemStaticInfo(pattern.GetItemType(), false));
                int valueSlot = slotManager.AllocateSlotNumber(NamespaceUri.NULL.QName("value"), null);
                ExpressionTool.AllocateSlots(newValueExp, valueSlot + 1, slotManager);
                bool isPreDescent = !rule.IsPostDescent();
                SimpleMode mode = isPreDescent ? accumulator.PreDescentRules : accumulator.PostDescentRules;
                AccumulatorRule action = new AccumulatorRule(newValueExp, slotManager, rule.IsPostDescent());
                action.SetLocation(rule.SaveLocation());
                action.SetAccumulatorName(((XSLAccumulator)rule.GetParent()).GetObjectName());
                mode.AddRule(pattern, action, decl.Module, decl.Module.Precedence, 1, position++, 0);
                CheckRuleStreamability();
                if (accumulator.IsDeclaredStreamable() && rule.IsPostDescent() && rule.IsCapture())
                {
                    action.SetCapturing(true);
                }

                ItemType itemType = pattern.GetItemType();
                if (itemType is NodeTest)
                {
                    if (!itemType.GetUType().Overlaps(UType.DOCUMENT.Union(UType.CHILD_NODE_KINDS)))
                    {
                        rule.IssueWarning("An accumulator rule that matches attribute or namespace nodes has no effect", "SXWN9999");
                    }
                }
                else if (itemType is IAtomicType)
                {
                    rule.IssueWarning("An accumulator rule that matches atomic values has no effect", "SXWN9999");
                }

                accumulator.AddChildExpression(newValueExp);
                accumulator.AddChildExpression(pattern);
                if (GetCompilation().GetCompilerInfo().CodeInjector != null)
                {
                    GetCompilation().GetCompilerInfo().CodeInjector.Process(action);
                }
            }

            accumulator.PreDescentRules.AllocateAllPatternSlots();
            accumulator.PostDescentRules.AllocateAllPatternSlots();
        }

        public override StructuredQName GetObjectName()
        {
            StructuredQName qn = base.GetObjectName();
            if (qn == null)
            {
                string nameAtt = Whitespace.Trim(GetAttributeValue(NamespaceUri.NULL, "name"));
                if (nameAtt == null)
                {
                    return new StructuredQName("saxon", NamespaceUri.SAXON, "badly-named-accumulator" + GenerateId());
                }

                qn = MakeQName(nameAtt, null, "name");
                SetObjectName(qn);
            }

            return qn;
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            if (accumulator.AccumulatorName == null)
            {
                PrepareSimpleAttributes();
            }

            accumulator.ImportPrecedence = decl.Precedence;
            if (top.GetAccumulatorManager() == null)
            {
                StyleNodeFactory styleNodeFactory = GetCompilation().GetStyleNodeFactory(true);
                AccumulatorRegistry manager = styleNodeFactory.MakeAccumulatorManager();
                top.SetAccumulatorManager(manager);
                GetCompilation().GetPackageData().AccumulatorRegistry = manager;
            }

            AccumulatorRegistry mgr = top.GetAccumulatorManager();
            Accumulator existing = mgr.GetAccumulator(accumulator.AccumulatorName);
            if (existing != null)
            {
                int existingPrec = existing.ImportPrecedence;
                if (existingPrec == decl.Precedence)
                {
                    CompileError("There are two accumulators with the same name (" + accumulator.AccumulatorName.DisplayName + ") and the same import precedence", "XTSE3350");
                }

                if (existingPrec > decl.Precedence)
                {
                    return;
                }
            }

            mgr.AddAccumulator(accumulator);
        }

        public override void Validate(ComponentDeclaration decl)
        {
            slotManager = GetConfiguration().MakeSlotManager();

            // check the element is at the top level of the stylesheet
            CheckTopLevel("XTSE0010", true);

            // only permitted child is XSLAccumulatorRule, and there must be at least one
            bool foundRule = false;
            foreach (NodeInfo curr in Children())
            {
                if (curr is XSLAccumulatorRule)
                {
                    foundRule = true;
                }
                else
                {
                    CompileError("Only xsl:accumulator-rule is allowed here", "XTSE0010");
                }
            }

            if (!foundRule)
            {
                CompileError("xsl:accumulator must contain at least one xsl:accumulator-rule", "XTSE0010");
            }
        }

        public SlotManager GetSlotManager()
        {
            return slotManager;
        }

        public void Optimize(ComponentDeclaration declaration)
        {
        }

        private void CheckInitialStreamability()
        {
        }

        private void CheckRuleStreamability()
        {
        }
    }
}

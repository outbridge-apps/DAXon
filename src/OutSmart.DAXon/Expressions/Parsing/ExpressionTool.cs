////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Expressions.Parsing
{
    internal sealed class ExpressionTool
    {

        public static string CurrentDirectory
        {
            get
            {
                string dir;
                try
                {
                    dir = Environment.GetEnvironmentVariable("user.dir");
                }
                catch (Exception geterr)
                {

                    // this doesn't work when running an applet
                    return null;
                }

                if (!dir.EndsWith("/", StringComparison.Ordinal))
                {
                    dir = dir + '/';
                }

                URI currentDirectoryURL = new Uri(Path.GetFullPath(dir)).AbsoluteUri;
                return currentDirectoryURL.ToString();
            }
        }

        public static Expression Make(string expression, IStaticContext env, int start, int terminator, ICodeInjector codeInjector)
        {
            XPathParser parser = env.GetConfiguration().NewExpressionParser("XP", false, env);
            if (codeInjector != null)
            {
                parser.CodeInjector = codeInjector;
            }

            if (terminator == -1)
            {
                terminator = Token.EOF;
            }

            Expression exp = parser.Parse(expression, start, terminator, env);

            // TODO: parser.parse() already sets the retained static context
            SetDeepRetainedStaticContext(exp, env.MakeRetainedStaticContext());
            exp = exp.Simplify();
            return exp;
        }

        // Every recursive walk of an expression tree in this class starts here. The parser's operator, predicate and
        // path loops build a left-deep tree one level per ITERATION, so the tree can be far deeper than the parser's own
        // recursion guard saw: a 2000-term chain compiled fine on a 1 MB thread and overflowed a 256 KB one in these walks.
        internal static void ProbeTreeDepth()
        {
            Controller.CheckActiveTimeout();   // the compile's deadline: these walks are where a long chain's quadratic work is
            try
            {
                StackGuard.Probe();
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe("Expression is too deeply nested (insufficient stack on this thread)", "XPST0003", null);
            }
        }

        public static void SetDeepRetainedStaticContext(Expression exp, RetainedStaticContext rsc)
        {
            ProbeTreeDepth();
            if (exp.LocalRetainedStaticContext == null)
            {
                exp.SetRetainedStaticContextLocally(rsc);
            }
            else
            {
                rsc = exp.LocalRetainedStaticContext;
            }

            foreach (Operand o in exp.Operands())
            {
                SetDeepRetainedStaticContext(o.GetChildExpression(), rsc);
            }
        }

        public static void CopyLocationInfo(Expression from, Expression to)
        {
            if (from != null && to != null)
            {
                if (to.GetLocation() == null || to.GetLocation() == Loc.NONE)
                {
                    to.SetLocation(from.GetLocation());
                }

                if (to.LocalRetainedStaticContext == null)
                {
                    to.SetRetainedStaticContextLocally(from.LocalRetainedStaticContext);
                }
            }
        }

        public static Expression UnsortedIfHomogeneous(Expression exp, bool forStreaming)
        {
            if (exp is Literal)
            {
                return exp; // fast exit
            }

            if (exp.GetItemType() is AnyItemType)
            {
                return exp;
            }
            else
            {
                return exp.Unordered(false, forStreaming);
            }
        }

        public static Expression InjectCode(Expression exp, ICodeInjector injector)
        {
            ProbeTreeDepth();
            if (exp is FLWORExpression)
            {
                ((FLWORExpression)exp).InjectCode(injector);
            }
            else if (exp is TraceExpression)
            {
                foreach (Operand o in ((TraceExpression)exp).Child.Operands())
                {
                    if (!o.OperandRole.IsConstrainedClass())
                    {
                        o.SetChildExpression(InjectCode(o.GetChildExpression(), injector));
                    }
                }
            }
            else
            {
                foreach (Operand o in exp.Operands())
                {
                    if (!o.OperandRole.IsConstrainedClass())
                    {
                        o.SetChildExpression(InjectCode(o.GetChildExpression(), injector));
                    }
                }
            }

            return injector.Inject(exp);
        }

        public static IGroundedValue EagerEvaluate(Expression exp, IXPathContext context)
        {
            return exp.MakeElaborator().Eagerly().Evaluate(context).Materialize();
        }

        public static int MarkTailFunctionCalls(Expression exp, StructuredQName qName, int arity)
        {
            return exp.MarkTailFunctionCalls(qName, arity);
        }

        public static bool Contains(Expression a, Expression b)
        {
            Expression temp = b;
            while (temp != null)
            {
                if (temp == a)
                {
                    return true;
                }
                else
                {
                    temp = temp.ParentExpression;
                }
            }

            return false;
        }

        public static bool ContainsLocalVariableReference(Expression exp)
        {
            return Contains(exp, false, (e) =>
            {
                if (e is LocalVariableReference)
                {
                    LocalVariableReference vref = (LocalVariableReference)e;
                    ILocalBinding binding = vref.GetBinding();
                    return !(binding is Expression && Contains(exp, (Expression)binding));
                }

                return false;
            });
        }

        public static bool Contains(Expression exp, bool sameFocusOnly, Func<Expression, bool> predicate)
        {
            ProbeTreeDepth();
            if (predicate(exp))
            {
                return true;
            }

            foreach (Operand info in exp.Operands())
            {
                if ((info.HasSameFocus() || !sameFocusOnly) && Contains(info.GetChildExpression(), sameFocusOnly, predicate))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ChangesXsltContext(Expression exp)
        {
            ProbeTreeDepth();
            if (exp is ResultDocument || exp is CallTemplate || exp is ApplyTemplates || exp is NextMatch || exp is ApplyImports || exp.IsCallOn(typeof(RegexGroup)) || exp.IsCallOn(typeof(CurrentGroup)) || exp is DynamicFunctionCall)
            {
                return true;
            }

            foreach (Operand o in exp.Operands())
            {
                if (ChangesXsltContext(o.GetChildExpression()))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLoopingSubexpression(Expression child, Expression ancestor)
        {
            while (true)
            {
                Expression parent = child.ParentExpression;
                if (parent == null)
                {
                    return false;
                }

                if (HasLoopingSubexpression(parent, child))
                {
                    return true;
                }

                if (parent == ancestor)
                {
                    return false;
                }

                child = parent;
            }
        }

        public static bool IsLoopingReference(VariableReference reference, IBinding binding)
        {
            Expression child = reference;
            Expression parent = child.ParentExpression;
            while (true)
            {
                if (parent == null)
                {

                    // haven't found the binding on the stack, so the safe thing is to assume we're in a loop
                    return true;
                }

                if (parent is FLWORExpression)
                {
                    if (parent.HasVariableBinding(binding))
                    {

                        // The variable is declared in one of the clauses of the FLWOR expression
                        return ((FLWORExpression)parent).HasLoopingVariableReference(binding);
                    }
                    else
                    {

                        // The variable is declared outside the FLWOR expression
                        if (HasLoopingSubexpression(parent, child))
                        {
                            return true;
                        }
                    }
                }
                else if (parent.ExpressionName.Equals("tryCatch"))
                {
                    return true; // not actually a loop, but it's a simple way to prevent inlining of variables (test QT3 try-007)
                }
                else
                {
                    if (parent is ForEachGroup && parent.HasVariableBinding(binding))
                    {
                        return false;
                    }

                    if (HasLoopingSubexpression(parent, child))
                    {
                        return true;
                    }

                    if (parent.HasVariableBinding(binding))
                    {
                        return false;
                    }
                }

                child = parent;
                parent = child.ParentExpression;
            }
        }

        public static bool HasLoopingSubexpression(Expression parent, Expression child)
        {
            foreach (Operand info in parent.Operands())
            {
                if (info.GetChildExpression() == child)
                {
                    return info.IsEvaluatedRepeatedly();
                }
            }

            return false;
        }

        public static Expression GetFocusSettingContainer(Expression exp)
        {
            Expression child = exp;
            Expression parent = child.ParentExpression;
            while (parent != null)
            {
                Operand o = FindOperand(parent, child);
                if (o == null)
                {
                    throw new InvalidOperationException();
                }

                if (!o.HasSameFocus())
                {
                    return parent;
                }

                child = parent;
                parent = child.ParentExpression;
            }

            return null;
        }

        public static Expression GetContextDocumentSettingContainer(Expression exp)
        {
            Expression child = exp;
            Expression parent = child.ParentExpression;
            while (parent != null)
            {
                if (parent is IContextSwitchingExpression)
                {
                    IContextSwitchingExpression switcher = (IContextSwitchingExpression)parent;
                    if (child == switcher.GetActionExpression())
                    {
                        if (switcher.GetSelectExpression().HasSpecialProperty(StaticProperty.CONTEXT_DOCUMENT_NODESET))
                        {
                            parent.ResetLocalStaticProperties();
                            parent.GetSpecialProperties();
                            return GetContextDocumentSettingContainer(parent);
                        }
                    }
                }

                Operand o = FindOperand(parent, child);
                if (o == null)
                {
                    throw new InvalidOperationException();
                }

                if (!o.HasSameFocus())
                {
                    return parent;
                }

                child = parent;
                parent = child.ParentExpression;
            }

            return null;
        }

        public static void ResetStaticProperties(Expression exp)
        {
            int i = 0;
            while (exp != null)
            {
                exp.ResetLocalStaticProperties();
                exp = exp.ParentExpression;
                if (i++ > 100000)
                {
                    throw new InvalidOperationException("Loop in parent expression chain");
                }
            }
        }

        public static int GetAxisNavigation(Expression exp)
        {
            ProbeTreeDepth();
            Expression unfiltered = UnfilteredExpression(exp, true);
            if (unfiltered is AxisExpression)
            {
                return ((AxisExpression)unfiltered).Axis;
            }

            if (unfiltered is VennExpression)
            {
                int v1 = GetAxisNavigation(((VennExpression)unfiltered).GetLhsExpression());
                int v2 = GetAxisNavigation(((VennExpression)unfiltered).GetRhsExpression());
                if (v1 == v2)
                {
                    return v1;
                }
            }

            return -1;
        }

        public static bool EqualOrNull(object x, object y)
        {
            if (x == null)
            {
                return y == null;
            }
            else
            {
                return x.Equals(y);
            }
        }

        public static ISequenceIterator GetIteratorFromProcessMethod(Expression exp, IXPathContext context)
        {
            Controller controller = context.GetController();
            SequenceCollector seq = controller.AllocateSequenceOutputter();
            exp.Process(new ComplexContentOutputter(seq), context);
            seq.Close();
            return seq.Iterate();
        }

        public static int AllocateSlots(Expression exp, int nextFree, SlotManager frame)
        {
            ProbeTreeDepth();
            if (exp is Assignation)
            {
                ((Assignation)exp).SetSlotNumber(nextFree);
                int count = ((Assignation)exp).RequiredSlots;
                nextFree += count;
                if (frame != null)
                {
                    frame.AllocateSlotNumber(((Assignation)exp).GetVariableQName(), (Assignation)exp);
                }
            }

            if (exp is LocalParam && ((LocalParam)exp).SlotNumber < 0)
            {
                ((LocalParam)exp).SlotNumber = nextFree++;
            }

            if (exp is FLWORExpression)
            {
                foreach (Clause c in ((FLWORExpression)exp).ClauseList)
                {
                    foreach (LocalVariableBinding b in c.RangeVariables)
                    {
                        b.SetSlotNumber(nextFree++);
                        frame.AllocateSlotNumber(b.GetVariableQName(), b);
                    }
                }
            }

            if (exp is VariableReference)
            {
                VariableReference var = (VariableReference)exp;
                IBinding binding = var.GetBinding();
                if (exp is LocalVariableReference)
                {
                    ((LocalVariableReference)var).SlotNumber = ((ILocalBinding)binding).LocalSlotNumber;
                }

                if (binding is Assignation && ((ILocalBinding)binding).LocalSlotNumber < 0)
                {

                    // This indicates something badly wrong: we've found a variable reference on the tree, that's
                    // bound to a variable declaration that is no longer on the tree. All we can do is print diagnostics.
                    // The most common reason for this failure is that the declaration of the variable was removed
                    // from the tree in the mistaken belief that there were no references to the variable. Variable
                    // references are counted during the typeCheck phase, so this can happen if typeCheck() fails to
                    // visit some branch of the expression tree.
                    Assignation decl = (Assignation)binding;
                    Logger err;
                    try
                    {
                        err = exp.GetConfiguration().Logger;
                    }
                    catch (Exception ex)
                    {
                        err = new StandardLogger();
                    }

                    string msg = "*** Internal Saxon error: local variable encountered whose binding has been deleted";
                    err.Error(msg);
                    err.Error("Variable name: " + decl.VariableName);
                    err.Error("Line number of reference: " + var.GetLocation().GetLineNumber() + " in " + var.GetLocation().GetSystemId());
                    err.Error("Line number of declaration: " + decl.GetLocation().GetLineNumber() + " in " + decl.GetLocation().GetSystemId());
                    err.Error("DECLARATION:");
                    try
                    {
                        decl.Explain(err);
                    }
                    catch (Exception e)
                    {
                    }

                    throw new InvalidOperationException(msg);
                }
            }

            if (exp is Patterns.Pattern)
            {
                nextFree = ((Patterns.Pattern)exp).AllocateSlots(frame, nextFree);
            }
            else if (exp is IScopedBindingElement)
            {
                nextFree = ((IScopedBindingElement)exp).AllocateSlots(frame, nextFree);
            }
            else
            {
                foreach (Operand o in exp.Operands())
                {
                    nextFree = AllocateSlots(o.GetChildExpression(), nextFree, frame);
                }
            }

            return nextFree; // Note, we allocate a distinct slot to each range variable, even if the
            // scopes don't overlap. This isn't strictly necessary, but might help
            // debugging.
        }

        public static bool EffectiveBooleanValue(ISequenceIterator iterator)
        {
            IItem first = iterator.Next();
            if (first == null)
            {
                return false;
            }

            Genre genre = first.GetGenre(); // Variable introduced for C# type checking
            switch (genre)
            {
                case Genre.NODE:
                    iterator.Dispose();
                    return true;
                case Genre.ATOMIC:
                    {
                        if (first is BooleanValue)
                        {
                            if (iterator.Next() != null)
                            {
                                iterator.Dispose();
                                EbvError("a sequence of two or more items starting with a boolean");
                            }

                            iterator.Dispose();
                            return ((BooleanValue)first).GetBooleanValue();
                        } // includes anyURI value
                        else if (first is StringValue)
                        {

                            // includes anyURI value
                            if (iterator.Next() != null)
                            {
                                iterator.Dispose();
                                EbvError("a sequence of two or more items starting with a string ('" + first.GetStringValue() + "')");
                            }

                            return !((StringValue)first).IsEmpty();
                        }
                        else if (first is NumericValue)
                        {
                            if (iterator.Next() != null)
                            {
                                iterator.Dispose();
                                EbvError("a sequence of two or more items starting with a numeric value (" + first.GetStringValue() + ")");
                            }

                            NumericValue n = (NumericValue)first;
                            return (n.CompareTo(0) != 0) && !n.IsNaN();
                        }
                        else
                        {
                            iterator.Dispose();
                            EbvError("a sequence starting with an atomic value of type " + ((AtomicValue)first).GetItemType().Description);
                            return false;
                        }
                    }

                case Genre.ARRAY:
                    iterator.Dispose();
                    EbvError("a sequence starting with an array item (" + first.ToShortString() + ")");
                    return false;
                case Genre.MAP:
                    iterator.Dispose();
                    EbvError("a sequence starting with a map (" + first.ToShortString() + ")");
                    return false;
                case Genre.FUNCTION:
                    {
                        iterator.Dispose();
                        EbvError("a sequence starting with a function (" + first.ToShortString() + ")");
                        return false;
                    }

                case Genre.EXTERNAL:
                    {
                        if (iterator.Next() != null)
                        {
                            iterator.Dispose();
                            EbvError("a sequence of two or more items starting with an external object value");
                        }

                        return true;
                    }
            }

            EbvError("a sequence starting with an item of unknown kind");
            return false;
        }

        public static bool EffectiveBooleanValue(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is NodeInfo)
            {
                return true;
            }
            else if (item is AtomicValue)
            {
                if (item is BooleanValue)
                {
                    return ((BooleanValue)item).GetBooleanValue();
                } // includes anyURI value
                else if (item is StringValue)
                {

                    // includes anyURI value
                    return !((StringValue)item).IsEmpty();
                }
                else if (item is NumericValue)
                {
                    NumericValue n = (NumericValue)item;
                    return (n.CompareTo(0) != 0) && !n.IsNaN();
                }
                else if (item.GetGenre() == Genre.EXTERNAL)
                {
                    return true;
                }
                else
                {
                    EbvError("an atomic value of type " + ((AtomicValue)item).PrimitiveType.DisplayName);
                    return false;
                }
            }
            else
            {
                EbvError(item.GetGenre().ToString());
                return false;
            }
        }

        public static void EbvError(string reason)
        {
            throw new XPathException("Effective boolean value is not defined for " + reason).WithErrorCode("FORG0006").AsTypeError();
        }

        public static void EbvError(string reason, Expression cause)
        {
            throw new XPathException("Effective boolean value is not defined for " + reason).WithErrorCode("FORG0006").AsTypeError().WithFailingExpression(cause);
        }

        public static bool DependsOnFocus(Expression exp)
        {
            return (exp.Dependencies & StaticProperty.DEPENDS_ON_FOCUS) != 0;
        }

        public static bool DependsOnVariable(Expression exp, IBinding[] bindingList)
        {
            return !(bindingList == null || bindingList.Length == 0) && Contains(exp, false, (e) =>
            {
                if (e is VariableReference)
                {
                    foreach (IBinding binding in bindingList)
                    {
                        if (((VariableReference)e).GetBinding() == binding)
                        {
                            return true;
                        }
                    }
                }

                return false;
            });
        }

        public static void GatherReferencedVariables(Expression e, IList<IBinding> list)
        {
            ProbeTreeDepth();
            if (e is VariableReference)
            {
                IBinding binding = ((VariableReference)e).GetBinding();
                if (!list.Contains(binding))
                {
                    list.Add(binding);
                }
            }
            else
            {
                foreach (Operand o in e.Operands())
                {
                    if (!o.OperandRole.IsInChoiceGroup())
                    {
                        GatherReferencedVariables(o.GetChildExpression(), list);
                    }
                }
            }
        }

        public static bool RefersToVariableOrFunction(Expression exp)
        {
            return Contains(exp, false, (e) => e is VariableReference || e is UserFunctionCall || e is IBinding || e is CallTemplate || e is ApplyTemplates || e is ApplyImports || IsCallOnSystemFunction(e, "function-lookup") || e.IsCallOn(typeof(ApplyFn)));
        }

        public static bool IsCallOnSystemFunction(Expression e, string localName)
        {
            return e is StaticFunctionCall && localName.Equals(((StaticFunctionCall)e).GetFunctionName().GetLocalPart());
        }

        public static bool CallsFunction(Expression exp, StructuredQName qName, bool sameFocusOnly)
        {
            return Contains(exp, sameFocusOnly, (e) => e is FunctionCall && qName.Equals(((FunctionCall)e).GetFunctionName()));
        }

        public static void GatherCalledFunctions(Expression e, IList<UserFunction> list)
        {
            ProbeTreeDepth();
            if (e is UserFunctionCall)
            {
                UserFunction function = ((UserFunctionCall)e).GetFunction();
                if (!list.Contains(function))
                {
                    list.Add(function);
                }
            }
            else if (e is UserFunctionReference)
            {
                UserFunction function = ((UserFunctionReference)e).NominalTarget;
                if (!list.Contains(function))
                {
                    list.Add(function);
                }
            }
            else
            {
                foreach (Operand o in e.Operands())
                {
                    GatherCalledFunctions(o.GetChildExpression(), list);
                }
            }
        }

        public static void GatherCalledFunctionNames(Expression e, IList<SymbolicName> list)
        {
            ProbeTreeDepth();
            if (e is UserFunctionCall)
            {
                list.Add(((UserFunctionCall)e).GetSymbolicName());
            }
            else
            {
                foreach (Operand o in e.Operands())
                {
                    GatherCalledFunctionNames(o.GetChildExpression(), list);
                }
            }
        }

        public static Expression OptimizeComponentBody(Expression body, Compilation compilation, ExpressionVisitor visitor, ContextItemStaticInfo cisi, bool extractGlobals)
        {
            Optimizer opt = visitor.ObtainOptimizer();
            if (opt.IsOptionSet(OptimizerOptions.MISCELLANEOUS))
            {
                ExpressionTool.ResetPropertiesWithinSubtree(body);
                if (opt.IsOptionSet(OptimizerOptions.MISCELLANEOUS))
                {
                    body = body.Optimize(visitor, cisi);
                }

                body.ParentExpression = null;
                if (extractGlobals && compilation != null)
                {
                    Expression exp2 = opt.PromoteExpressionsToGlobal(body, compilation.GetPrincipalStylesheetModule(), visitor);
                    if (exp2 != null)
                    {

                        // Try another optimization pass: extracting global variables can identify things that are indexable
                        ExpressionTool.ResetPropertiesWithinSubtree(exp2);
                        body = exp2.Optimize(visitor, cisi);
                    }
                }

                if (opt.IsOptionSet(OptimizerOptions.LOOP_LIFTING))
                {
                    body = LoopLifter.Process(body, visitor, cisi);
                }
            }
            else
            {
                body = AvoidDocumentSort(body);
            }

            if (!visitor.IsOptimizeForStreaming())
            {
                body = opt.EliminateCommonSubexpressions(body);
            }

            opt.PrepareForStreaming(body);

            body.RestoreParentPointers();
            return body;
        }

        private static Expression AvoidDocumentSort(Expression exp)
        {
            ProbeTreeDepth();
            if (exp is DocumentSorter)
            {
                Expression @base = ((DocumentSorter)exp).BaseExpression;
                if (@base.HasSpecialProperty(StaticProperty.ORDERED_NODESET))
                {
                    return @base;
                }

                return exp;
            }
            else if (exp is ConditionalSorter)
            {
                DocumentSorter sorter = ((ConditionalSorter)exp).DocumentSorter;
                Expression eliminatedSorter = AvoidDocumentSort(sorter);
                if (eliminatedSorter != sorter)
                {
                    return eliminatedSorter;
                }
            }

            foreach (Operand o in exp.Operands())
            {
                o.SetChildExpression(AvoidDocumentSort(o.GetChildExpression()));
            }

            return exp;
        }

        public static void ComputeEvaluationModesForUserFunctionCalls(Expression exp)
        {
            ExpressionTool.ProcessExpressionTree(exp, null, (expression, result) =>
            {
                if (expression is UserFunctionCall)
                {
                    ((UserFunctionCall)expression).AllocateArgumentEvaluators();
                }

                if (expression is LocalParam)
                {
                    ((LocalParam)expression).ComputeEvaluationMode();
                }

                return false;
            });
        }

        public static void ResetPropertiesWithinSubtree(Expression exp)
        {
            ProbeTreeDepth();
            exp.ResetLocalStaticProperties();
            if (exp is LocalVariableReference)
            {
                LocalVariableReference @ref = (LocalVariableReference)exp;
                IBinding binding = @ref.GetBinding();
                if (binding is Assignation)
                {
                    binding.AddReference(@ref, @ref.IsInLoop());
                }
            }

            foreach (Operand o in exp.Operands())
            {
                ResetPropertiesWithinSubtree(o.GetChildExpression());
                o.GetChildExpression().ParentExpression = exp;
            }
        }

        public static Expression ResolveCallsToCurrentFunction(Expression exp)
        {
            if (exp.IsCallOn(typeof(Current)))
            {
                ContextItemExpression cie = new ContextItemExpression();
                CopyLocationInfo(exp, cie);
                return cie;
            }
            else
            {
                if (CallsFunction(exp, Current.FN_CURRENT, true))
                {

                    // replace trivial (same-focus) calls to current by a simple "."
                    ReplaceTrivialCallsToCurrent(exp);
                }

                if (CallsFunction(exp, Current.FN_CURRENT, false))
                {

                    // replace non-trivial (different-focus) calls to current by a variable reference
                    LetExpression let = new LetExpression();
                    let.SetVariableQName(new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "current" + exp.GetHashCode()));
                    let.SetRequiredType(SequenceType.SINGLE_ITEM);
                    let.Sequence = new CurrentItemExpression();
                    ReplaceCallsToCurrent(exp, let);
                    let.SetAction(exp);
                    return let;
                }
                else
                {
                    return exp;
                }
            }
        }

        public static void GatherVariableReferences(Expression exp, IBinding binding, IList<VariableReference> list)
        {
            ProbeTreeDepth();
            if (exp is VariableReference && ((VariableReference)exp).GetBinding() == binding)
            {
                list.Add((VariableReference)exp);
            }
            else
            {
                foreach (Operand o in exp.Operands())
                {
                    GatherVariableReferences(o.GetChildExpression(), binding, list);
                }
            }
        }

        public static bool ProcessExpressionTree(Expression root, object result, IExpressionAction action)
        {
            ProbeTreeDepth();
            bool done = action.Process(root, result);
            if (!done)
            {
                foreach (Operand o in root.Operands())
                {
                    done = ProcessExpressionTree(o.GetChildExpression(), result, action);
                    if (done)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ReplaceSelectedSubexpressions(Expression exp, Func<Expression, bool> selector, Expression replacement, bool mustCopy)
        {
            ProbeTreeDepth();
            bool replaced = false;
            foreach (Operand o in exp.Operands())
            {
                if (replaced)
                {
                    mustCopy = true;
                }

                Expression child = o.GetChildExpression();
                if (selector(child))
                {
                    Expression e2 = mustCopy ? replacement.Copy(new RebindingMap()) : replacement;
                    o.SetChildExpression(e2);
                    replaced = true;
                }
                else
                {
                    replaced |= ReplaceSelectedSubexpressions(child, selector, replacement, mustCopy);
                }
            }

            return replaced;
        }

        public static void ReplaceVariableReferences(Expression exp, IBinding binding, Expression replacement, bool mustCopy)
        {
            Func<Expression, bool> selector = (child) => child is VariableReference && ((VariableReference)child).GetBinding() == binding;
            bool changed = ReplaceSelectedSubexpressions(exp, selector, replacement, mustCopy);
            if (changed)
            {
                ResetPropertiesWithinSubtree(exp);
            }
        }

        public static int GetReferenceCount(Expression exp, IBinding binding, bool inLoop)
        {
            ProbeTreeDepth();
            int rcount = 0;
            if (exp is VariableReference && ((VariableReference)exp).GetBinding() == binding)
            {
                if (((VariableReference)exp).IsFiltered())
                {
                    return FilterExpression.FILTERED;
                }
                else
                {
                    rcount += inLoop ? 10 : 1;
                }
            }
            else if ((exp.Dependencies & StaticProperty.DEPENDS_ON_LOCAL_VARIABLES) == 0)
            {
                return 0;
            }
            else
            {
                foreach (Operand info in exp.Operands())
                {
                    Expression child = info.GetChildExpression();
                    bool childLoop = inLoop || info.IsEvaluatedRepeatedly();
                    rcount += GetReferenceCount(child, binding, childLoop);
                    if (rcount >= FilterExpression.FILTERED)
                    {
                        break;
                    }
                }
            }

            return rcount;
        }

        public static void RebindVariableReferences(Expression exp, IBinding oldBinding, IBinding newBinding)
        {
            ProbeTreeDepth();
            if (exp is VariableReference)
            {
                if (((VariableReference)exp).GetBinding() == oldBinding)
                {
                    ((VariableReference)exp).Fixup(newBinding);
                }
            }
            else
            {
                foreach (Operand o in exp.Operands())
                {
                    RebindVariableReferences(o.GetChildExpression(), oldBinding, newBinding);
                }
            }
        }

        public static Expression MakePathExpression(Expression start, Expression step)
        {

            // the expression /.. is sometimes used to represent the empty node-set. Applying this simplification
            // now avoids generating warnings for this case.
            if (start is RootExpression && step is AxisExpression && ((AxisExpression)step).Axis == AxisInfo.PARENT)
            {
                return Literal.MakeEmptySequence();
            }

            SlashExpression expr = new SlashExpression(start, step);

            // If start is a path expression such as a, and step is b/c, then
            // instead of a/(b/c) we construct (a/b)/c. This is because it often avoids
            // a sort.
            // The "/" operator in XPath 2.0 is not always left-associative. Problems
            // can occur if position() and last() are used on the rhs, or if node-constructors
            // appear, e.g. //b/../<d/>. So we only do this rewrite if the step is a path
            // expression in which both operands are axis expressions optionally with predicates
            if (step is SlashExpression)
            {
                SlashExpression stepPath = (SlashExpression)step;
                if (IsFilteredAxisPath(stepPath.GetSelectExpression()) && IsFilteredAxisPath(stepPath.GetActionExpression()))
                {
                    expr.Start = ExpressionTool.MakePathExpression(start, stepPath.GetSelectExpression());
                    expr.SetStep(stepPath.GetActionExpression());
                }
            }

            return expr;
        }

        public static Operand FindOperand(Expression parentExpression, Expression childExpression)
        {
            foreach (Operand o in parentExpression.Operands())
            {
                if (o.GetChildExpression() == childExpression)
                {
                    return o;
                }
            }

            return null;
        }

        private static bool IsFilteredAxisPath(Expression exp)
        {
            return UnfilteredExpression(exp, true) is AxisExpression;
        }

        public static Expression UnfilteredExpression(Expression exp, bool allowPositional)
        {
            ProbeTreeDepth();
            if (exp is FilterExpression && (allowPositional || !((FilterExpression)exp).IsFilterIsPositional()))
            {
                return UnfilteredExpression(((FilterExpression)exp).GetSelectExpression(), allowPositional);
            }
            else if (exp is TailExpression && allowPositional)
            {
                return UnfilteredExpression(((UnaryExpression)exp).BaseExpression, allowPositional);
            }
            else if (exp is SingleItemFilter && allowPositional)
            {
                return UnfilteredExpression(((SingleItemFilter)exp).BaseExpression, allowPositional);
            }
            else
            {
                return exp;
            }
        }

        public static Expression TryToFactorOutDot(Expression exp, ItemType contextItemType)
        {
            if (exp is ContextItemExpression)
            {
                return null;
            }
            else if (exp is LetExpression && ((LetExpression)exp).Sequence is ContextItemExpression)
            {
                Expression action = ((LetExpression)exp).GetAction();
                bool changed = FactorOutDot(action, (LetExpression)exp);
                if (changed)
                {
                    exp.ResetLocalStaticProperties();
                }

                return exp;
            }
            else if ((exp.Dependencies & (StaticProperty.DEPENDS_ON_CONTEXT_ITEM | StaticProperty.DEPENDS_ON_CONTEXT_DOCUMENT)) != 0)
            {
                LetExpression let = new LetExpression();
                let.SetVariableQName(new StructuredQName("saxon", NamespaceUri.SAXON, "dot" + exp.GetHashCode()));
                let.SetRequiredType(SequenceType.MakeSequenceType(contextItemType, StaticProperty.EXACTLY_ONE));
                let.Sequence = new ContextItemExpression();
                Expression actionCopy = exp.Copy(new RebindingMap());
                let.SetAction(actionCopy);
                bool changed = FactorOutDot(actionCopy, let);
                if (changed)
                {
                    return let;
                }
                else
                {
                    return exp;
                }
            }
            else
            {
                return null;
            }
        }

        public static bool FactorOutDot(Expression exp, IBinding variable)
        {
            ProbeTreeDepth();
            bool changed = false;
            if ((exp.Dependencies & (StaticProperty.DEPENDS_ON_CONTEXT_ITEM | StaticProperty.DEPENDS_ON_CONTEXT_DOCUMENT)) != 0)
            {
                foreach (Operand info in exp.Operands())
                {
                    if (info.HasSameFocus())
                    {
                        Expression child = info.GetChildExpression();
                        if (child is ContextItemExpression)
                        {
                            VariableReference @ref = MakeReference(variable);
                            CopyLocationInfo(child, @ref);
                            info.SetChildExpression(@ref);
                            changed = true;
                        }
                        else if (child is AxisExpression || child is RootExpression)
                        {
                            VariableReference @ref = MakeReference(variable);
                            CopyLocationInfo(child, @ref);
                            Expression path = ExpressionTool.MakePathExpression(@ref, child);
                            info.SetChildExpression(path);
                            changed = true;
                        }
                        else
                        {
                            changed |= FactorOutDot(child, variable);
                        }
                    }
                }
            }

            if (changed)
            {
                exp.ResetLocalStaticProperties();
            }

            return changed;
        }

        private static VariableReference MakeReference(IBinding variable)
        {
            if (variable.IsGlobal())
            {
                return new GlobalVariableReference((GlobalVariable)variable);
            }
            else
            {
                return new LocalVariableReference((ILocalBinding)variable);
            }
        }

        public static bool InlineVariableReferences(Expression expr, IBinding binding, Expression replacement)
        {
            return InlineVariableReferencesInternal(expr, binding, replacement);
        }

        public static bool InlineVariableReferencesInternal(Expression expr, IBinding binding, Expression replacement)
        {
            ProbeTreeDepth();
            if (expr is TryCatch && !(replacement is Literal))
            {

                // Don't inline variable references within a try/catch, as this will lead to errors in the
                // variable's initializer being incorrectly caught by the catch clause. See XSLT3 test try-029.
                return false;
            }
            else
            {
                bool found = false;
                foreach (Operand o in expr.Operands())
                {
                    Expression child = o.GetChildExpression();
                    if (child is VariableReference && ((VariableReference)child).GetBinding() == binding)
                    {
                        Expression copy;
                        try
                        {
                            copy = replacement.Copy(new RebindingMap());
                            ExpressionTool.CopyLocationInfo(child, copy);
                        }
                        catch (NotSupportedException err)
                        {

                            // If we can't make a copy, return the original. This is safer than it seems,
                            // because on the paths where this happens, we are merely moving the expression from
                            // one place to another, not replicating it
                            copy = replacement;
                        }

                        o.SetChildExpression(copy);
                        found = true;
                    }
                    else
                    {
                        found |= InlineVariableReferencesInternal(child, binding, replacement);
                    }
                }

                if (found)
                {
                    expr.ResetLocalStaticProperties();
                }

                return found;
            }
        }

        public static bool ReplaceTrivialCallsToCurrent(Expression expr)
        {
            ProbeTreeDepth();
            bool found = false;
            foreach (Operand o in expr.Operands())
            {
                if (o.HasSameFocus())
                {
                    Expression child = o.GetChildExpression();
                    if (child.IsCallOn(typeof(Current)))
                    {
                        CurrentItemExpression var = new CurrentItemExpression();
                        ExpressionTool.CopyLocationInfo(child, var);
                        o.SetChildExpression(var);
                        found = true;
                    }
                    else
                    {
                        found = ReplaceTrivialCallsToCurrent(child);
                    }
                }
            }

            if (found)
            {
                expr.ResetLocalStaticProperties();
            }

            return found;
        }

        public static bool ReplaceCallsToCurrent(Expression expr, ILocalBinding binding)
        {
            ProbeTreeDepth();
            bool found = false;
            foreach (Operand o in expr.Operands())
            {
                Expression child = o.GetChildExpression();
                if (child.IsCallOn(typeof(Current)))
                {
                    LocalVariableReference var = new LocalVariableReference(binding);
                    ExpressionTool.CopyLocationInfo(child, var);
                    o.SetChildExpression(var);
                    binding.AddReference(var, true);
                    found = true;
                }
                else
                {
                    found = ReplaceCallsToCurrent(child, binding);
                }
            }

            if (found)
            {
                expr.ResetLocalStaticProperties();
            }

            return found;
        }

        public static bool IsNotAllowedInUpdatingContext(Expression exp)
        {
            return !exp.IsUpdatingExpression() && !exp.IsVacuousExpression();
        }

        public static URI GetBaseURI(IStaticContext env, OutSmart.DAXon.Api.ILocation locator, bool fail)
        {
            URI expressionBaseURI = null;
            string @base = null;
            try
            {
                @base = env.StaticBaseURI;
                if (@base == null)
                {
                    @base = CurrentDirectory;
                }

                if (@base != null && !(@base.Length == 0))
                {
                    expressionBaseURI = new URI(@base);
                }
            }
            catch (URISyntaxException e)
            {

                // perhaps escaping special characters will fix the problem
                string esc = IriToUri.IriToUriFn(StringView.Tidy(@base)).ToString();
                try
                {
                    expressionBaseURI = new URI(esc);
                }
                catch (URISyntaxException e2)
                {

                    // don't fail unless the base URI is actually needed (it usually isn't)
                    expressionBaseURI = null;
                }

                if (expressionBaseURI == null && fail)
                {
                    XPathException err = new XPathException("The base URI " + Err.Wrap(env.StaticBaseURI, Err.URI) + " is not a valid URI");
                    err.SetLocator(locator);
                    throw err;
                }
            }

            return expressionBaseURI;
        }

        public static string Parenthesize(Expression exp)
        {
            if (exp.Operands().Any())
            {
                return "(" + exp.ToString() + ")";
            }
            else
            {
                return exp.ToString();
            }
        }

        public static string ParenthesizeShort(Expression exp)
        {
            if (HasTwoOrMoreOperands(exp))
            {
                return "(" + exp.ToShortString() + ")";
            }
            else
            {
                return exp.ToShortString();
            }
        }

        private static bool HasTwoOrMoreOperands(Expression exp)
        {
            return exp.Operands().Skip(1).Any();
        }

        public static bool IsLocalConstructor(Expression child)
        {
            if (!(child is ParentNodeConstructor || child is SimpleNodeConstructor))
            {
                return false;
            }

            Expression parent = child.ParentExpression;
            while (parent != null)
            {
                if (parent is ParentNodeConstructor)
                {
                    return true;
                }

                Operand o = FindOperand(parent, child);
                if (o.Usage != OperandUsage.TRANSMISSION)
                {
                    return false;
                }

                child = parent;
                parent = parent.ParentExpression;
            }

            return false;
        }
    }
}
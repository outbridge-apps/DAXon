////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Core;
using System.IO;
namespace OutSmart.DAXon.Functions.Registry
{
    /// <summary>
    /// This class is used to contain information about a set of built-in functions.
    /// </summary>
    public abstract class BuiltInFunctionSet : IFunctionLibrary
    {
        public const int ONE = StaticProperty.ALLOWS_ONE;
        public const int OPT = StaticProperty.ALLOWS_ZERO_OR_ONE;
        public const int STAR = StaticProperty.ALLOWS_ZERO_OR_MORE;
        public const int PLUS = StaticProperty.ALLOWS_ONE_OR_MORE;
        public const int AS_ARG0 = 1; // Result has same type as first argument
        public const int AS_PRIM_ARG0 = 2; // Result has same primitive type as first argument
        public const int CITEM = 4; // Depends on context item
        public const int BASE = 8; // Depends on base URI
        public const int NS = 16; // Depends on namespace context
        public const int DCOLL = 32; // Depends on default collation
        public const int DLANG = 64; // Depends on default language
        public const int FILTER = 256; // Result is a subset of the value of the first arg
        public const int LATE = 512; // Disallow compile-time evaluation
        public const int UO = 1024; // Ordering in first argument is irrelevant
        public const int POSN = 1024 * 2; // Depends on position
        public const int LAST = 1024 * 4; // Depends on last
        public const int SIDE = 1024 * 8; // Has side-effects
        public const int CDOC = 1024 * 16; // Depends on context document
        public const int CARD0 = 1024 * 32; // Result is empty only if first arg is empty
        public const int NEW = 1024 * 64; // All nodes in the result are newly created
        public const int CTRL = 1024 * 128; // Controls the optimization of its arguments
        public const int SEQV = 1024 * 256; // ISequence-variadic, like concat() in 4.0
        public const int DEPENDS_ON_STATIC_CONTEXT = BASE | NS | DCOLL;
        public const int FOCUS = CITEM | POSN | LAST | CDOC;
        protected const int INS = 1 << 24; // = usage INSPECTION
        protected const int ABS = 1 << 25; // = usage ABSORPTION (implicit when type is atomic)
        protected const int TRA = 1 << 26; // = usage TRANSMISSION (node is included in function result)
        protected const int NAV = 1 << 27; // = usage NAVIGATION (function navigates from this node)
        public static ISequence EMPTY = EmptySequence.GetInstance();

        private readonly Dictionary<string, Entry> functionTable = new Dictionary<string, Entry>(200);
        private readonly Dictionary<string, int> sequenceVariadicFunctions = new Dictionary<string, int>(10);

        public virtual string ConventionalPrefix => "fn";
        protected static RecordTest.Field Field(string name, SequenceType type, bool optional)
        {
            return new RecordTest.Field(name, type, optional);
        }
        public void ImportFunctionSet(BuiltInFunctionSet importee)
        {
            if (!importee.GetNamespace().Equals(GetNamespace()))
            {
                throw new ArgumentException(importee.GetNamespace().ToString());
            }

            functionTable.PutAll(importee.functionTable);
            sequenceVariadicFunctions.PutAll(importee.sequenceVariadicFunctions); //importedFunctions.add(importee);
        }

        public virtual Entry GetFunctionDetails(string name, int arity)
        {
            if (arity == -1)
            {
                for (int i = 0; i < 20; i++)
                {
                    Entry found = GetFunctionDetails(name, i);
                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;
            }

            string key = name + "#" + arity;
            Entry entry = functionTable.GetOrDefault(key);
            if (entry != null)
            {
                return entry;
            }


            // Try for a generalised (XP40) sequence-variadic function
            int minArity = sequenceVariadicFunctions.GetOrDefault(name, -1);
            if (minArity != -1 && arity >= minArity)
            {
                key = name + "#" + (minArity + 1);
                return functionTable.GetOrDefault(key);
            }


            //        // Try for a variable-arity function (concat only up to 3.1)
            //        if (name.equals("concat") && arity >= 2 && getNamespace().equals(NamespaceConstant.FN)) {
            //            key = "concat#-1";
            //            entry = functionTable.get(key);
            return null;
        }

        public virtual Expression Bind(SymbolicName.F symbolicName, Expression[] staticArgs, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            StructuredQName functionName = symbolicName.ComponentName;
            int arity = symbolicName.GetArity();
            string localName = functionName.GetLocalPart();
            Entry entry = GetFunctionDetails(localName, arity);
            if (functionName.HasURI(GetNamespace()) && entry != null)
            {
                entry.EnsurePopulated();
                if ((entry.properties & SEQV) != 0)
                {

                    // sequence-variadic functions in 4.0 (e.g..concat, codepoints-to-string)
                    // combine the "variable" arguments into a single argument
                    if (env.GetXPathVersion() < 40)
                    {

                        // Need to special-case the fn:concat() function prior to XPath 4.0
                        if (localName.Equals("concat"))
                        {
                            if (staticArgs.Length < 2)
                            {
                                reasons.Add("concat() prior to XPath 4.0 requires at least two arguments");
                                return null;
                            } //                        // Require each argument to be a singleton
                            //                        Expression[] a2 = new Expression[staticArgs.length];
                            //                        for (int i=0; i<staticArgs.length; i++) {
                            //                            if (staticArgs[i] instanceof StringLiteral) {
                            //                                a2[i] = staticArgs[i];
                            //                            } else {
                            //                                final int pos = i;
                            //                                Func<RoleDiagnostic> role =
                            //                                        () -> new RoleDiagnostic(RoleDiagnostic.FUNCTION, "concat", pos);
                            //                                a2[i] = CardinalityChecker.makeCardinalityChecker(
                            //                                        staticArgs[i], StaticProperty.ALLOWS_ZERO_OR_ONE, role);
                            //                            }
                            //                        }
                            //                        staticArgs = a2;
                        }
                    } //                int declaredArity = entry.maxArity;
                    //                Expression[] newArgs = Arrays.copyOf(staticArgs, declaredArity);
                    //                if (declaredArity > staticArgs.length) {
                    //                } else if (declaredArity < staticArgs.length) {
                    //                    Expression block = new Block(Arrays.copyOfRange(staticArgs, declaredArity - 1, staticArgs.length));
                    //                    ExpressionTool.copyLocationInfo(staticArgs[0], block);
                    //                    newArgs[declaredArity - 1] = block;
                    //                }
                    //                staticArgs = newArgs;
                }
                else if ((keywords != null && keywords.Count > 0) || staticArgs.Length < entry.maxArity)
                {
                    staticArgs = UserFunction.MakeExpandedArgumentArray(staticArgs, keywords, entry);
                }

                RetainedStaticContext rsc = new RetainedStaticContext(env);
                try
                {
                    SystemFunction fn = MakeFunction(localName, staticArgs.Length);
                    fn.SetRetainedStaticContext(rsc);
                    Expression f = fn.MakeFunctionCall(staticArgs);
                    f.SetRetainedStaticContext(rsc);
                    return f;
                }
                catch (XPathException e)
                {
                    reasons.Add(e.Message);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public virtual SystemFunction MakeFunction(string name, int arity)
        {
            Entry entry = GetFunctionDetails(name, arity);
            if (entry == null)
            {
                string diagName = GetNamespace().Equals(NamespaceUri.FN) ? "System function " + name : "Function Q{" + GetNamespace() + "}" + name;
                if (GetFunctionDetails(name, -1) == null)
                {
                    throw new XPathException(diagName + "() does not exist or is not available in this environment").WithErrorCode("XPST0017").AsStaticError();
                }
                else
                {
                    throw new XPathException(diagName + "() cannot be called with " + PluralArguments(arity)).WithErrorCode("XPST0017").AsStaticError();
                }
            }

            entry.EnsurePopulated();
            SystemFunction f = entry.implementationFactory();
            f.Details = entry;
            f.SetArity(arity);
            return f;
        }

        private static string PluralArguments(int num)
        {
            if (num == 0)
            {
                return "zero arguments";
            }

            if (num == 1)
            {
                return "one argument";
            }

            return num + " arguments";
        }

        public virtual bool IsAvailable(SymbolicName.F symbolicName, int languageLevel)
        {
            StructuredQName qn = symbolicName.ComponentName;
            if (!qn.HasURI(GetNamespace()))
            {
                return false;
            }

            Entry entry = GetFunctionDetails(qn.GetLocalPart(), symbolicName.GetArity());
            if (entry == null)
            {
                return false;
            }


            //if ((entry.properties & SEQV) != 0) {
            // sequence-variadic functions in 4.0 (e.g..concat, codepoints-to-string)
            // combine the "variable" arguments into a single argument
            if (languageLevel < 40 && symbolicName.ComponentName.GetLocalPart().Equals("concat") && symbolicName.GetArity() < 2)
            {
                return false;
            }


            //}
            return true;
        }

        //}
        public virtual IFunctionLibrary Copy()
        {
            return this;
        }

        public virtual IFunctionItem GetFunctionItem(SymbolicName.F symbolicName, IStaticContext staticContext)
        {
            StructuredQName functionName = symbolicName.ComponentName;
            int arity = symbolicName.GetArity();
            if (functionName.HasURI(GetNamespace()) && GetFunctionDetails(functionName.GetLocalPart(), arity) != null)
            {
                RetainedStaticContext rsc = staticContext.MakeRetainedStaticContext();
                SystemFunction fn = MakeFunction(functionName.GetLocalPart(), arity);
                if (staticContext.GetXPathVersion() < 40 && fn is Concat31 && arity < 2)
                {

                    // Treat concat() specially prior to 4.0
                    return null;
                }

                fn.SetRetainedStaticContext(rsc);
                return fn;
            }
            else
            {
                return null;
            }
        }

        protected virtual Entry Register(string name, int arity, Func<Entry, Entry> populator)
        {
            Entry e = new Entry();
            e.name = new StructuredQName(ConventionalPrefix, GetNamespace(), name);
            e.minArity = arity;
            e.maxArity = arity;
            e.populator = populator;
            e.functionSet = this;
            functionTable[name + "#" + arity] = e;
            return e;
        }

        protected virtual Entry Register(string name, int minArity, int maxArity, Func<Entry, Entry> populator)
        {
            Entry e = new Entry();
            e.name = new StructuredQName(ConventionalPrefix, GetNamespace(), name);
            e.minArity = minArity;
            e.maxArity = maxArity;
            e.populator = populator;
            e.functionSet = this;
            for (int a = minArity; a <= maxArity; a++)
            {
                functionTable[name + "#" + a] = e;
            }

            return e;
        }

        protected virtual Entry RegisterVariadic(string name, int arity, Func<Entry, Entry> populator)
        {
            Entry e = Register(name, arity, populator);
            sequenceVariadicFunctions[name] = arity - 1;
            return e;
        }

        public virtual NamespaceUri GetNamespace()
        {
            return NamespaceUri.FN;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===

        /// <summary>
        /// An entry in the table describing the properties of a function
        /// </summary>
        public class Entry : IFunctionDefinition
        {
            /// <summary>
            /// The name of the function as a QName
            /// </summary>
            public StructuredQName name;
            /// <summary>
            /// The class containing the implementation of this function (always a subclass of SystemFunction)
            /// </summary>
            public Func<SystemFunction> implementationFactory;
            /// <summary>
            /// The class containing the implementation of this function (always a subclass of SystemFunction)
            /// </summary>
            public Func<Entry, Entry> populator;
            /// <summary>
            /// The function set in which this function is defined
            /// </summary>
            public BuiltInFunctionSet functionSet;
            /// <summary>
            /// The upper bound of the arity range
            /// </summary>
            public int maxArity;
            /// <summary>
            /// The lower bound of the arity range
            /// </summary>
            public int minArity;
            /// <summary>
            /// The item type of the result of the function
            /// </summary>
            public ItemType itemType;
            /// <summary>
            /// The cardinality of the result of the function
            /// </summary>
            public int cardinality;
            /// <summary>
            /// The syntactic context of each argument for the purposes of streamability analysis
            /// </summary>
            public OperandUsage[] usage;
            /// <summary>
            /// An array holding the names of the parameters to the function
            /// </summary>
            public string[] paramNames;
            public SequenceType[] paramTypes;
            public ISequence[] resultIfEmpty;
            public IntHashMap<Expression> defaultValueExpressions;
            public int properties;
            public OptionsParameter optionDetails;

            //
            public virtual int NumberOfParameters => maxArity;
            private readonly object populateLock = new object();
            public virtual void EnsurePopulated()
            {
                lock (populateLock)
                {
                    if (implementationFactory == null)
                    {
                        populator(this);
                    }
                }
            }

            public virtual Entry Populate(Func<SystemFunction> functionFactory, ItemType itemType, int cardinality, int properties)
            {
                this.implementationFactory = functionFactory;
                this.itemType = itemType;
                this.cardinality = cardinality;
                this.properties = properties;
                if (this.maxArity == -1)
                {

                    // special case for concat()
                    this.paramTypes = new SequenceType[1];
                    this.resultIfEmpty = new AtomicValue[1];
                    this.usage = new OperandUsage[1];
                }
                else
                {
                    this.paramTypes = new SequenceType[maxArity];
                    this.resultIfEmpty = new ISequence[maxArity];
                    this.usage = new OperandUsage[maxArity];
                }

                if ((properties & SEQV) != 0)
                {
                    this.functionSet.sequenceVariadicFunctions[name.GetLocalPart()] = this.maxArity - 1;
                }

                NamespaceUri ns = name.GetNamespaceUri();
                Dictionary<string, string> paramNameMap;
                if (ns == NamespaceUri.FN)
                {
                    paramNameMap = ParamKeywords.fnParamNames;
                }
                else if (ns == NamespaceUri.MAP_FUNCTIONS)
                {
                    paramNameMap = ParamKeywords.mapParamNames;
                }
                else if (ns == NamespaceUri.ARRAY_FUNCTIONS)
                {
                    paramNameMap = ParamKeywords.arrayParamNames;
                }
                else if (ns == NamespaceUri.MATH)
                {
                    paramNameMap = ParamKeywords.mathParamNames;
                }
                else
                {
                    paramNameMap = new Dictionary<string, string>();
                }

                string keywords = (paramNameMap.TryGetValue(name.GetLocalPart(), out var __kwA) ? __kwA : null);
                if (keywords == null)
                {
                    keywords = (paramNameMap.TryGetValue(name.GetLocalPart() + "#" + maxArity, out var __kwB) ? __kwB : null);
                }

                if (keywords == null)
                {
                    keywords = "a|b|c|d|e|f";
                }

                this.paramNames = keywords.Split('|');
                return this;
            }

            public virtual Entry Arg(int a, ItemType type, int options, ISequence resultIfEmpty)
            {

                //
                //        public OutSmart.DAXon.Functions.Registry.BuiltInFunctionSet.Entry arg(
                //                int a, ItemType type, int options, ISequence resultIfEmpty, Expression defaultValue) {
                int cardinality = options & StaticProperty.CARDINALITY_MASK;
                OperandUsage usage = OperandUsage.NAVIGATION;
                if ((options & ABS) != 0)
                {
                    usage = OperandUsage.ABSORPTION;
                }
                else if ((options & TRA) != 0)
                {
                    usage = OperandUsage.TRANSMISSION;
                }
                else if ((options & INS) != 0)
                {
                    usage = OperandUsage.INSPECTION;
                }
                else if (type is IPlainType)
                {
                    usage = OperandUsage.ABSORPTION;
                }

                try
                {
                    this.paramTypes[a] = SequenceType.MakeSequenceType(type, cardinality);
                    this.resultIfEmpty[a] = resultIfEmpty;
                    this.usage[a] = usage; //                if (defaultValue != null) {
                    //                    withDefault(a, defaultValue);
                    //                }
                }
                catch (IndexOutOfRangeException err)
                {
                    Console.Error.WriteLine("Internal Saxon error: Can't set argument " + a + " of " + name);
                }

                return this;
            }

            //
            public virtual Entry SetOptionDetails(OptionsParameter details)
            {
                this.optionDetails = details;
                return this;
            }

            //
            public virtual StructuredQName GetFunctionName()
            {
                return name;
            }

            //
            public virtual int GetMinimumArity()
            {
                return minArity;
            }

            //
            public virtual StructuredQName GetParameterName(int i)
            {
                return new StructuredQName("", "", paramNames[i]);
            }

            //
            public virtual Expression GetDefaultValueExpression(int i)
            {
                if (defaultValueExpressions != null)
                {
                    return defaultValueExpressions[i];
                }
                else
                {
                    return null;
                }
            }

            //
            public virtual int GetPositionOfParameter(StructuredQName name)
            {
                for (int i = 0; i < maxArity; i++)
                {
                    if (paramNames[i].Equals(name.GetLocalPart()))
                    {
                        return i;
                    }
                }

                return -1;
            }
        }
    }
}

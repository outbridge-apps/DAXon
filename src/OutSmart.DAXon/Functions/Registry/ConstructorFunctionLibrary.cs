////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Functions.Registry
{
    internal sealed class ConstructorFunctionLibrary : IFunctionLibrary
    {
        private readonly Configuration config;
        public ConstructorFunctionLibrary(Configuration config)
        {
            this.config = config;
        }

        public IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            if (functionName.GetArity() != 1)
            {
                return null;
            }

            NamespaceUri uri = functionName.ComponentName.GetNamespaceUri();
            if (uri.Equals(NamespaceUri.ANONYMOUS))
            {
                return null;
            }

            string localName = functionName.ComponentName.GetLocalPart();
            ISchemaType type = config.GetSchemaType(new StructuredQName("", uri, localName));
            if (type == null || type.IsComplexType())
            {
                return null;
            }

            INamespaceResolver resolver = ((ISimpleType)type).IsNamespaceSensitive() ? staticContext.GetNamespaceResolver() : null;
            if (type is IAtomicType)
            {
                return (IFunctionItem)(new AtomicConstructorFunction((IAtomicType)type, resolver));
            }
            else if (type is IListType)
            {
                return new ListConstructorFunction((IListType)type, resolver, true);
            }
            else
            {
                ICallable callable = new CallableDelegate((context, arguments) =>
                {
                    AtomicValue value = (AtomicValue)arguments[0].Head();
                    if (value == null)
                    {
                        return EmptySequence.GetInstance();
                    }

                    return UnionConstructorFunction.Cast(value, (IUnionType)type, resolver, context.GetConfiguration().GetConversionRules());
                });
                SequenceType returnType = ((IUnionType)type).ResultTypeOfCast;
                return new CallableFunction(1, callable, new SpecificFunctionType(new SequenceType[] { SequenceType.OPTIONAL_ATOMIC }, returnType));
            }
        }

        public bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            if (functionName.GetArity() != 1)
            {
                return false;
            }

            ISchemaType type = config.GetSchemaType(functionName.ComponentName);
            if (type == null || type.IsComplexType())
            {
                return false;
            }

            if (type.IsAtomicType() && ((IAtomicType)type).IsAbstract())
            {
                return false;
            }

            return type != AnySimpleType.INSTANCE;
        }

        public Expression Bind(SymbolicName.F functionName, Expression[] arguments, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            NamespaceUri uri = functionName.ComponentName.GetNamespaceUri();
            string localName = functionName.ComponentName.GetLocalPart();
            bool builtInNamespace = uri.Equals(NamespaceUri.SCHEMA);
            if (builtInNamespace)
            {
                int languageVersion = env.GetXPathVersion();
                if (languageVersion >= 40 && arguments.Length == 0)
                {
                    SymbolicName.F f1 = new SymbolicName.F(functionName.ComponentName, 1);
                    return Bind(f1, new Expression[] { new ContextItemExpression() }, keywords, env, reasons);
                }
                else if (functionName.GetArity() != 1)
                {
                    reasons.Add("A constructor function must have exactly one argument");
                    return null;
                }

                if (keywords != null && keywords.Count > 0)
                {
                    if (keywords.Count != 1)
                    {
                        reasons.Add("The keyword for the sole argument of a constructor function is 'value'");
                        return null;
                    }

                    foreach (var kw in keywords)
                    {
                        if (kw.Key.EQName.Equals("Q{}value"))
                        {
                            if (kw.Value != 0)
                            {
                                reasons.Add("The 'value' keyword in a constructor function call must be the first and only argument");
                                return null;
                            }
                        }
                        else
                        {
                            reasons.Add("The argument keyword '" + kw.Key.EQName + " is not allowed in a constructor function call");
                            return null;
                        }
                    }
                }

                ISimpleType type = (ISimpleType)Types.Type.GetBuiltInSimpleType(uri, localName);
                if (type != null)
                {
                    if (type.IsAtomicType())
                    {
                        if (((IAtomicType)type).IsAbstract())
                        {
                            reasons.Add("Abstract type used in constructor function: {" + uri + '}' + localName);
                            return null;
                        }
                        else
                        {
                            CastExpression cast = new CastExpression(arguments[0], (IAtomicType)type, true);
                            if (arguments[0] is StringLiteral)
                            {
                                cast.SetOperandIsStringLiteral(true);
                            }

                            return cast;
                        }
                    }
                    else if (type.IsUnionType())
                    {
                        INamespaceResolver resolver = env.GetNamespaceResolver();
                        UnionConstructorFunction ucf = new UnionConstructorFunction((IUnionType)type, resolver, true);
                        return new StaticFunctionCall(ucf, arguments);
                    }
                    else
                    {
                        INamespaceResolver resolver = env.GetNamespaceResolver();
                        try
                        {
                            ListConstructorFunction lcf = new ListConstructorFunction((IListType)type, resolver, true);
                            return new StaticFunctionCall(lcf, arguments);
                        }
                        catch (MissingComponentException e)
                        {
                            reasons.Add("Missing schema component: " + e.Message);
                            return null;
                        }
                    }
                }
                else
                {
                    reasons.Add("Unknown constructor function: {" + uri + '}' + localName);
                    return null;
                }
            }


            // Now see if it's a constructor function for a user-defined type
            if (arguments.Length == 1)
            {
                ISchemaType st = config.GetSchemaType(new StructuredQName("", uri, localName));
                if (st is ISimpleType)
                {
                    if (st is IAtomicType)
                    {
                        return new CastExpression(arguments[0], (IAtomicType)st, true);
                    }
                    else if (st is IListType && env.GetXPathVersion() >= 30)
                    {
                        INamespaceResolver resolver = env.GetNamespaceResolver();
                        try
                        {
                            ListConstructorFunction lcf = new ListConstructorFunction((IListType)st, resolver, true);
                            return new StaticFunctionCall(lcf, arguments);
                        }
                        catch (MissingComponentException e)
                        {
                            reasons.Add("Missing schema component: " + e.Message);
                            return null;
                        }
                    }
                    else if (((ISimpleType)st).IsUnionType() && env.GetXPathVersion() >= 30)
                    {
                        INamespaceResolver resolver = env.GetNamespaceResolver();
                        UnionConstructorFunction ucf = new UnionConstructorFunction((IUnionType)st, resolver, true);
                        return new StaticFunctionCall(ucf, arguments);
                    }
                }
            }

            return null;
        }

        public IFunctionLibrary Copy()
        {
            return this;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}
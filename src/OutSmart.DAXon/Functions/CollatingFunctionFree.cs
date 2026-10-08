////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
namespace OutSmart.DAXon.Functions
{
    internal sealed class CollatingFunctionFree : SystemFunction
    {
        protected int CollationArgument => GetArity() - 1;

        public override string StreamerName
        {
            get
            {
                try
                {
                    return BindCollation(NamespaceConstant.CODEPOINT_COLLATION_URI).StreamerName;
                }
                catch (XPathException e)
                {
                    throw new InvalidOperationException(e.Message, e); // should not happen
                }
            }
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            Expression c = arguments[CollationArgument];
            if (c is StringLiteral)
            {
                string coll = ((StringLiteral)c).Stringify();
                try
                {
                    URI collUri = new URI(coll);
                    if (!collUri.IsAbsolute())
                    {
                        collUri = ResolveURI.MakeAbsolute(coll, StaticBaseUriString);
                        coll = collUri.ToASCIIString();
                    }
                }
                catch (URISyntaxException)
                {
                    visitor.StaticContext.IssueWarning("Cannot resolve relative collation URI " + coll, DAXonErrorCode.SXWN9034, c.GetLocation());
                }

                CollatingFunctionFixed fn = BindCollation(coll);
                Expression[] newArgs = new Expression[arguments.Length - 1];
                Array.Copy(arguments, 0, newArgs, 0, newArgs.Length);
                return fn.MakeFunctionCall(newArgs);
            }
            else if (Literal.IsEmptySequence(c))
            {

                // allowed in 4.0
                string coll = visitor.StaticContext.GetDefaultCollationName();
                CollatingFunctionFixed fn = BindCollation(coll);
                Expression[] newArgs = new Expression[arguments.Length - 1];
                Array.Copy(arguments, 0, newArgs, 0, newArgs.Length);
                return fn.MakeFunctionCall(newArgs);
            }

            return null;
        }

        public CollatingFunctionFixed BindCollation(string collationName)
        {
            Configuration config = GetRetainedStaticContext().GetConfiguration();
            int version = GetRetainedStaticContext().GetPackageData().HostLanguageVersion;
            CollatingFunctionFixed @fixed = (CollatingFunctionFixed)config.MakeSystemFunction(GetFunctionName().GetLocalPart(), GetArity() - 1, version);
            @fixed.SetRetainedStaticContext(GetRetainedStaticContext());
            @fixed.SetCollationName(collationName);
            return @fixed;
        }

        public static string ExpandCollationURI(string collationName, URI expressionBaseURI)
        {
            try
            {
                URI collationURI = new URI(collationName);
                if (!collationURI.IsAbsolute())
                {
                    if (expressionBaseURI == null)
                    {
                        throw new XPathException("Cannot resolve relative collation URI '" + collationName + "': unknown or invalid base URI", "FOCH0002");
                    }

                    collationURI = expressionBaseURI.Resolve(collationURI);
                    collationName = collationURI.ToString();
                }
            }
            catch (URISyntaxException)
            {
                throw new XPathException("Collation name '" + collationName + "' is not a valid URI", "FOCH0002");
            }

            return collationName;
        }

        public override ISequence Call(IXPathContext context, ISequence[] args)
        {
            int c = CollationArgument;
            IItem collArg = args[c].Head();
            string collation;
            if (collArg == null)
            {

                // allowed in 4.0
                collation = GetRetainedStaticContext().DefaultCollationName;
            }
            else
            {
                collation = collArg.GetStringValue();
            }

            collation = ExpandCollationURI(collation, GetRetainedStaticContext().GetStaticBaseUri());
            CollatingFunctionFixed @fixed = BindCollation(collation);
            ISequence[] retainedArgs = new ISequence[args.Length - 1];
            Array.Copy(args, 0, retainedArgs, 0, c);
            if (c + 1 < GetArity())
            {
                Array.Copy(args, c + 1, retainedArgs, c, GetArity() - c);
            }

            return @fixed.Call(context, retainedArgs);
        }
    }
}
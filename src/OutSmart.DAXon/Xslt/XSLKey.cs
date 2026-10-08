////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:key elements in stylesheet. <br>
    /// </summary>
    internal sealed class XSLKey : StyleElement, IStylesheetComponent
    {

        private static readonly Func<Expression, bool> containsGlobalVariable = (e) => (e is GlobalVariableReference || e is UserFunctionCall || e is CallTemplate || e is ApplyTemplates);
        private Patterns.Pattern match;
        private Expression use;
        private string collationName;
        private StructuredQName keyName;
        private SlotManager stackFrameMap;
        private bool rangeKey;
        private bool composite = false;
        private KeyDefinition keyDefinition;

        public StructuredQName KeyName
        {
            get
            {

                //We use null to mean "not yet evaluated"
                if (GetObjectName() == null)
                {

                    // allow for forwards references
                    string nameAtt = GetAttributeValue(NamespaceUri.NULL, "name");
                    if (nameAtt != null)
                    {
                        SetObjectName(MakeQName(nameAtt, null, "name"));
                    }
                }

                return GetObjectName();
            }
        }
        public Actor GetActor()
        {
            throw new NotSupportedException();
        }

        public SymbolicName GetSymbolicName()
        {
            return null;
        }

        public void CheckCompatibility(Component component)
        {
        }

        public override bool IsDeclaration()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public SlotManager GetSlotManager()
        {
            return stackFrameMap;
        }

        public override void PrepareAttributes()
        {
            string nameAtt = null;
            string matchAtt = null;
            string useAtt;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                NamespaceUri uri = attName.GetNamespaceUri();
                string local = attName.GetLocalPart();
                if (uri.IsEmpty())
                {
                    switch (local)
                    {
                        case "name":
                            nameAtt = Whitespace.Trim(value);
                            break;
                        case "use":
                            useAtt = value;
                            use = MakeExpression(useAtt, att);
                            break;
                        case "match":
                            matchAtt = value;
                            break;
                        case "collation":
                            collationName = Whitespace.Trim(value);
                            break;
                        case "composite":
                            composite = ProcessBooleanAttribute("composite", value);
                            break;
                        default:
                            CheckUnknownAttribute(attName);
                            break;
                    }
                }
                else if (local.Equals("range-key") && uri.Equals(NamespaceUri.SAXON))
                {
                    if (Core.Version.platform.IsDotNet())
                    {
                        CompileError("saxon:range-key is not supported in SaxonCS");
                    }

                    rangeKey = ProcessBooleanAttribute("range-key", value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (nameAtt == null)
            {
                ReportAbsence("name");
                nameAtt = "_dummy_key_name";
            }

            keyName = MakeQName(nameAtt, null, "name");
            SetObjectName(keyName);
            if (matchAtt == null)
            {
                ReportAbsence("match");
                matchAtt = "*";
            }

            match = MakePattern(matchAtt, "match");
            if (match == null)
            {

                // error has been reported
                match = new NodeTestPattern(ErrorType.GetInstance());
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            Configuration config = GetConfiguration();
            stackFrameMap = config.MakeSlotManager();
            CheckTopLevel("XTSE0010", false);
            if (use != null)
            {

                // the value can be supplied as a content constructor in place of a use expression
                if (HasChildNodes())
                {
                    CompileError("An xsl:key element with a @use attribute must be empty", "XTSE1205");
                }

                try
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:key/use", 0);
                    use = config.GetTypeChecker(false).StaticTypeCheck(use, SequenceType.ATOMIC_SEQUENCE, role, MakeExpressionVisitor());
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }
            else
            {
                if (!HasChildNodes())
                {
                    CompileError("An xsl:key element must either have a @use attribute or have content", "XTSE1205");
                }
            }

            use = TypeCheck("use", use);
            match = TypeCheck("match", match);

            // Do a further check that the use expression makes sense in the context of the match pattern
            if (use != null)
            {
                use = use.TypeCheck(MakeExpressionVisitor(), config.MakeContextItemStaticInfo(match.GetItemType(), false));
            }

            if (collationName != null)
            {
                URI collationURI;
                try
                {
                    collationURI = new URI(collationName);
                    if (!collationURI.IsAbsolute())
                    {
                        URI @base = new URI(GetBaseURI());
                        collationURI = @base.Resolve(collationURI);
                        collationName = collationURI.ToString();
                    }
                }
                catch (URISyntaxException err)
                {
                    CompileError("Collation name '" + collationName + "' is not a valid URI"); //collationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
                }
            }
            else
            {
                collationName = GetDefaultCollationName();
            }
        }
        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            StructuredQName keyName = KeyName;
            if (keyName != null)
            {
                top.GetKeyManager().PreRegisterKeyDefinition(keyName);
            }
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
            IStaticContext env = GetStaticContext();
            Configuration config = env.GetConfiguration();
            IStringCollator collator = null;
            if (collationName != null)
            {
                try
                {
                    collator = FindCollation(collationName, GetBaseURI());
                }
                catch (XPathException err)
                {
                    CompileError("Failed to load collation " + collationName + ": " + err.Message, "XTSE1210");
                    collator = CodepointCollator.GetInstance(); // for recovery paths
                }

                if (collator == null)
                {
                    CompileError("The collation name " + Err.Wrap(collationName, Err.URI) + " is not recognized", "XTSE1210");
                    collator = CodepointCollator.GetInstance();
                }

                if (collator is CodepointCollator)
                {

                    // if the user explicitly asks for the codepoint collation, treat it as if they hadn't asked
                    collator = null;
                    collationName = null;
                }
                else if (!Core.Version.platform.CanReturnCollationKeys(collator))
                {
                    CompileError("The collation used for xsl:key must be capable of generating collation keys", "XTSE1210");
                }
            }

            if (use == null)
            {
                Expression body = CompileSequenceConstructor(compilation, decl, true);
                try
                {
                    use = Atomizer.MakeAtomizer(body, null);
                    use = use.Simplify();
                }
                catch (XPathException e)
                {
                    CompileError(e);
                }

                try
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:key/use", 0);
                    use = config.GetTypeChecker(false).StaticTypeCheck(use, SequenceType.ATOMIC_SEQUENCE, role, MakeExpressionVisitor());
                    use = use.TypeCheck(MakeExpressionVisitor(), config.MakeContextItemStaticInfo(match.GetItemType(), false));
                }
                catch (XPathException err)
                {
                    CompileError(err);
                }
            }

            ItemType useItemType = use.GetItemType();
            if (useItemType == ErrorType.GetInstance())
            {
                useItemType = BuiltInAtomicType.STRING; // corner case, prevents crashing
            }

            BuiltInAtomicType useType = (BuiltInAtomicType)useItemType.GetPrimitiveItemType();
            if (XPath10ModeIsEnabled())
            {
                if (!useType.Equals(BuiltInAtomicType.STRING) && !useType.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
                {
                    use = new AtomicSequenceConverter(use, BuiltInAtomicType.STRING);
                    Converter c = ((AtomicSequenceConverter)use).AllocateConverter(config, false);
                    ((AtomicSequenceConverter)use).SetConverter(c);
                    useType = BuiltInAtomicType.STRING;
                }
            }


            // first slot in pattern is reserved for current()
            int nextFree = 0;
            if ((match.Dependencies & StaticProperty.DEPENDS_ON_CURRENT_ITEM) != 0)
            {
                nextFree = 1;
            }

            match.AllocateSlots(stackFrameMap, nextFree);

            // If either the match pattern or the use expression references a global variable or parameter,
            // or a call on a function or template that might reference one, then
            // the key indexes cannot be reused across multiple transformations. See Saxon bug 1968.
            bool sensitive = ExpressionTool.Contains(use, false, containsGlobalVariable) || ExpressionTool.Contains(match, false, containsGlobalVariable);
            KeyManager km = GetCompilation().GetPrincipalStylesheetModule().GetKeyManager();
            SymbolicName symbolicName = new SymbolicName(StandardNames.XSL_KEY, keyName);
            KeyDefinition keydef = new KeyDefinition(symbolicName, match, use, collationName, collator);
            keydef.SetPackageData(GetCompilation().GetPackageData());
            keydef.SetRangeKey(rangeKey);
            keydef.IndexedItemType = useType;
            keydef.SetStackFrameMap(stackFrameMap);
            keydef.SetLocation(this);
            keydef.SetBackwardsCompatible(XPath10ModeIsEnabled());
            keydef.SetComposite(composite);
            keydef.ObtainDeclaringComponent(this);
            try
            {
                km.AddKeyDefinition(keyName, keydef, !sensitive, compilation.GetConfiguration());
            }
            catch (XPathException err)
            {
                CompileError(err);
            }

            keyDefinition = keydef;
        }

        public void Optimize(ComponentDeclaration declaration)
        {
            ExpressionVisitor visitor = MakeExpressionVisitor();
            ContextItemStaticInfo contextItemType = GetConfiguration().MakeContextItemStaticInfo(match.GetItemType(), false);
            Expression useExp = keyDefinition.Use;
            useExp = useExp.Optimize(visitor, contextItemType);
            AllocateLocalSlots(useExp);
            keyDefinition.SetBody(useExp);
        }
    }
}

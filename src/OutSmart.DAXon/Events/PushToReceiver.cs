////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Api.Push;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Events
{
    // The push API over a destination: a document the host writes node by node. How far names are checked is left
    // to the implementation: here a name is an NCName, none is one XML Namespaces reserves, a comment holds no "--"
    // and an instruction no "?>", so what the host passes as a name or as content cannot end up as markup of its own.
    internal sealed class PushToReceiver : IPush
    {
        private readonly ComplexContentOutputter cco;
        private readonly Configuration config;
        private readonly IDestination destination;

        // Set when an event was refused: the document is not finished after that, only its destination released.
        private bool failed;

        public PushToReceiver(IReceiver receiver, IDestination destination)
        {
            cco = new ComplexContentOutputter(new RegularSequenceChecker(receiver, false));
            config = receiver.GetPipelineConfiguration().GetConfiguration();
            this.destination = destination;
        }

        public IDocument IDocument(bool wellFormed)
        {
            Send(() =>
            {
                cco.Open();
                cco.StartDocument(ReceiverOption.NONE);
            });
            return new DocImpl(this, wellFormed);
        }

        private void Send(Action events)
        {
            try
            {
                events();
            }
            catch (XPathException e)
            {
                throw Refused(new DAXonApiException(e));
            }
            catch (RecursionDepthError e)
            {
                throw Refused(new DAXonApiException(e.ToXPathException()));
            }
        }

        private DAXonApiException Refused(DAXonApiException e)
        {
            failed = true;
            return e;
        }

        private DAXonApiException Refused(string message)
        {
            return Refused(new DAXonApiException(message));
        }

        // Content the host hands over: a string cut through a surrogate pair has U+FFFD where the half is.
        private static string Paired(string value)
        {
            return StringTool.WithoutHalfPairs(value);
        }

        private string Name(string what, string name)
        {
            if (name == null || !NameChecker.IsValidNCName(name))
            {
                throw Refused("The name of " + what + " must be an NCName: " + (name == null ? "null" : "'" + name + "'"));
            }

            return name;
        }

        private INodeName Name(string what, QName name, bool attribute)
        {
            if (name == null)
            {
                throw Refused("The name of " + what + " is null");
            }

            string local = Name(what, name.LocalName);
            string prefix = name.GetPrefix() ?? "";
            NamespaceUri uri = name.GetNamespaceUri();
            if (prefix.Length > 0)
            {
                Name("the prefix of " + what, prefix);
                if (uri.IsEmpty())
                {
                    throw Refused("The name of " + what + " has a prefix and no namespace: '" + prefix + ":" + local + "'");
                }
            }

            // What XML Namespaces keeps to itself: xmlns and the namespace of declarations are no part of a name, the
            // namespace of xml: goes under that prefix. An attribute in a namespace needs a prefix, and is given one.
            if (prefix == "xmlns" || uri.Equals(NamespaceUri.XMLNS))
            {
                throw Refused("The name of " + what + " cannot have the prefix xmlns or be in the namespace " + NamespaceUri.XMLNS);
            }

            if (attribute && uri.IsEmpty())
            {
                return new NoNamespaceName(AttributeName(local));
            }

            string written = uri.Equals(NamespaceUri.XML) ? "xml" : attribute && prefix.Length == 0 ? "ns0" : prefix;
            return written == prefix ? new FingerprintedQName(name.GetStructuredQName(), config.GetNamePool()) : new FingerprintedQName(written, uri, local);
        }

        // An attribute named xmlns in no namespace would be written as the declaration of a default namespace.
        private string AttributeName(string local)
        {
            if (local == "xmlns")
            {
                throw Refused("An attribute cannot be named xmlns: Namespace() declares a namespace");
            }

            return local;
        }

        // Whether the binding is one to declare. The prefix xml is bound already, to its own namespace and no other;
        // that namespace takes no other prefix, and neither xmlns nor the namespace of declarations is ever bound.
        private bool Declares(string prefix, NamespaceUri uri)
        {
            bool xmlPrefix = prefix == "xml";
            if (prefix == "xmlns" || uri.Equals(NamespaceUri.XMLNS) || xmlPrefix != uri.Equals(NamespaceUri.XML))
            {
                throw Refused((prefix.Length == 0 ? "The default namespace" : "The prefix " + prefix) + " cannot be bound to the namespace '" + uri + "'");
            }

            return !xmlPrefix;
        }

        private abstract class ContainerImpl : IContainer
        {
            protected readonly PushToReceiver push;
            private string defaultNamespace;
            private ElemImpl elementAwaitingClosure;
            private bool closed;

            protected ContainerImpl(PushToReceiver push, string defaultNamespace)
            {
                this.push = push;
                this.defaultNamespace = defaultNamespace;
            }

            public void SetDefaultNamespace(string uri)
            {
                uri = Paired(uri ?? "");
                push.Declares("", NamespaceUri.Of(uri));
                defaultNamespace = uri;
            }

            public virtual IElement Element(QName name)
            {
                ImplicitClose();
                INodeName nodeName = push.Name("an element", name, false);
                push.Send(() => push.cco.StartElement(nodeName, Untyped.GetInstance(), Loc.NONE, ReceiverOption.NONE));
                return elementAwaitingClosure = new ElemImpl(push, defaultNamespace);
            }

            public virtual IElement Element(string name)
            {
                ImplicitClose();
                push.Name("an element", name);
                INodeName nodeName = defaultNamespace.Length == 0 ? new NoNamespaceName(name) : new FingerprintedQName("", NamespaceUri.Of(defaultNamespace), name);
                push.Send(() => push.cco.StartElement(nodeName, Untyped.GetInstance(), Loc.NONE, ReceiverOption.NONE));
                return elementAwaitingClosure = new ElemImpl(push, defaultNamespace);
            }

            IContainer IContainer.Text(string value)
            {
                WriteText(value);
                return this;
            }

            IContainer IContainer.Comment(string value)
            {
                WriteComment(value);
                return this;
            }

            IContainer IContainer.ProcessingInstruction(string name, string value)
            {
                WriteProcessingInstruction(name, value);
                return this;
            }

            // Text of no length and null are no text; a null comment or instruction is not written.
            protected virtual void WriteText(string value)
            {
                ImplicitClose();
                if (!string.IsNullOrEmpty(value))
                {
                    push.Send(() => push.cco.Characters(StringView.Of(Paired(value)), Loc.NONE, ReceiverOption.NONE));
                }
            }

            protected virtual void WriteComment(string value)
            {
                ImplicitClose();
                if (value != null)
                {
                    if (value.Contains("--") || value.EndsWith("-", StringComparison.Ordinal))
                    {
                        throw push.Refused("A comment cannot contain '--' or end with '-'");
                    }

                    push.Send(() => push.cco.Comment(StringView.Of(Paired(value)), Loc.NONE, ReceiverOption.NONE));
                }
            }

            protected virtual void WriteProcessingInstruction(string name, string value)
            {
                ImplicitClose();
                if (value != null)
                {
                    push.Name("a processing instruction", name);
                    if (name.Equals("xml", StringComparison.OrdinalIgnoreCase))
                    {
                        throw push.Refused("A processing instruction cannot be named '" + name + "'");
                    }

                    if (value.Contains("?>"))
                    {
                        throw push.Refused("A processing instruction cannot contain '?>'");
                    }

                    push.Send(() => push.cco.ProcessingInstruction(name, StringView.Of(Paired(value)), Loc.NONE, ReceiverOption.NONE));
                }
            }

            // Closing twice is closing once; after a refused event there is no document to finish.
            public void Close()
            {
                if (closed)
                {
                    return;
                }

                closed = true;
                if (push.failed)
                {
                    DestinationHelper.ReleaseUnclosed(push.destination);
                    return;
                }

                if (elementAwaitingClosure != null)
                {
                    elementAwaitingClosure.Close();
                    elementAwaitingClosure = null;
                }

                SendEndEvent();
            }

            public void Dispose()
            {
                Close();
            }

            protected void ImplicitClose()
            {
                if (closed)
                {
                    throw push.Refused("The container has been closed");
                }

                if (push.failed)
                {
                    throw new DAXonApiException("Nothing can be written after an event that was refused");
                }

                if (elementAwaitingClosure != null)
                {
                    elementAwaitingClosure.Close();
                    elementAwaitingClosure = null;
                }
            }

            protected abstract void SendEndEvent();
        }

        private sealed class DocImpl : ContainerImpl, IDocument
        {
            private readonly bool wellFormed;
            private bool foundElement;

            public DocImpl(PushToReceiver push, bool wellFormed) : base(push, "")
            {
                this.wellFormed = wellFormed;
            }

            public override IElement Element(QName name)
            {
                OneElement();
                return base.Element(name);
            }

            public override IElement Element(string name)
            {
                OneElement();
                return base.Element(name);
            }

            private void OneElement()
            {
                if (wellFormed && foundElement)
                {
                    throw push.Refused("A well-formed document cannot have more than one element child");
                }

                foundElement = true;
            }

            public IDocument Text(string value)
            {
                WriteText(value);
                return this;
            }

            public IDocument Comment(string value)
            {
                WriteComment(value);
                return this;
            }

            public IDocument ProcessingInstruction(string name, string value)
            {
                WriteProcessingInstruction(name, value);
                return this;
            }

            protected override void WriteText(string value)
            {
                if (wellFormed && !string.IsNullOrEmpty(value))
                {
                    throw push.Refused("A well-formed document cannot contain text outside any element");
                }

                base.WriteText(value);
            }

            protected override void SendEndEvent()
            {
                if (wellFormed && !foundElement)
                {
                    DestinationHelper.ReleaseUnclosed(push.destination);
                    throw push.Refused("A well-formed document must contain an element node");
                }

                bool finished = false;
                try
                {
                    push.Send(() =>
                    {
                        push.cco.EndDocument();
                        push.cco.Close();
                    });
                    push.destination.CloseAndNotify();
                    finished = true;
                }
                finally
                {
                    if (!finished)
                    {
                        DestinationHelper.ReleaseUnclosed(push.destination);
                    }
                }
            }
        }

        private sealed class ElemImpl : ContainerImpl, IElement
        {
            private bool foundChild;

            public ElemImpl(PushToReceiver push, string defaultNamespace) : base(push, defaultNamespace)
            {
            }

            public IElement Attribute(QName name, string value)
            {
                BeforeChildren("An attribute");
                if (value != null)
                {
                    INodeName nodeName = push.Name("an attribute", name, true);
                    push.Send(() => push.cco.Attribute(nodeName, BuiltInAtomicType.UNTYPED_ATOMIC, Paired(value), Loc.NONE, ReceiverOption.NONE));
                }

                return this;
            }

            public IElement Attribute(string name, string value)
            {
                BeforeChildren("An attribute");
                if (value != null)
                {
                    INodeName nodeName = new NoNamespaceName(push.AttributeName(push.Name("an attribute", name)));
                    push.Send(() => push.cco.Attribute(nodeName, BuiltInAtomicType.UNTYPED_ATOMIC, Paired(value), Loc.NONE, ReceiverOption.NONE));
                }

                return this;
            }

            public IElement Namespace(string prefix, string uri)
            {
                BeforeChildren("A namespace");
                prefix = prefix ?? "";
                if (prefix.Length > 0)
                {
                    push.Name("a namespace prefix", prefix);
                }

                NamespaceUri bound = NamespaceUri.Of(Paired(uri ?? ""));
                if (push.Declares(prefix, bound))
                {
                    push.Send(() => push.cco.Namespace(prefix, bound, ReceiverOption.NONE));
                }

                return this;
            }

            private void BeforeChildren(string what)
            {
                if (foundChild)
                {
                    throw push.Refused(what + " must be attached to an element before any children");
                }

                ImplicitClose();
            }

            public override IElement Element(QName name)
            {
                foundChild = true;
                return base.Element(name);
            }

            public override IElement Element(string name)
            {
                foundChild = true;
                return base.Element(name);
            }

            public IElement Text(string value)
            {
                WriteText(value);
                return this;
            }

            public IElement Comment(string value)
            {
                WriteComment(value);
                return this;
            }

            public IElement ProcessingInstruction(string name, string value)
            {
                WriteProcessingInstruction(name, value);
                return this;
            }

            protected override void WriteText(string value)
            {
                foundChild = true;
                base.WriteText(value);
            }

            protected override void WriteComment(string value)
            {
                foundChild = true;
                base.WriteComment(value);
            }

            protected override void WriteProcessingInstruction(string name, string value)
            {
                foundChild = true;
                base.WriteProcessingInstruction(name, value);
            }

            protected override void SendEndEvent()
            {
                push.Send(() => push.cco.EndElement());
            }
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Events
{
    internal sealed class RegularSequenceChecker : ProxyReceiver
    {
        private static readonly Dictionary<State, Dictionary<Transition, State>> machine = new Dictionary<State, Dictionary<Transition, State>>();
        private readonly Stack<int> stack = new Stack<int>();

        private State state;
        private bool fullChecking = false;

        // for C#
        static RegularSequenceChecker()
        {
            Edge(State.INITIAL, Transition.OPEN, State.OPEN);
            Edge(State.OPEN, Transition.APPEND, State.OPEN);
            Edge(State.OPEN, Transition.TEXT, State.OPEN);
            Edge(State.OPEN, Transition.COMMENT, State.OPEN);
            Edge(State.OPEN, Transition.PI, State.OPEN);
            Edge(State.OPEN, Transition.START_DOCUMENT, State.CONTENT);
            Edge(State.OPEN, Transition.START_ELEMENT, State.CONTENT);
            Edge(State.CONTENT, Transition.TEXT, State.CONTENT);
            Edge(State.CONTENT, Transition.COMMENT, State.CONTENT);
            Edge(State.CONTENT, Transition.PI, State.CONTENT);
            Edge(State.CONTENT, Transition.START_ELEMENT, State.CONTENT);
            Edge(State.CONTENT, Transition.END_ELEMENT, State.CONTENT); // or Open if the stack is empty
            Edge(State.CONTENT, Transition.END_DOCUMENT, State.OPEN);
            Edge(State.OPEN, Transition.CLOSE, State.FINAL);
            Edge(State.FAILED, Transition.CLOSE, State.FAILED); //edge(State.Final, "close", State.Final);  // This was a concession to poor practice, but apparently no longer needed
        }

        public RegularSequenceChecker(IReceiver nextReceiver, bool fullChecking) : base(nextReceiver)
        {
            state = State.INITIAL;
            this.fullChecking = fullChecking;
        }
        private static void Edge(State from, Transition @event, State to)
        {
            Dictionary<Transition, State> edges = machine.ComputeIfAbsent(from, (s) => new Dictionary<Transition, State>());
            edges[@event] = to;
        }

        // for C#
        private void TransitionFn(Transition @event)
        {
            Dictionary<Transition, State> map = machine.GetOrDefault(state);
            if (map.ContainsKey(@event))
            {
                state = map.GetOrDefault(@event);
            }
            else
            {
                throw new InvalidOperationException("Event " + @event + " is not permitted in state " + state);
            }
        }

        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            try
            {
                TransitionFn(Transition.APPEND);
                nextReceiver.Append(item, locationId, copyNamespaces);
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            TransitionFn(Transition.TEXT);
            if (chars.IsEmpty() && stack.Count > 0)
            {
                throw new InvalidOperationException("Zero-length text nodes not allowed within document/element content");
            }

            try
            {
                nextReceiver.Characters(chars, locationId, properties);
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        // for C#
        /// <summary>
        /// End of sequence
        /// </summary>
        public override void Close()
        {
            if (state != State.FINAL && state != State.FAILED)
            {
                if (stack.Count > 0)
                {
                    throw new InvalidOperationException("Unclosed element or document nodes at end of stream");
                }

                nextReceiver.Close();
                state = State.FINAL;
            }
        }

        // for C#
        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            TransitionFn(Transition.COMMENT);
            try
            {
                nextReceiver.Comment(chars, locationId, properties);
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        // for C#
        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            TransitionFn(Transition.END_DOCUMENT);
            if (stack.Count == 0 || stack.Pop() != Types.Type.DOCUMENT)
            {
                throw new InvalidOperationException("Unmatched endDocument() call");
            }

            try
            {
                nextReceiver.EndDocument();
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        // for C#
        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            TransitionFn(Transition.END_ELEMENT);
            if (stack.Count == 0 || stack.Pop() != Types.Type.ELEMENT)
            {
                throw new InvalidOperationException("Unmatched endElement() call");
            }

            if (stack.Count == 0)
            {
                state = State.OPEN;
            }

            try
            {
                nextReceiver.EndElement();
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        // for C#
        /// <summary>
        /// Start of event stream
        /// </summary>
        public override void Open()
        {
            TransitionFn(Transition.OPEN);
            try
            {
                nextReceiver.Open();
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        // for C#
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            TransitionFn(Transition.PI);
            try
            {
                nextReceiver.ProcessingInstruction(target, data, locationId, properties);
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        // for C#
        public override void StartDocument(int properties)
        {
            TransitionFn(Transition.START_DOCUMENT);
            stack.Push(Types.Type.DOCUMENT);
            try
            {
                nextReceiver.StartDocument(properties);
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }

        // for C#
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            TransitionFn(Transition.START_ELEMENT);
            stack.Push(Types.Type.ELEMENT);
            if (fullChecking)
            {
                attributes.Verify();
                string prefix = elemName.GetPrefix();
                if ((prefix.Length == 0))
                {
                    NamespaceUri declaredDefaultUri = namespaces.DefaultNamespace;
                    if (!declaredDefaultUri.Equals(elemName.GetNamespaceUri()))
                    {
                        throw new InvalidOperationException("URI of element Q{" + elemName.GetNamespaceUri() + "}" + elemName.GetLocalPart() + " does not match declared default namespace {" + declaredDefaultUri + "}");
                    }
                }
                else
                {
                    NamespaceUri declaredUri = namespaces.GetNamespaceUri(prefix);
                    if (declaredUri == null)
                    {
                        throw new InvalidOperationException("Prefix " + prefix + " has not been declared");
                    }
                    else if (!declaredUri.Equals(elemName.GetNamespaceUri()))
                    {
                        throw new InvalidOperationException("Prefix " + prefix + " is bound to the wrong namespace");
                    }
                }

                foreach (AttributeInfo att in attributes)
                {
                    INodeName name = att.GetNodeName();
                    if (!name.GetNamespaceUri().IsEmpty())
                    {
                        string attPrefix = name.GetPrefix();
                        NamespaceUri declaredUri = namespaces.GetNamespaceUri(attPrefix);
                        if (declaredUri == null)
                        {
                            throw new InvalidOperationException("Prefix " + attPrefix + " has not been declared for attribute " + att.GetNodeName().DisplayName);
                        }
                        else if (!declaredUri.Equals(name.GetNamespaceUri()))
                        {
                            throw new InvalidOperationException("Prefix " + prefix + " is bound to the wrong namespace {" + declaredUri + "}");
                        }
                    }
                }
            }

            try
            {
                nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
            }
            catch (XPathException e)
            {
                state = State.FAILED;
                throw e;
            }
        }
        internal enum State
        {
            INITIAL,
            OPEN,
            START_TAG,
            CONTENT,
            FINAL,
            FAILED
        }

        private enum Transition
        {
            OPEN,
            APPEND,
            TEXT,
            COMMENT,
            PI,
            START_DOCUMENT,
            START_ELEMENT,
            END_ELEMENT,
            END_DOCUMENT,
            CLOSE
        }
    }
}

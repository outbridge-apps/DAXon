////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

namespace OutSmart.DAXon.Transformation
{
}


// Java stdlib namespace stubs for transpiled references to packages we don't implement.






// LocalDateTime/Instant/ZonedDateTime/OffsetDateTime/LocalDate live in JavaTime.cs
// (this file just adds ChronoField/UnsupportedTemporalTypeException below).


// TemporalField/ChronoField/UnsupportedTemporalTypeException live in JavaTime.cs.


// Saxon-internal stub namespaces — sub-packages permanently excluded for now.
// Stubs only what's needed for top-level references to resolve.


// IApiProvider for s9api ApiProvider interface (referenced by Processor etc).
// Removed OutSmart.DAXon.Api.IApiProvider stub -- it shadowed the
// real OutSmart.DAXon.Core.Configuration.IApiProvider, causing Processor to implement
// the wrong one. Callers should reference Configuration.IApiProvider directly.
// namespace OutSmart.DAXon.Api { public interface IApiProvider { } }
// IPush — Processor.cs references it via FQN
// OutSmart.DAXon.Api.Streams.Step — referenced from a few non-excluded files. Provide a non-generic alias.

// Stub for net.sf.saxon.type.Type (the original file is excluded but ~180 files alias it
// as `using Type = OutSmart.DAXon.Types.Type;` and reference its node-kind constants).

// Stub for net.sf.saxon.value.SequenceType (transpiler collision with s9api.SequenceType lost the Value one).
// All 56 static field constants from Saxon's value/SequenceType.java + factory methods.


// Saxon s9api streaming wrappers — Step<T>, Predicates excluded from build. Provide minimal namespace.

// JAXP javax.xml.namespace package — `namespace` is a C# keyword, manually capitalized to Namespace.
// (NamespaceContext + QName already defined in JavaTime.cs + JavaxXml.cs; just need namespace to exist.)



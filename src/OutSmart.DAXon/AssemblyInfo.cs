using System.Runtime.CompilerServices;

// The supported surface of this assembly is OutSmart.DAXon.Api plus what its signatures
// reach; everything else is internal, so the engine is free to change shape without
// breaking a host. The first-party assemblies below are built from this repository and
// reach past that line on purpose: the HTML front end drives the tree builder directly,
// and the test projects exercise engine internals that no host is meant to touch.
[assembly: InternalsVisibleTo("OutSmart.DAXon.Html")]
[assembly: InternalsVisibleTo("OutSmart.DAXon.RobustProbes")]
[assembly: InternalsVisibleTo("OutSmart.DAXon.SpecTests")]
[assembly: InternalsVisibleTo("OutSmart.DAXon.TwinsTest")]
[assembly: InternalsVisibleTo("OutSmart.DAXon.SoakTest")]
[assembly: InternalsVisibleTo("HtmlTests")]
[assembly: InternalsVisibleTo("QT3Test")]
[assembly: InternalsVisibleTo("JavaCompatTests")]
[assembly: InternalsVisibleTo("DAXonRunner")]

// No [assembly: CLSCompliant(true)], and that is a decision, not an oversight: with it the
// compiler reports 116 violations (104 CS3005 case-colliding identifiers, 8 CS3008 leading
// underscores, 4 CS3026 volatile fields). Every CS3005 involves a PROTECTED member, which no
// case-insensitive binder ever sees; a robustness test covers the public pairs, which are the ones that
// broke PowerShell. Closed as not fixing.

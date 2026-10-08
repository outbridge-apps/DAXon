////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Types
{
    internal static class BuiltInType
    {
        // net472 port: the real OutSmart.DAXon.Types.BuiltInType is excluded; this stub was hollow (Register
        // no-op, GetSchemaType => null), so BuiltInAtomicType.MakeAtomicType's BuiltInType.Register(fp,type)
        // calls were lost and constructor functions (xs:integer/xs:decimal/...) failed at compile with
        // XPST0017 "Unknown constructor function". Make it a real registry mirroring the excluded
        // BuiltInType.GetSchemaType: Register stores; GetSchemaType forces BuiltInAtomicType static init
        // (whose static field initializers run MakeAtomicType -> Register) on a miss, then re-reads.
        private static readonly Dictionary<int, object> _lookup = new Dictionary<int, object>();
        // TIER-2 2026-06-17: parallel by-local-name registry, faithful mirror of upstream BuiltInType.lookupByLocalName.
        // Populated by Register alongside _lookup so fn:type-available / Configuration.GetSchemaType(QName) can resolve
        // xs:* built-in schema types by local part (was a hollow NIE stub -> type-available threw at runtime).
        private static readonly Dictionary<string, object> _lookupByLocalName = new Dictionary<string, object>();
        public static object GetSchemaType(int fingerprint)
        {
            // Non-atomic built-ins never call Register (no fingerprint in _lookup), so resolve them directly —
            // same set GetSchemaTypeByLocalName special-cases. Without this, Types.GetBuiltInSimpleType(xs,
            // "anySimpleType") returned null and `cast as xs:anySimpleType` gave XQST0052 instead of XPST0080
            // (target is a known SimpleType but not a casting target).
            switch (fingerprint)
            {
                case StandardNames.XS_UNTYPED: return Untyped.INSTANCE;
                case StandardNames.XS_ANY_TYPE: return AnyType.INSTANCE;
                case StandardNames.XS_ANY_SIMPLE_TYPE: return AnySimpleType.INSTANCE;
                // xs:error is a (union) SimpleType but never Register()s a fingerprint; resolve it directly so
                // the xs:error constructor function exists (function-1901) — casting to it always fails FORG0001.
                case StandardNames.XS_ERROR: return ErrorType.GetInstance();
            }
            object t;
            if (!_lookup.TryGetValue(fingerprint, out t) || t == null)
            {
                if (BuiltInAtomicType.DOUBLE == null) { }
                if (BuiltInListType.NMTOKENS == null) { } // force list-type registration (NMTOKENS/IDREFS/ENTITIES)
                NumericType.GetInstance(); // force xs:numeric union registration (lazy; registered inside GetInstance, not a static field)
                _lookup.TryGetValue(fingerprint, out t);
            }
            return t;
        }
        // Mirrors upstream getSchemaTypeByLocalName: force BuiltInAtomicType static init on a miss (its field
        // initializers run MakeAtomicType -> Register, filling both maps), then re-read. Returns null for unknown.
        public static object GetSchemaTypeByLocalName(string local)
        {
            // Non-atomic built-ins register nowhere (their singletons don't call Register); resolve directly,
            // as upstream BuiltInType's static block does (element(*, xs:untyped) etc. -> was XPST0008).
            switch (local)
            {
                case "untyped": return Untyped.INSTANCE;
                case "anyType": return AnyType.INSTANCE;
                case "anySimpleType": return AnySimpleType.INSTANCE;
                case "error": return ErrorType.GetInstance();   // by-QName lookup for the xs:error constructor (function-1901)
            }
            object t;
            if (!_lookupByLocalName.TryGetValue(local, out t) || t == null)
            {
                if (BuiltInAtomicType.DOUBLE == null) { }
                if (BuiltInListType.NMTOKENS == null) { } // force list-type registration (NMTOKENS/IDREFS/ENTITIES)
                NumericType.GetInstance(); // force xs:numeric union registration (lazy; registered inside GetInstance, not a static field)
                _lookupByLocalName.TryGetValue(local, out t);
            }
            return t;
        }
        public static void Register(int fingerprint, object type)
        {
            _lookup[fingerprint] = type;
            var st = type as ISchemaType;
            if (st != null)
            {
                _lookupByLocalName[st.Name] = type;
            }
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Functions.Registry
{
    public class UseWhen30FunctionSet : BuiltInFunctionSet
    {
        private static readonly UseWhen30FunctionSet THE_INSTANCE = new UseWhen30FunctionSet(31);

        protected UseWhen30FunctionSet(int version)
        {
            Init(version);
        }
        public static UseWhen30FunctionSet GetInstance(int version)
        {
            return THE_INSTANCE;
        }

        protected virtual void Init(int version)
        {
            AddXPathFunctions(version);
            Register("available-system-properties", 0, (e) => e.Populate(() => new AvailableSystemProperties(), BuiltInAtomicType.QNAME, STAR, LATE));
            Register("element-available", 1, (e) => e.Populate(() => new ElementAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("function-available", 1, (e) => e.Populate(() => new FunctionAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("function-available", 2, (e) => e.Populate(() => new FunctionAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));
            Register("system-property", 1, (e) => e.Populate(() => new SystemProperty(), BuiltInAtomicType.STRING, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("type-available", 1, (e) => e.Populate(() => new TypeAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS).Arg(0, BuiltInAtomicType.STRING, ONE, null));
        }

        protected virtual void AddXPathFunctions(int version)
        {

            // Ignore request for 40, not supported in HE
            ImportFunctionSet(XPath31FunctionSet.GetInstance());
        }
    }
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Lib
{
    internal sealed class FeatureIndex
    {
        private static readonly Dictionary<string, FeatureData> byName = new Dictionary<string, FeatureData>();
        private static readonly IntHashMap<FeatureData> byCode = new IntHashMap<FeatureData>();

        static FeatureIndex()
        {
            FeatureData.Init();
            foreach (FeatureData data in FeatureData.featureList)
            {
                byName[data.uri] = data;
                byCode.Put(data.code, data);
            }
        }

        public static bool Exists(string featureName)
        {
            return byName.ContainsKey(featureName);
        }

        public static FeatureData GetData(string featureName)
        {
            return byName.GetOrDefault(featureName);
        }

        public static FeatureData GetData(int code)
        {
            return byCode[code];
        }
    }
}
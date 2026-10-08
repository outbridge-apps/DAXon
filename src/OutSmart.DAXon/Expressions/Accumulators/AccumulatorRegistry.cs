////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Accumulators
{
    public class AccumulatorRegistry
    {
        protected Dictionary<StructuredQName, Accumulator> accumulatorsByName = new Dictionary<StructuredQName, Accumulator>();

        public virtual IEnumerable<Accumulator> AllAccumulators => accumulatorsByName.Values;
        public AccumulatorRegistry()
        {
        }

        public virtual HashSet<Accumulator> GetUsedAccumulators(string useAccumulatorsAtt, StyleElement styleElement)
        {
            HashSet<Accumulator> accumulators = new HashSet<Accumulator>();
            string attNames = Whitespace.Trim(useAccumulatorsAtt);
            string[] tokens = attNames.SplitRegex("[ \t\r\n]+");
            if (tokens.Length == 1 && tokens[0].Equals("#all"))
            {
                foreach (Accumulator acc in AllAccumulators)
                {
                    accumulators.Add(acc);
                }
            }
            else if (tokens.Length == 1 && (tokens[0].Length == 0))
            {
            }
            else
            {
                IList<StructuredQName> names = new List<StructuredQName>(tokens.Length);
                foreach (string token in tokens)
                {
                    if (token.Equals("#all"))
                    {
                        styleElement.CompileErrorInAttribute("If use-accumulators contains the token '#all', it must be the only token", "XTSE3300", "use-accumulators");
                        break;
                    }

                    StructuredQName name = styleElement.MakeQName(token, "XTSE3300", "use-accumulators");
                    if (names.Contains(name))
                    {
                        styleElement.CompileErrorInAttribute("Duplicate QName in use-accumulators attribute: " + token, "XTSE3300", "use-accumuators");
                        break;
                    }

                    Accumulator acc = GetAccumulator(name);
                    if (acc == null)
                    {
                        styleElement.CompileErrorInAttribute("Unknown accumulator name: " + token, "XTSE3300", "use-accumulators");
                        break;
                    }

                    names.Add(name);
                    accumulators.Add(acc);
                }
            }

            return accumulators;
        }

        public virtual void AddAccumulator(Accumulator acc)
        {
            if (acc.AccumulatorName != null)
            {
                accumulatorsByName[acc.AccumulatorName] = acc;
            }
        }

        public virtual Accumulator GetAccumulator(StructuredQName name)
        {
            return accumulatorsByName.GetOrDefault(name);
        }

        public virtual ISequence GetStreamingAccumulatorValue(NodeInfo node, Accumulator accumulator, AccumulatorFn.Phase phase)
        {
            return null;
        }
    }
}
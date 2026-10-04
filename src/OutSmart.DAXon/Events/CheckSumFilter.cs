////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using System.IO;

namespace OutSmart.DAXon.Events
{
    internal sealed class CheckSumFilter : ProxyReceiver
    {
        public const string SIGMA = "Σ";
        public const string SIGMA2 = "Σ2";
        private static readonly bool DEBUG = false;
        private DigestMaker digest = null;
        private int checksum = 0;
        private int sequence = 0;
        private bool checkExistingChecksum = false;
        private bool checksumCorrect = false;
        private bool checksumFound = false;
        private bool digestCorrect = false;
        private bool digestFound = false;
        private bool requireDigest = false;
        private bool rootElement = true;
        private int depth = 0;
        private string target = "unknown";

        //
        public int Checksum => checksum;

        //
        public string Digest => digest.Digest;
        public CheckSumFilter(IReceiver nextReceiver) : base(nextReceiver)
        {
            rootElement = true;
            digest = new DigestMaker();
        }

        public void SetCheckExistingChecksum(bool check)
        {
            this.checkExistingChecksum = check;
        }

        private static void Trace(string message)
        {
            if (DEBUG)
            {
                Console.Error.WriteLine(message);
            }
        }

        public override void StartDocument(int properties)
        {
            Trace("CHECKSUM - START DOC");
            base.StartDocument(properties);
        }

        public override void EndDocument()
        {
            Trace("Σ ::= " + (checksum).ToString("x"));
            nextReceiver.EndDocument();
        }

        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            checksum ^= Hash(item.ToString(), sequence++);
            Trace("After append: " + (checksum).ToString("x"));
            base.Append(item, locationId, copyNamespaces);
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!Whitespace.IsAllWhite(chars))
            {
                checksum ^= Hash(chars.ToString(), sequence++);
                Trace("After characters " + chars + ": " + (checksum).ToString("x"));
            }

            base.Characters(chars, locationId, properties);
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            checksum ^= Hash(elemName, sequence++);
            Trace("After startElement " + elemName.DisplayName + ": " + checksum);
            checksumCorrect = false;
            depth++;
            if (rootElement)
            {
                rootElement = false;
                bool scm_schema = elemName.GetNamespaceUri() == NamespaceUri.Of(NamespaceConstant.SCM) && elemName.GetLocalPart() == "schema";

                // A digest is required for version 12.5+
                string version = attributes.GetValue("saxonVersion");

                // No sneaky deleting the version to avoid the check. Except SCM files don't have a version...
                requireDigest = version == null && !scm_schema;
                if (version != null)
                {
                    string minorVersion = "x"; // cause number format exception
                    int dpos = version.IndexOf('.');
                    if (dpos > 0)
                    {
                        minorVersion = version.Substring(dpos + 1);
                        version = version.Substring(0, dpos);
                    }

                    try
                    {
                        int majorVersion = int.Parse(version);
                        if (majorVersion > 12)
                        {
                            requireDigest = true;
                        }
                        else if (majorVersion == 12)
                        {
                            requireDigest = int.Parse(minorVersion) >= 5;
                        }
                    }
                    catch (FormatException e)
                    {
                        requireDigest = true;
                    }
                }

                target = attributes.GetValue("target");
                if (target == null)
                {
                    target = "unknown";
                }
            }


            // Need these in lexicographic order for the cryptographic hash.
            // I also want to assure that the current checksum works. And I want
            // to use the hash() function as the common place for collecting data
            // for both. That means a bit of fiddling around here (extra fiddling
            // because INodeName doesn't implement Comparable).
            Dictionary<string, INodeName> namemap = new Dictionary<string, INodeName>();
            Dictionary<string, string> attrmap = new Dictionary<string, string>();
            string[] names = new string[attributes.Size()];
            int index = 0;
            foreach (AttributeInfo att in attributes)
            {
                string key = att.GetNodeName().GetLocalPart() + att.GetNodeName().GetNamespaceUri();
                attrmap[key] = att.Value;
                namemap[key] = att.GetNodeName();
                names[index++] = key;
            }

            Array.Sort(names, StringComparer.Ordinal);
            foreach (string key in names)
            {
                INodeName name = namemap.GetOrDefault(key);
                string value = attrmap.GetOrDefault(key);
                checksum ^= Hash(name, sequence);
                Trace("After attribute name " + name.DisplayName + ": " + checksum);
                checksum ^= Hash(value, sequence);
                Trace("After attribute value " + name.DisplayName + ": " + checksum);
            }

            base.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        public override void EndElement()
        {
            depth--;
            if (depth == 0 && target.StartsWith("JS", StringComparison.Ordinal))
            {

                // We've reached the end. First calculate the SIGMA2 hash, and then
                // update the checksum with a checksum of the
                // SIGMA2 hash (because that's what SaxonJS2 is going to have done).
                // The sequence is 1 when SIGMA2 is seen by the JS2 verifier.
                //
                // This only applies when the output is JSON because when the output
                // is XML, the checksums are stored in processing instructions and
                // are always ignored by the verifier.
                string sigma2Hash = Digest; // the digest changes with subsequent hash calls, so fix it first
                checksum ^= Hash(SIGMA2, 1);
                checksum ^= Hash("", 1); // SIGMA2 @is in no namespace
                checksum ^= Hash(sigma2Hash, 1);
                Trace("After SIGMA2: " + checksum);
            }

            checksum ^= 1;
            Trace("After endElement: " + checksum);
            base.EndElement();
        }

        //
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (target.Equals(SIGMA))
            {
                checksumFound = true;
                if (checkExistingChecksum)
                {
                    try
                    {
                        int found = (int)Convert.ToInt64("0" + data, 16);
                        checksumCorrect = found == checksum;
                    }
                    catch (FormatException e)
                    {
                        checksumCorrect = false;
                    }

                    if (data.ToString().Equals(Digest))
                    {

                        // This case represents some point in the future when we've
                        // abandoned the checksum and the digest is stored in SIGMA
                        digestFound = true;
                        digestCorrect = true;
                        checksumCorrect = true; // digest trumps checksum
                    }
                }
            }

            if (target.Equals(SIGMA2))
            {
                digestFound = true;
                if (checkExistingChecksum)
                {
                    digestCorrect = data.ToString().Equals(Digest);
                }
            }

            base.ProcessingInstruction(target, data, locationId, properties);
        }

        //
        public bool IsChecksumCorrect()
        {
            if (requireDigest && !digestCorrect)
            {
                return false;
            }

            return checksumCorrect || Environment.GetEnvironmentVariable("saxon-checksum") == "skip";
        }

        //
        private int Hash(string s, int sequence)
        {

            digest.Update(sequence);
            digest.Update(s);
            int h = sequence << 8;
            for (int i = 0; i < s.Length; i++)
            {
                h = (h << 1) + s[i];
            }

            return h;
        }

        //
        private int Hash(INodeName n, int sequence)
        {
            return Hash(n.GetLocalPart(), sequence) ^ Hash(n.GetNamespaceUri().ToString(), sequence);
        }
    }
}

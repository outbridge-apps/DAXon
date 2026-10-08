////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;

using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions
{
    public class PackageData
    {
        protected Configuration config;
        private HostLanguage hostLanguage;
        protected int hostLanguageVersion;
        private bool schemaAware;
        private DecimalFormatManager decimalFormatManager;
        protected KeyManager keyManager;
        private AccumulatorRegistry accumulatorRegistry;
        private readonly IList<GlobalVariable> globalVariables = new List<GlobalVariable>();
        private SlotManager globalSlotManager;
        private int localLicenseId = -1;
        private string targetEdition;
        private bool relocatable;
        private TypeAliasManager typeAliasManager;

        public virtual int HostLanguageVersion => hostLanguageVersion;

        public virtual int LocalLicenseId
        {
            get => localLicenseId; set
            {
                localLicenseId = value;
            }
        }

        public virtual string TargetEdition
        {
            get => this.targetEdition; set
            {
                this.targetEdition = value;
            }
        }

        public virtual AccumulatorRegistry AccumulatorRegistry
        {
            get => accumulatorRegistry; set
            {
                this.accumulatorRegistry = value;
            }
        }

        public virtual SlotManager GlobalSlotManager
        {
            get => globalSlotManager; set
            {
                this.globalSlotManager = value;
            }
        }

        public virtual IList<GlobalVariable> GlobalVariableList => globalVariables;
        public PackageData(Configuration config)
        {
            if (config == null)
            {
                throw new NullReferenceException();
            }

            this.config = config;
            targetEdition = config.EditionCode;
            globalSlotManager = config.MakeSlotManager();
            hostLanguage = HostLanguage.XPATH;
            hostLanguageVersion = 31;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void SetConfiguration(Configuration configuration)
        {
            this.config = configuration;
        }

        public virtual HostLanguage GetHostLanguage()
        {
            return hostLanguage;
        }

        public virtual bool IsXSLT()
        {
            return hostLanguage == HostLanguage.XSLT;
        }

        public virtual void SetHostLanguage(HostLanguage hostLanguage, int version)
        {
            this.hostLanguage = hostLanguage;
            this.hostLanguageVersion = version;
        }

        public virtual bool IsRelocatable()
        {
            return relocatable;
        }

        public virtual void SetRelocatable(bool relocatable)
        {
            this.relocatable = relocatable;
        }

        public virtual bool IsSchemaAware()
        {
            return schemaAware;
        }

        public virtual void SetSchemaAware(bool schemaAware)
        {
            this.schemaAware = schemaAware;
        }

        public virtual DecimalFormatManager GetDecimalFormatManager()
        {
            if (decimalFormatManager == null)
            {
                decimalFormatManager = new DecimalFormatManager(hostLanguage, 31);
            }

            return decimalFormatManager;
        }

        public virtual void SetDecimalFormatManager(DecimalFormatManager manager)
        {
            decimalFormatManager = manager;
        }

        public virtual KeyManager GetKeyManager()
        {
            if (keyManager == null)
            {
                keyManager = new KeyManager(GetConfiguration(), this);
            }

            return keyManager;
        }

        public virtual void SetKeyManager(KeyManager manager)
        {
            keyManager = manager;
        }

        public virtual void AddGlobalVariable(GlobalVariable variable)
        {
            globalVariables.Add(variable);
        }

        public virtual void SetTypeAliasManager(TypeAliasManager manager)
        {
            this.typeAliasManager = manager;
        }

        public virtual TypeAliasManager ObtainTypeAliasManager()
        {
            if (typeAliasManager == null)
            {
                typeAliasManager = config.MakeTypeAliasManager();
            }

            return typeAliasManager;
        }
    }
}
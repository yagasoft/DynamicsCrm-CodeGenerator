using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;

namespace CrmCodeGenerator.VSPackage.Model
{
	[Obsolete("Old Settings class used only for migration.", false)]
    [Serializable]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CrmEntityAttribute : Attribute
    {
        public string LogicalName { get; set; }

        public string PrimaryKey { get; set; }
    }

	[Obsolete("Old Settings class used only for migration.", false)]
    [Serializable]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CrmRelationshipAttribute : Attribute, ICloneable
    {
        public string FromEntity { get; set; }

        public string ToEntity { get; set; }

        public string FromKey { get; set; }

        public string ToKey { get; set; }

        public string IntersectingEntity { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

	[Obsolete("Old Settings class used only for migration.", false)]
    [Serializable]
    [AttributeUsage(AttributeTargets.Property)]
    public class CrmPropertyAttribute : Attribute
    {
        public string LogicalName { get; set; }
		public string SchemaName { get; set; }
		public bool IsLookup { get; set; }
		public bool IsMultiTyped { get; set; }
		public bool IsImage { get; set; }
        public bool IsEntityReferenceHelper { get; set; }
    }

	[Obsolete("Old Settings class used only for migration.", false)]
    [Serializable]
    [AttributeUsage(AttributeTargets.Field)]
    public class CrmPicklistAttribute : Attribute
    {
        public string DisplayName { get; set; }
        public int Value { get; set; }
		public LocalizedLabelSerialisable[] LocalizedLabels { get; set; }
    }

	[Obsolete("Old Settings class used only for migration.", false)]
	[Serializable]
	public class LocalizedLabelSerialisable
	{
		public int LanguageCode { get; set; }
		public string Label { get; set; }
	}

}

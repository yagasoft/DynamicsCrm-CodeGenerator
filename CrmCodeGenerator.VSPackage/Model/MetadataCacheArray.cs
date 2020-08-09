#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using Microsoft.Xrm.Sdk.Metadata;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	[Serializable]
	public class MetadataCacheArray
	{
		public string LatestUsedConnectionString;
		public IDictionary<string, MetadataCache> MetadataCaches;

		public MetadataCacheArray()
		{
			InitFields();
		}

		public void OnDeserialization()
		{
			InitFields();
		}

		private void InitFields()
		{
			MetadataCaches = new ConcurrentDictionary<string, MetadataCache>();
		}
	}
}

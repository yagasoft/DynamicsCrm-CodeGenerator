#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Cache
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

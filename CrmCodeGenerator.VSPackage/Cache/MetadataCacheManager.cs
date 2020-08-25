#region Imports

using Yagasoft.CrmCodeGenerator.Cache.Metadata;
using Yagasoft.CrmCodeGenerator.Models.Cache;

#endregion

namespace CrmCodeGenerator.VSPackage.Cache
{
	public class MetadataCacheManager : MetadataCacheManagerBase
	{
		protected override MetadataCacheArray GetCacheArray()
		{
			return Configuration.LoadCache();
		}
	}
}

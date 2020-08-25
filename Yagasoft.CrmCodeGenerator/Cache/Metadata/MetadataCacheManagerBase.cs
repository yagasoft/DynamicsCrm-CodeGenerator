using System.Collections.Generic;
using Yagasoft.CrmCodeGenerator.Models.Cache;

namespace Yagasoft.CrmCodeGenerator.Cache.Metadata
{
    public abstract class MetadataCacheManagerBase : ICacheManager<MetadataCache>
    {
	    protected static readonly object LockObj = new object();

	    public MetadataCache GetCache(string connectionString)
	    {
		    var cacheArray = GetCacheArray();
		    var caches = cacheArray.MetadataCaches;

		    MetadataCache cache;

		    lock (LockObj)
			{
				if (!caches.TryGetValue(connectionString, out cache))
				{
					caches[connectionString] = cache = new MetadataCache();
				}

				cacheArray.LatestUsedConnectionString = connectionString;
			}

		    return cache;
	    }

	    public MetadataCache Clear(string connectionString)
	    {
		    return GetCacheArray().MetadataCaches[connectionString] = new MetadataCache();
	    }

	    protected abstract MetadataCacheArray GetCacheArray();
    }
}

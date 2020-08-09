using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrmCodeGenerator.VSPackage.Model;

namespace CrmCodeGenerator.VSPackage.Helpers
{
    public static class MetadataCacheHelpers
    {
	    private static readonly object lockObj = new object();

	    public static MetadataCache GetMetadataCache(string connectionString)
	    {
		    var cacheArray = Configuration.LoadCache();
		    var caches = cacheArray.MetadataCaches;

		    MetadataCache cache;

		    lock (lockObj)
			{
				if (!caches.TryGetValue(connectionString, out cache))
				{
					caches[connectionString] = cache = new MetadataCache();
				}

				cacheArray.LatestUsedConnectionString = connectionString;
			}

		    return cache;
	    }

	    public static MetadataCache ClearMetadataCache(string connectionString)
	    {
		    var caches = Configuration.LoadCache().MetadataCaches;
		    return caches[connectionString] = new MetadataCache();
	    }
    }
}

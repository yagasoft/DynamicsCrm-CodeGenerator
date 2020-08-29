#region Imports

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Yagasoft.CrmCodeGenerator.Helpers.Assembly;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.Libraries.Common;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	public static class CacheHelpers
	{
		private static readonly object lockObj = new object();
		public const string MetaCacheMemKey = "ys_CrmGen_Meta_639156";

		public static MetadataCache LoadCache(Guid settingsId, string baseCachePath, Action<string> logger)
		{
			var cacheKey = $"{MetaCacheMemKey}_{settingsId}";

			lock (lockObj)
			{
				try
				{
					var cache = Libraries.Common.CacheHelpers.GetFromMemCache<MetadataCache>(cacheKey);

					if (cache != null)
					{
						return cache;
					}

					logger("[Cache] Loading cache ... ");

					var file = $@"{baseCachePath}\{settingsId.ToString().ToLower()}";

					if (File.Exists(file))
					{
						//Open the file written above and read values from it.
						using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
						{
							var bformatter = new BinaryFormatter { Binder = new Binder() };
							stream.Position = 0;
							cache = bformatter.Deserialize(stream) as MetadataCache;

							if (cache == null)
							{
								throw new Exception("Invalid settings format.");
							}

							Libraries.Common.CacheHelpers.AddToMemCache(cacheKey, cache);

							logger("[Cache] [DONE] Finished loading cache.");

							return cache;
						}
					}

					logger("!! [Cache] ![ERROR]! cache file does not exist.");
				}
				catch (Exception ex)
				{
					logger("!! [Cache] ![ERROR]! Failed to read cache => " + ex.BuildExceptionMessage(isUseExStackTrace: true));
				}

				return Libraries.Common.CacheHelpers.AddToMemCache(cacheKey, new MetadataCache());
			}
		}

		public static void SaveCache(Guid settingsId, string baseCachePath, Action<string> logger)
		{
			SaveCache(LoadCache(settingsId, baseCachePath, logger), settingsId, baseCachePath, logger);
		}

		public static void SaveCache(MetadataCache metadataCache, Guid settingsId, string baseCachePath, Action<string> logger)
		{
			logger("[Cache] Writing cache ... ");

			var file = $@"{baseCachePath}\{settingsId.ToString().ToLower()}";

			lock (lockObj)
			{
				try
				{
					if (!File.Exists(file))
					{
						File.Create(file).Dispose();
						logger($"[Cache] Created a new cache file: '{file}'.");
					}

					new Thread(
						() =>
						{
							try
							{
								logger("[Cache] Moved write operation to a new thread.");

								using (var stream = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
								{
									// clear the file to start from scratch
									stream.SetLength(0);

									var bformatter = new BinaryFormatter { Binder = new Binder() };
									bformatter.Serialize(stream, metadataCache);

									logger("[Cache] [DONE] Finished writing cache.");
								}
							}
							catch (Exception ex)
							{
								logger("!! [Cache] ![ERROR]! Failed to write cache => " + ex.BuildExceptionMessage(isUseExStackTrace: true));
							}
						}).Start();
				}
				catch (Exception ex)
				{
					logger("!! [Cache] ![ERROR]! Failed to write cache => " + ex.BuildExceptionMessage(isUseExStackTrace: true));
				}
			}
		}
	}
}

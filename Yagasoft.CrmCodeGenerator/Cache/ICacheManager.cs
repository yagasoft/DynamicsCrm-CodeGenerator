namespace Yagasoft.CrmCodeGenerator.Cache
{
	public interface ICacheManager<out TCache>
	{
		/// <summary>
		///     Gets the specific cache by its key.
		/// </summary>
		TCache GetCache(string key);

		/// <summary>
		///     Clears the cache by its key and returns a new cache object.
		/// </summary>
		TCache Clear(string key);
	}
}

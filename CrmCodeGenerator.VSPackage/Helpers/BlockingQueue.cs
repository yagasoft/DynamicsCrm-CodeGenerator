using System.Collections.Concurrent;


namespace LinkDev.WebService.LogQueue
{
	// credits: http://joe-bq-wang.iteye.com/blog/1878940
	public class BlockingQueue<T> : BlockingCollection<T>
	{
		#region ctor(s)
		public BlockingQueue()
			: base(new ConcurrentQueue<T>())
		{
		}

		public BlockingQueue(int maxSize)
			: base(new ConcurrentQueue<T>(), maxSize)
		{
		}
		#endregion ctor(s)

		#region Methods
		/// <summary>  
		/// Enqueue an Item  
		/// </summary>  
		/// <param name="item">Item to enqueue</param>  
		/// <remarks>blocks if the blocking queue is full</remarks>  
		public void Enqueue(T item)
		{
			Add(item);
		}

		/// <summary>  
		/// Dequeue an item  
		/// </summary>  
		/// <param name="Item"></param>  
		/// <returns>Item dequeued</returns>  
		/// <remarks>blocks if the blocking queue is empty</remarks>  
		public T Dequeue()
		{
			return Take();
		}
		#endregion Methods
	}
}
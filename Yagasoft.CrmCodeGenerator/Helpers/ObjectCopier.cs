#region File header

// Project / File: Yagasoft.CrmCodeGenerator / ObjectCopier.cs
//         Author: Ahmed Elsawalhy
//   Contributors:
//        Created: 2020 / 08 / 29
//       Modified: 2020 / 08 / 29

#endregion

#region Imports

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Yagasoft.CrmCodeGenerator.Helpers.Assembly;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	/// <summary>
	///     Reference Article http://www.codeproject.com/KB/tips/SerializedObjectCloner.aspx
	///     Provides a method for performing a deep copy of an object.
	///     Binary Serialization is used to perform the copy.
	/// </summary>
	public static class ObjectCopier
	{
		/// <summary>
		///     Perform a deep Copy of the object.
		/// </summary>
		/// <typeparam name="T">The type of object being copied.</typeparam>
		/// <param name="source">The object instance to copy.</param>
		/// <returns>The copied object.</returns>
		public static T Clone<T>(this T source) where T : ISerializable
		{
			if (source == null)
			{
				return default;
			}

			IFormatter formatter = new BinaryFormatter { Binder = new Binder() };
			Stream stream = new MemoryStream();

			using (stream)
			{
				formatter.Serialize(stream, source);
				stream.Seek(0, SeekOrigin.Begin);
				return (T)formatter.Deserialize(stream);
			}
		}
	}
}

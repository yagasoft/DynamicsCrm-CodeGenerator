#region Imports

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Xml;
using Microsoft.Xrm.Sdk.Metadata;
using Yagasoft.CrmCodeGenerator.Models.Attributes;

namespace Yagasoft.CrmCodeGenerator.Helpers
{

	#endregion

	public class Naming
	{
		public static string Clean(string p, bool isTitleCase = false)
		{
			var result = "";

			if (string.IsNullOrEmpty(p))
			{
				return result;
			}

			p = p.Trim();
			p = Normalize(p);

			if (!string.IsNullOrEmpty(p))
			{
				var sb = new StringBuilder();
				if (!char.IsLetter(p[0]))
				{
					sb.Append("_");
				}

				foreach (var character in p)
				{
					if ((char.IsDigit(character) || char.IsLetter(character) || character == '_')
						&& !string.IsNullOrEmpty(character.ToString()))
					{
						sb.Append(character);
					}
				}

				result = sb.ToString();
			}

			var arabicChars = new[]
							  {
								  "ذ", "ض", "ص", "ث", "ق", "ف", "غ", "ع", "ه", "خ", "ح", "ج"
								  , "ش", "س", "ي", "ب", "ل", "ا", "ت", "ن", "م", "ك", "ط"
								  , "ئ", "ء", "ؤ", "ر", "لا", "ى", "ة", "و", "ز", "ظ"
								  , "لإ", "إ", "أ", "لأ", "لآ", "آ"
							  };
			var correspondingEnglishChars = new[]
											{
												"z", "d", "s", "s", "k", "f", "gh", "a", "h", "kh", "h", "g"
												, "sh", "s", "y", "b", "l", "a", "t", "n", "m", "k", "t"
												, "ea", "a", "oa", "r", "la", "y", "t", "o", "th", "z"
												, "la", "e", "a", "la", "la", "a"
											};

			result = Regex.Replace(result, "[^A-Za-z0-9]"
				, match =>
				  {
					  return match.Success
						  ? match.Value.Select(character =>
											   {
												   var index = Array.IndexOf(arabicChars, character.ToString());
												   return index < 0 ? "_" : correspondingEnglishChars[index];
											   })
							  .Aggregate((char1, char2) => char1 + char2)
						  : "";
				  });

			result = Capitalize(result, isTitleCase);

			result = Regex.Replace(result, "[^A-Za-z0-9_]", "_");

			if (!char.IsLetter(result[0]) && result[0] != '_')
			{
				result = "_" + result;
			}

			result = ReplaceKeywords(result);

			//result = result.Substring(0, 1) + result.Substring(1).Replace("_", "");

			return result;
		}

		private static string Normalize(string regularString)
		{
			var normalizedString = regularString.Normalize(NormalizationForm.FormD);

			var sb = new StringBuilder(normalizedString);

			for (var i = 0; i < sb.Length; i++)
			{
				if (CharUnicodeInfo.GetUnicodeCategory(sb[i]) == UnicodeCategory.NonSpacingMark)
				{
					sb.Remove(i, 1);
				}
			}
			regularString = sb.ToString();

			return regularString.Replace("æ", "");
		}


		private static string ReplaceKeywords(string p)
		{
			if (p.Equals("public", StringComparison.InvariantCulture)
				|| p.Equals("private", StringComparison.InvariantCulture)
				|| p.Equals("event", StringComparison.InvariantCulture)
				|| p.Equals("single", StringComparison.InvariantCulture)
				|| p.Equals("new", StringComparison.InvariantCulture)
				|| p.Equals("partial", StringComparison.InvariantCulture)
				|| p.Equals("to", StringComparison.InvariantCulture)
				|| p.Equals("error", StringComparison.InvariantCulture)
				|| p.Equals("readonly", StringComparison.InvariantCulture)
				|| p.Equals("case", StringComparison.InvariantCulture)
				|| p.Equals("object", StringComparison.InvariantCulture)
				|| p.Equals("global", StringComparison.InvariantCulture)
				|| p.Equals("namespace", StringComparison.InvariantCulture)
				|| p.Equals("abstract", StringComparison.InvariantCulture))
			{
				return "_" + p;
			}

			return p;
		}


		public static string CapitalizeWord(string p)
		{
			if (string.IsNullOrWhiteSpace(p))
			{
				return "";
			}

			return p.Substring(0, 1).ToUpper() + p.Substring(1);
		}

		private static string DecapitalizeWord(string p)
		{
			if (string.IsNullOrWhiteSpace(p))
			{
				return "";
			}

			return p.Substring(0, 1).ToLower() + p.Substring(1);
		}

		public static string Capitalize(string p, bool capitalizeFirstWord)
		{
			var parts = p.Split(' ');

			for (var i = 0; i < parts.Length; i++)
			{
				parts[i] = i != 0 || capitalizeFirstWord ? CapitalizeWord(parts[i]) : parts[i];
			}

			return string.Join(" ", parts);
		}

		public static string GetProperEntityName(string entityName)
		{
			return Clean(Capitalize(entityName, true));
		}

		public static string GetProperHybridName(string displayName, string logicalName)
		{
			if (logicalName.Contains("_"))
			{
				Console.WriteLine(displayName + " " + logicalName);
				return displayName;
			}
			else
			{
				return Clean(Capitalize(displayName, true));
			}
		}

		public static string GetProperHybridFieldName(string displayName, CrmPropertyAttribute attribute)
		{
			if (attribute != null && attribute.LogicalName.Contains("_"))
			{
				return attribute.LogicalName;
			}
			else
			{
				return displayName;
			}
		}

		public static string GetProperVariableName(AttributeMetadata attribute, bool isTitleCase)
		{
			// Normally we want to use the SchemaName as it has the capitalized names (Which is what CrmSvcUtil.exe does).  
			// HOWEVER, If you look at the 'annual' attributes on the annualfiscalcalendar you see it has schema name of Period1  
			// So if the logicalname & schema name don't match use the logical name and try to capitalize it 
			// EXCEPT,  when it's RequiredAttendees/From/To/Cc/Bcc/SecondHalf/FirstHalf  (i have no idea how CrmSvcUtil knows to make those upper case)
			if (attribute.LogicalName == "requiredattendees")
			{
				return "RequiredAttendees";
			}
			if (attribute.LogicalName == "from")
			{
				return "From";
			}
			if (attribute.LogicalName == "to")
			{
				return "To";
			}
			if (attribute.LogicalName == "cc")
			{
				return "Cc";
			}
			if (attribute.LogicalName == "bcc")
			{
				return "Bcc";
			}
			if (attribute.LogicalName == "firsthalf")
			{
				return "FirstHalf";
			}
			if (attribute.LogicalName == "secondhalf")
			{
				return "SecondHalf";
			}
			if (attribute.LogicalName == "firsthalf_base")
			{
				return "FirstHalf_Base";
			}
			if (attribute.LogicalName == "secondhalf_base")
			{
				return "SecondHalf_Base";
			}
			if (attribute.LogicalName == "attributes")
			{
				return "Attributes1";
			}

			if (attribute.LogicalName.Equals(attribute.SchemaName, StringComparison.InvariantCultureIgnoreCase))
			{
				return Clean(isTitleCase
					? string.Join("_", Capitalize(attribute.SchemaName.Replace('_', ' '), true).Split(' '))
					: attribute.SchemaName);
			}

			return Clean(Capitalize(attribute.LogicalName, true));
		}

		public static string GetProperVariableName(string p, bool isTitleCase)
		{
			if (string.IsNullOrWhiteSpace(p))
			{
				return "Empty";
			}
			if (p == "Closed (deprecated)") //Invoice
			{
				return "Closed";
			}
			//return Clean(Capitalize(p, true));
			return Clean(isTitleCase
				? string.Join("_", Capitalize(p.Replace('_', ' '), true).Split(' '))
				: p, isTitleCase);
		}

		public static string GetPluralName(string p)
		{
			if (p.EndsWith("y"))
			{
				return p.Substring(0, p.Length - 1) + "ies";
			}

			if (p.EndsWith("s"))
			{
				return p;
			}

			return p + "s";
		}

		public static string GetEntityPropertyPrivateName(string p)
		{
			return "_" + Clean(Capitalize(p, false));
		}

		public static string XmlEscape(string unescaped)
		{
			var doc = new XmlDocument();
			XmlNode node = doc.CreateElement("root");
			node.InnerText = unescaped;
			return node.InnerXml;
		}
	}

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
		public static T Clone<T>(T source)
		{
			if (!typeof(T).IsSerializable)
			{
				throw new ArgumentException("The type must be serializable.", "source");
			}

			// Don't serialize a null object, simply return the default for that object
			if (ReferenceEquals(source, null))
			{
				return default(T);
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

	public class Binder : SerializationBinder
	{
		// credit: http://stackoverflow.com/a/18856352/1919456
		public override Type BindToType(string assemblyName, string typeName)
		{
			var currentAssemblyInfo = Assembly.GetExecutingAssembly().FullName;

			var currentAssemblyName = currentAssemblyInfo.Split(',')[0];

			if (assemblyName.StartsWith(currentAssemblyName))
			{
				assemblyName = currentAssemblyInfo;
			}

			return Type.GetType($"{typeName}, {assemblyName}");
		}
	}

	public class AssemblyHelpers
	{
		// credit: http://blog.slaks.net/2013-12-25/redirecting-assembly-loads-at-runtime/
		public static void RedirectAssembly(string shortName, Version targetVersion, string publicKeyToken)
		{
			Assembly Handler(object sender, ResolveEventArgs args)
			{
				// Use latest strong name & version when trying to load SDK assemblies
				var requestedAssembly = new AssemblyName(args.Name);

				if (requestedAssembly.Name != shortName && !requestedAssembly.FullName.Contains(shortName + ","))
				{
					return null;
				}

				Debug.WriteLine("Redirecting assembly load of " + args.Name + ",\tloaded by " + (args.RequestingAssembly == null ? "(unknown)" : args.RequestingAssembly.FullName));

				requestedAssembly.Version = targetVersion;
				requestedAssembly.SetPublicKeyToken(new AssemblyName("x, PublicKeyToken=" + publicKeyToken).GetPublicKeyToken());
				requestedAssembly.CultureInfo = CultureInfo.InvariantCulture;

				AppDomain.CurrentDomain.AssemblyResolve -= Handler;

				var loadedAssembly = Assembly.Load(requestedAssembly);
				return loadedAssembly;
			}

			AppDomain.CurrentDomain.AssemblyResolve += Handler;
		}
	}

	//#region Imports

	//using System;
	//using System.Diagnostics;
	//using System.Globalization;
	//using System.IO;
	//using System.Linq;
	//using System.Reflection;
	//using System.Runtime.Serialization;
	//using System.Runtime.Serialization.Formatters.Binary;
	//using System.Text;
	//using System.Text.RegularExpressions;
	//using System.Xml;
	//using CrmCodeGenerator.VSPackage.Model;
	//using Microsoft.Xrm.Sdk.Metadata;

	//#endregion

	//namespace CrmCodeGenerator.VSPackage.Helpers
	//{
	//	public class Naming
	//	{
	//		public static string Clean(string p)
	//		{
	//			var result = "";

	//			if (string.IsNullOrEmpty(p))
	//			{
	//				return result;
	//			}

	//			result = Normalize(p.Trim());

	//			var arabicChars = new[]
	//			                  {
	//				                  "ذ", "ض", "ص", "ث", "ق", "ف", "غ", "ع", "ه", "خ", "ح", "ج"
	//				                  , "ش", "س", "ي", "ب", "ل", "ا", "ت", "ن", "م", "ك", "ط"
	//				                  , "ئ", "ء", "ؤ", "ر", "لا", "ى", "ة", "و", "ز", "ظ"
	//				                  , "لإ", "إ", "أ", "لأ", "لآ", "آ", " "
	//			                  };
	//			var correspondingEnglishChars = new[]
	//			                                {
	//				                                "z", "d", "s", "s", "k", "f", "gh", "3", "h", "kh", "7", "g"
	//				                                , "sh", "s", "y", "b", "l", "a", "t", "n", "m", "k", "t"
	//				                                , "y2", "2", "o2", "r", "la", "y", "t", "o", "z", "z"
	//				                                , "la2e", "2e", "2a", "la2a", "la2a", "2a", " "
	//			                                };

	//			result = Regex.Replace(result, "[^A-Za-z0-9_]"
	//				, match =>
	//				  {
	//					  return match.Success
	//						         ? match.Value.Select(character =>
	//						                              {
	//							                              var index = Array.IndexOf(arabicChars, character.ToString());
	//							                              return index < 0 ? "_" : correspondingEnglishChars[index];
	//						                              })
	//							           .Aggregate((char1, char2) => char1 + char2)
	//						         : match.Value;
	//				  });

	//			result = Capitalize(result, false);

	//			result = result.Replace(" ", "");
	//			result = Regex.Replace(result, "[^A-Za-z0-9_]", "_");

	//			if (!char.IsLetter(result[0]) && result[0] != '_')
	//			{
	//				result = "_" + result;
	//			}

	//			result = ReplaceKeywords(result);

	//			//result = result.Substring(0, 1) + result.Substring(1).Replace("_", "");

	//			return result;
	//		}

	//		private static string Normalize(string regularString)
	//		{
	//			var normalizedString = regularString.Normalize(NormalizationForm.FormD);

	//			var sb = new StringBuilder(normalizedString);

	//			for (var i = 0; i < sb.Length; i++)
	//			{
	//				if (CharUnicodeInfo.GetUnicodeCategory(sb[i]) == UnicodeCategory.NonSpacingMark)
	//				{
	//					sb.Remove(i, 1);
	//				}
	//			}
	//			regularString = sb.ToString();

	//			return regularString.Replace("æ", "");
	//		}


	//		private static string ReplaceKeywords(string p)
	//		{
	//			if (p.Equals("public", StringComparison.InvariantCulture)
	//			    || p.Equals("private", StringComparison.InvariantCulture)
	//			    || p.Equals("event", StringComparison.InvariantCulture)
	//			    || p.Equals("single", StringComparison.InvariantCulture)
	//			    || p.Equals("new", StringComparison.InvariantCulture)
	//			    || p.Equals("partial", StringComparison.InvariantCulture)
	//			    || p.Equals("to", StringComparison.InvariantCulture)
	//			    || p.Equals("error", StringComparison.InvariantCulture)
	//			    || p.Equals("readonly", StringComparison.InvariantCulture)
	//			    || p.Equals("case", StringComparison.InvariantCulture)
	//			    || p.Equals("object", StringComparison.InvariantCulture)
	//			    || p.Equals("global", StringComparison.InvariantCulture)
	//				|| p.Equals("namespace", StringComparison.InvariantCulture)
	//				|| p.Equals("abstract", StringComparison.InvariantCulture))
	//			{
	//				return "_" + p;
	//			}

	//			return p;
	//		}


	//		public static string CapitalizeWord(string p)
	//		{
	//			if (string.IsNullOrWhiteSpace(p))
	//			{
	//				return "";
	//			}

	//			return p.Substring(0, 1).ToUpper() + p.Substring(1);
	//		}

	//		private static string DecapitalizeWord(string p)
	//		{
	//			if (string.IsNullOrWhiteSpace(p))
	//			{
	//				return "";
	//			}

	//			return p.Substring(0, 1).ToLower() + p.Substring(1);
	//		}

	//		public static string Capitalize(string p, bool capitalizeFirstWord)
	//		{
	//			var parts = p.Split(' ');

	//			for (var i = 0; i < parts.Length; i++)
	//			{
	//				parts[i] = i != 0 || capitalizeFirstWord ? CapitalizeWord(parts[i]) : parts[i];
	//			}

	//			return string.Join(" ", parts);
	//		}

	//		public static string GetProperEntityName(string entityName)
	//		{
	//			return Clean(Capitalize(entityName, true));
	//		}

	//		public static string GetProperHybridName(string displayName, string logicalName)
	//		{
	//			if (logicalName.Contains("_"))
	//			{
	//				Console.WriteLine(displayName + " " + logicalName);
	//				return displayName;
	//			}
	//			else
	//			{
	//				return Clean(Capitalize(displayName, true));
	//			}
	//		}

	//		public static string GetProperHybridFieldName(string displayName, CrmPropertyAttribute attribute)
	//		{
	//			if (attribute != null && attribute.LogicalName.Contains("_"))
	//			{
	//				return attribute.LogicalName;
	//			}
	//			else
	//			{
	//				return displayName;
	//			}
	//		}

	//		public static string GetProperVariableName(AttributeMetadata attribute)
	//		{
	//			// Normally we want to use the SchemaName as it has the capitalized names (Which is what CrmSvcUtil.exe does).  
	//			// HOWEVER, If you look at the 'annual' attributes on the annualfiscalcalendar you see it has schema name of Period1  
	//			// So if the logicalname & schema name don't match use the logical name and try to capitalize it 
	//			// EXCEPT,  when it's RequiredAttendees/From/To/Cc/Bcc/SecondHalf/FirstHalf  (i have no idea how CrmSvcUtil knows to make those upper case)
	//			if (attribute.LogicalName == "requiredattendees")
	//			{
	//				return "RequiredAttendees";
	//			}
	//			if (attribute.LogicalName == "from")
	//			{
	//				return "From";
	//			}
	//			if (attribute.LogicalName == "to")
	//			{
	//				return "To";
	//			}
	//			if (attribute.LogicalName == "cc")
	//			{
	//				return "Cc";
	//			}
	//			if (attribute.LogicalName == "bcc")
	//			{
	//				return "Bcc";
	//			}
	//			if (attribute.LogicalName == "firsthalf")
	//			{
	//				return "FirstHalf";
	//			}
	//			if (attribute.LogicalName == "secondhalf")
	//			{
	//				return "SecondHalf";
	//			}
	//			if (attribute.LogicalName == "firsthalf_base")
	//			{
	//				return "FirstHalf_Base";
	//			}
	//			if (attribute.LogicalName == "secondhalf_base")
	//			{
	//				return "SecondHalf_Base";
	//			}
	//			if (attribute.LogicalName == "attributes")
	//			{
	//				return "Attributes1";
	//			}

	//			if (attribute.LogicalName.Equals(attribute.SchemaName, StringComparison.InvariantCultureIgnoreCase))
	//			{
	//				return Clean(attribute.SchemaName);
	//			}

	//			return Clean(attribute.LogicalName);
	//		}

	//		public static string GetProperVariableName(string p)
	//		{
	//			if (string.IsNullOrWhiteSpace(p))
	//			{
	//				return "Empty";
	//			}
	//			if (p == "Closed (deprecated)") //Invoice
	//			{
	//				return "Closed";
	//			}
	//			//return Clean(Capitalize(p, true));
	//			return Clean(p);
	//		}

	//		public static string GetPluralName(string p)
	//		{
	//			if (p.EndsWith("y"))
	//			{
	//				return p.Substring(0, p.Length - 1) + "ies";
	//			}

	//			if (p.EndsWith("s"))
	//			{
	//				return p;
	//			}

	//			return p + "s";
	//		}

	//		public static string GetEntityPropertyPrivateName(string p)
	//		{
	//			return "_" + Clean(Capitalize(p, false));
	//		}

	//		public static string XmlEscape(string unescaped)
	//		{
	//			var doc = new XmlDocument();
	//			XmlNode node = doc.CreateElement("root");
	//			node.InnerText = unescaped;
	//			return node.InnerXml;
	//		}
	//	}

	//	/// <summary>
	//	///     Reference Article http://www.codeproject.com/KB/tips/SerializedObjectCloner.aspx
	//	///     Provides a method for performing a deep copy of an object.
	//	///     Binary Serialization is used to perform the copy.
	//	/// </summary>
	//	public static class ObjectCopier
	//	{
	//		/// <summary>
	//		///     Perform a deep Copy of the object.
	//		/// </summary>
	//		/// <typeparam name="T">The type of object being copied.</typeparam>
	//		/// <param name="source">The object instance to copy.</param>
	//		/// <returns>The copied object.</returns>
	//		public static T Clone<T>(T source)
	//		{
	//			if (!typeof (T).IsSerializable)
	//			{
	//				throw new ArgumentException("The type must be serializable.", "source");
	//			}

	//			// Don't serialize a null object, simply return the default for that object
	//			if (ReferenceEquals(source, null))
	//			{
	//				return default(T);
	//			}

	//			IFormatter formatter = new BinaryFormatter { Binder = new Binder() };
	//			Stream stream = new MemoryStream();
	//			using (stream)
	//			{
	//				formatter.Serialize(stream, source);
	//				stream.Seek(0, SeekOrigin.Begin);
	//				return (T) formatter.Deserialize(stream);
	//			}
	//		}
	//	}

	//	public class Binder : SerializationBinder
	//	{
	//		// credit: http://stackoverflow.com/a/18856352/1919456
	//		public override Type BindToType(string assemblyName, string typeName)
	//		{
	//			var currentAssemblyInfo = Assembly.GetExecutingAssembly().FullName;

	//			var currentAssemblyName = currentAssemblyInfo.Split(',')[0];

	//			if (assemblyName.StartsWith(currentAssemblyName))
	//			{
	//				assemblyName = currentAssemblyInfo;
	//			}

	//			// backward compatibility
	//			if (assemblyName.Contains("6.0.0.0") && assemblyName.Contains("Microsoft.Xrm.Sdk"))
	//			{
	//				assemblyName = assemblyName.Split(',')[0];
	//			}

	//			return Type.GetType($"{typeName}, {assemblyName}");
	//		}
	//	}
	//}

	public static class ObjectExtensions
	{
		private static readonly MethodInfo CloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

		public static bool IsPrimitive(this Type type)
		{
			if (type == typeof(String)) return true;
			return (type.IsValueType & type.IsPrimitive);
		}

		public static Object Copy(this Object originalObject)
		{
			return InternalCopy(originalObject, new Dictionary<Object, Object>(new ReferenceEqualityComparer()));
		}
		private static Object InternalCopy(Object originalObject, IDictionary<Object, Object> visited)
		{
			if (originalObject == null) return null;
			var typeToReflect = originalObject.GetType();
			if (IsPrimitive(typeToReflect)) return originalObject;
			if (visited.ContainsKey(originalObject)) return visited[originalObject];
			if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;
			var cloneObject = CloneMethod.Invoke(originalObject, null);
			if (typeToReflect.IsArray)
			{
				var arrayType = typeToReflect.GetElementType();
				if (IsPrimitive(arrayType) == false)
				{
					Array clonedArray = (Array)cloneObject;
					clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
				}

			}
			visited.Add(originalObject, cloneObject);
			CopyFields(originalObject, visited, cloneObject, typeToReflect);
			RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);
			return cloneObject;
		}

		private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect)
		{
			if (typeToReflect.BaseType != null)
			{
				RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
				CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
			}
		}

		private static void CopyFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null)
		{
			foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags))
			{
				if (filter != null && filter(fieldInfo) == false) continue;
				if (IsPrimitive(fieldInfo.FieldType)) continue;
				var originalFieldValue = fieldInfo.GetValue(originalObject);
				var clonedFieldValue = InternalCopy(originalFieldValue, visited);
				fieldInfo.SetValue(cloneObject, clonedFieldValue);
			}
		}
		public static T Copy<T>(this T original)
		{
			return (T)Copy((Object)original);
		}
	}

	public class ReferenceEqualityComparer : EqualityComparer<object>
	{
		public override bool Equals(object x, object y)
		{
			return ReferenceEquals(x, y);
		}
		public override int GetHashCode(object obj)
		{
			if (obj == null) return 0;
			return obj.GetHashCode();
		}
	}

	public static class ArrayExtensions
	{
		public static void ForEach(this Array array, Action<Array, int[]> action)
		{
			if (array.LongLength == 0) return;
			ArrayTraverse walker = new ArrayTraverse(array);
			do action(array, walker.Position);
			while (walker.Step());
		}
	}

	internal class ArrayTraverse
	{
		public int[] Position;
		private int[] maxLengths;

		public ArrayTraverse(Array array)
		{
			maxLengths = new int[array.Rank];
			for (int i = 0; i < array.Rank; ++i)
			{
				maxLengths[i] = array.GetLength(i) - 1;
			}
			Position = new int[array.Rank];
		}

		public bool Step()
		{
			for (int i = 0; i < Position.Length; ++i)
			{
				if (Position[i] < maxLengths[i])
				{
					Position[i]++;
					for (int j = 0; j < i; j++)
					{
						Position[j] = 0;
					}
					return true;
				}
			}
			return false;
		}
	}
}
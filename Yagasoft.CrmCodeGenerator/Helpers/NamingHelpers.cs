#region Imports

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Xrm.Sdk.Metadata;
using Yagasoft.CrmCodeGenerator.Models.Attributes;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	public class Naming
	{
		static string[][] replacementStrings;
		public static string[][] ReplacemenStrings
        {
			get 
			{
				return replacementStrings ?? new string[0][];
			}
			set => replacementStrings = value;
        }
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

			foreach(string[] replacementPair in replacementStrings)
            {
				if (replacementStrings.Length != 2)
					continue;
				result = result.Replace(replacementPair[0], replacementPair[1]);
            }

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
}

//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace CrmCodeGenerator.VSPackage.Helpers
//{
//	public static class NamingHelper
//	{
//		public static string Clean(string p)
//		{
//			var result = "";

//			if (string.IsNullOrEmpty(p))
//			{
//				return result;
//			}

//			p = p.Trim();
//			p = Normalize(p);

//			if (!string.IsNullOrEmpty(p))
//			{
//				var sb = new StringBuilder();
//				if (!char.IsLetter(p[0]))
//				{
//					sb.Append("_");
//				}

//				foreach (var character in p)
//				{
//					if ((char.IsDigit(character) || char.IsLetter(character) || character == '_') &&
//						!string.IsNullOrEmpty(character.ToString()))
//					{
//						sb.Append(character);
//					}
//				}

//				result = sb.ToString();
//			}

//			result = ReplaceKeywords(result);

//			var arabicChars = new[]
//							{
//								"ذ", "ض", "ص", "ث", "ق", "ف", "غ", "ع", "ه", "خ", "ح", "ج"
//								, "ش", "س", "ي", "ب", "ل", "ا", "ت", "ن", "م", "ك", "ط"
//								, "ئ", "ء", "ؤ", "ر", "لا", "ى", "ة", "و", "ز", "ظ"
//								, "لإ", "إ", "أ", "لأ", "لآ", "آ"
//							};
//			var correspondingEnglishChars = new[]
//										{
//											"z", "d", "s", "s", "k", "f", "gh", "a", "h", "kh", "h", "g"
//											, "sh", "s", "y", "b", "l", "a", "t", "n", "m", "k", "t"
//											, "ea", "a", "oa", "r", "la", "y", "t", "o", "th", "z"
//											, "la", "e", "a", "la", "la", "a"
//										};

//			result = Regex.Replace(result, "[^A-Za-z0-9_]"
//				, match =>
//				{
//					return match.Success
//								? match.Value.Select(character =>
//								{
//									var index = Array.IndexOf(arabicChars, character.ToString());
//									return index < 0 ? "_" : correspondingEnglishChars[index];
//								})
//										.Aggregate((char1, char2) => char1 + char2)
//								: "";
//				});

//			result = Regex.Replace(result, "[^A-Za-z0-9_]", "_");

//			if (!char.IsLetter(result[0]) && result[0] != '_')
//			{
//				result = "_" + result;
//			}

//			result = Capitalize(result, true);
//			result = result.Substring(0, 1) + result.Substring(1).Replace("_", "");

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
//				|| p.Equals("private", StringComparison.InvariantCulture)
//				|| p.Equals("event", StringComparison.InvariantCulture)
//				|| p.Equals("single", StringComparison.InvariantCulture)
//				|| p.Equals("new", StringComparison.InvariantCulture)
//				|| p.Equals("partial", StringComparison.InvariantCulture)
//				|| p.Equals("to", StringComparison.InvariantCulture)
//				|| p.Equals("error", StringComparison.InvariantCulture)
//				|| p.Equals("readonly", StringComparison.InvariantCulture)
//				|| p.Equals("case", StringComparison.InvariantCulture)
//				|| p.Equals("object", StringComparison.InvariantCulture)
//				|| p.Equals("global", StringComparison.InvariantCulture)
//				|| p.Equals("namespace", StringComparison.InvariantCulture)
//				|| p.Equals("abstract", StringComparison.InvariantCulture))
//			{
//				return "_" + p;
//			}

//			return p;
//		}


//		private static string CapitalizeWord(string p)
//		{
//			if (string.IsNullOrWhiteSpace(p))
//			{
//				return "";
//			}

//			return p.Substring(0, 1).ToUpper() + p.Substring(1);
//		}

//		private static string Capitalize(string p, bool capitalizeFirstWord)
//		{
//			var parts = p.Split(' ', '_');

//			for (var i = 0; i < parts.Length; i++)
//			{
//				parts[i] = i != 0 || capitalizeFirstWord ? CapitalizeWord(parts[i]) : parts[i];
//			}

//			return string.Join("_", parts);
//		}
//	}
//}

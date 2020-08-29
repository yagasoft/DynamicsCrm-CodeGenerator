#region File header

// Project / File: Yagasoft.CrmCodeGenerator / TemplateHelpers.cs
//         Author: Ahmed Elsawalhy
//   Contributors:
//        Created: 2020 / 08 / 29
//       Modified: 2020 / 08 / 29

#endregion

using System.Text.RegularExpressions;
using Yagasoft.CrmCodeGenerator.Models;

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	public static class TemplateHelpers
	{
		public static TemplateInfo ParseTemplateInfo(string templateContent)
		{
			var groups = Regex.Match(templateContent, @">+.*?Template version.*?(\d+\.\d+.\d+).*?<+",
				RegexOptions.IgnoreCase).Groups;

			var info = new TemplateInfo();

			if (groups.Count >= 2)
			{
				info.DetectedTemplateVersion = groups[1].Value;
			}

			groups = Regex.Match(templateContent, @">+.*?MINIMUM COMPATIBLE VERSION.*?(\d+\.\d+.\d+).*?<+",
				RegexOptions.IgnoreCase).Groups;

			if (groups.Count >= 2)
			{
				info.DetectedMinAppVersion = groups[1].Value;
			}

			return info;
		}
	}
}

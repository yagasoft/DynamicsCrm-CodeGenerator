using System;
using System.Collections.Generic;
using System.Linq;
using Yagasoft.CrmCodeGenerator.Models.Settings;

namespace CrmCodeGenerator.VSPackage.ViewModels
{
	public class EntityProfileGridRow : EntityGridRow
	{
		public ClearModeEnumUi ValueClearMode
		{
			get => valueClearMode;
			set
			{
				valueClearMode = value;
				OnPropertyChanged();
			}
		}

		public IEnumerable<ClearModeEnumUi> ValueClearModes => new[] { ClearModeEnumUi.Default }.Union(
			Enum.GetValues(typeof(ClearModeEnum)).Cast<ClearModeEnumUi>());

		private ClearModeEnumUi valueClearMode;
	}
}
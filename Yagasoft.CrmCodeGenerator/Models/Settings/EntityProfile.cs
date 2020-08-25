#region File header

// Project / File: Yagasoft.CrmCodeGenerator / EntityProfile.cs
//         Author: Ahmed Elsawalhy
//   Contributors:
//        Created: 2020 / 08 / 22
//       Modified: 2020 / 08 / 22

#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Yagasoft.CrmCodeGenerator.Models.Settings
{
	[Serializable]
	public class EntityProfile : INotifyPropertyChanged
	{
		public string LogicalName { get; set; }

		public string EntityRename
		{
			get => entityRename;
			set
			{
				entityRename = string.IsNullOrEmpty(value) ? null : value;
				OnPropertyChanged();
			}
		}

		public bool IsApplyToCrm
		{
			get => isApplyToCrm;
			set
			{
				isApplyToCrm = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateMeta
		{
			get => isGenerateMeta;
			set
			{
				isGenerateMeta = value;
				OnPropertyChanged();
			}
		}

		public bool IsOptionsetLabels
		{
			get => isOptionsetLabels;
			set
			{
				isOptionsetLabels = value;
				OnPropertyChanged();
			}
		}

		public bool IsLookupLabels
		{
			get => isLookupLabels;
			set
			{
				isLookupLabels = value;
				OnPropertyChanged();
			}
		}

		public ClearModeEnum? ValueClearMode
		{
			get => valueClearMode;
			set
			{
				valueClearMode = value;
				OnPropertyChanged();
			}
		}

		public bool IsExcluded
		{
			get => isExcluded;
			set
			{
				isExcluded = value;
				OnPropertyChanged();
			}
		}

		public string EnglishLabelField
		{
			get => englishLabelField;
			set
			{
				englishLabelField = value;
				OnPropertyChanged();
			}
		}

		public bool IsFiltered => !string.IsNullOrEmpty(EntityRename)
			|| Attributes.Length > 0 || OneToN.Length > 0 || NToOne.Length > 0 || NToN.Length > 0;

		public string[] Attributes { get; set; } = new string[0];
		public IDictionary<string, string> AttributeRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, string> AttributeLanguages { get; set; } = new Dictionary<string, string>();
		public string[] ReadOnly { get; set; } = new string[0];
		public string[] ClearFlag { get; set; } = new string[0];

		public string[] OneToN { get; set; } = new string[0];
		public IDictionary<string, string> OneToNRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, bool> OneToNReadOnly { get; set; } = new Dictionary<string, bool>();

		public string[] NToOne { get; set; } = new string[0];
		public IDictionary<string, string> NToOneRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, bool> NToOneFlatten { get; set; } = new Dictionary<string, bool>();
		public IDictionary<string, bool> NToOneReadOnly { get; set; } = new Dictionary<string, bool>();

		public string[] NToN { get; set; } = new string[0];
		public IDictionary<string, string> NToNRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, bool> NToNReadOnly { get; set; } = new Dictionary<string, bool>();

		public EntityProfile(string logicalName)
		{
			LogicalName = logicalName;
		}

		[field: NonSerialized]
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private string entityRename;
		private bool isExcluded = true;
		private bool isGenerateMeta;
		private bool isApplyToCrm;
		private bool isOptionsetLabels;
		private bool isLookupLabels;
		private ClearModeEnum? valueClearMode;
		private string englishLabelField;
	}
}

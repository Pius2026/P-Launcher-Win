using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PiusLauncher;

public sealed class WidgetTab : INotifyPropertyChanged
{
	private string _name;

	private bool _isSelected;

	private int _configuredCount;

	public int Index { get; }

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			SetField(ref _name, string.IsNullOrWhiteSpace(value) ? AppText.DefaultWidgetName(Index) : value.Trim(), "Name");
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			SetField(ref _isSelected, value, "IsSelected");
		}
	}

	public int ConfiguredCount
	{
		get
		{
			return _configuredCount;
		}
		set
		{
			if (SetField(ref _configuredCount, value, "ConfiguredCount"))
			{
				OnPropertyChanged("CountText");
			}
		}
	}

	public string CountText => $"{ConfiguredCount}/10";

	public event PropertyChangedEventHandler? PropertyChanged;

	public WidgetTab(int index)
	{
		Index = index;
		_name = AppText.DefaultWidgetName(index);
	}

	private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PackagingTools.Core.Models;

namespace PackagingTools.App.ViewModels;

public partial class PlatformConfigurationViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> formats = new();

    [ObservableProperty]
    private ObservableCollection<PropertyItemViewModel> properties = new();

    public PlatformConfigurationViewModel() { }

    public PlatformConfigurationViewModel(string name, PlatformConfiguration configuration)
    {
        Name = name;
        Formats = new ObservableCollection<string>(configuration.Formats);
        Properties = new ObservableCollection<PropertyItemViewModel>(configuration.Properties.Select(kv => new PropertyItemViewModel(kv.Key, kv.Value)));
    }

    public string? GetPropertyValue(string key)
        => Properties.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    public void SetOrRemoveProperty(string key, string? value)
    {
        var existing = Properties.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing is not null)
            {
                Properties.Remove(existing);
            }
            return;
        }

        if (existing is null)
        {
            Properties.Add(new PropertyItemViewModel(key, value!));
        }
        else
        {
            existing.Value = value!;
        }
    }

    public PlatformConfiguration ToConfiguration()
        => new(
            Formats.ToList(),
            Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase));
}

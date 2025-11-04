using CommunityToolkit.Mvvm.ComponentModel;

namespace PackagingTools.App.ViewModels;

public partial class PropertyItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string key = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    public PropertyItemViewModel() { }

    public PropertyItemViewModel(string key, string value)
    {
        Key = key;
        Value = value;
    }
}

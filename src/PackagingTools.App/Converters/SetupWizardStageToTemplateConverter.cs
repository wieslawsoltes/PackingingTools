using System;
using System.Globalization;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using PackagingTools.App.ViewModels;

namespace PackagingTools.App.Converters;

public sealed class SetupWizardStageToTemplateConverter : IValueConverter
{
    public IDataTemplate? ProjectDetailsTemplate { get; set; }
    public IDataTemplate? EnvironmentTemplate { get; set; }
    public IDataTemplate? PlatformTemplate { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is WizardStage stage)
        {
            return stage switch
            {
                WizardStage.ProjectDetails => ProjectDetailsTemplate ?? EnvironmentTemplate ?? PlatformTemplate,
                WizardStage.EnvironmentValidation => EnvironmentTemplate ?? ProjectDetailsTemplate ?? PlatformTemplate,
                WizardStage.PlatformConfiguration => PlatformTemplate ?? ProjectDetailsTemplate ?? EnvironmentTemplate,
                _ => ProjectDetailsTemplate ?? EnvironmentTemplate ?? PlatformTemplate
            };
        }

        return ProjectDetailsTemplate ?? EnvironmentTemplate ?? PlatformTemplate;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

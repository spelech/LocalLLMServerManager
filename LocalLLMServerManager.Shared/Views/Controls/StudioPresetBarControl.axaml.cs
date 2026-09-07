using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class StudioPresetBarControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> PresetsProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, IEnumerable?>(nameof(Presets));

    public static readonly StyledProperty<StudioPreset?> SelectedPresetProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, StudioPreset?>(nameof(SelectedPreset), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable?> StarterPromptsProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, IEnumerable?>(nameof(StarterPrompts));

    public static readonly StyledProperty<ICommand?> SavePresetCommandProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, ICommand?>(nameof(SavePresetCommand));

    public static readonly StyledProperty<ICommand?> EditPresetCommandProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, ICommand?>(nameof(EditPresetCommand));

    public static readonly StyledProperty<ICommand?> DeletePresetCommandProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, ICommand?>(nameof(DeletePresetCommand));

    public static readonly StyledProperty<ICommand?> DuplicatePresetCommandProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, ICommand?>(nameof(DuplicatePresetCommand));

    public static readonly StyledProperty<ICommand?> ApplyStarterPromptCommandProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, ICommand?>(nameof(ApplyStarterPromptCommand));

    public static readonly StyledProperty<bool> IsCustomPresetProperty =
        AvaloniaProperty.Register<StudioPresetBarControl, bool>(nameof(IsCustomPreset), false);

    public IEnumerable? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    public StudioPreset? SelectedPreset
    {
        get => GetValue(SelectedPresetProperty);
        set => SetValue(SelectedPresetProperty, value);
    }

    public IEnumerable? StarterPrompts
    {
        get => GetValue(StarterPromptsProperty);
        set => SetValue(StarterPromptsProperty, value);
    }

    public ICommand? SavePresetCommand
    {
        get => GetValue(SavePresetCommandProperty);
        set => SetValue(SavePresetCommandProperty, value);
    }

    public ICommand? EditPresetCommand
    {
        get => GetValue(EditPresetCommandProperty);
        set => SetValue(EditPresetCommandProperty, value);
    }

    public ICommand? DeletePresetCommand
    {
        get => GetValue(DeletePresetCommandProperty);
        set => SetValue(DeletePresetCommandProperty, value);
    }

    public ICommand? DuplicatePresetCommand
    {
        get => GetValue(DuplicatePresetCommandProperty);
        set => SetValue(DuplicatePresetCommandProperty, value);
    }

    public ICommand? ApplyStarterPromptCommand
    {
        get => GetValue(ApplyStarterPromptCommandProperty);
        set => SetValue(ApplyStarterPromptCommandProperty, value);
    }

    public bool IsCustomPreset
    {
        get => GetValue(IsCustomPresetProperty);
        set => SetValue(IsCustomPresetProperty, value);
    }

    public StudioPresetBarControl()
    {
        InitializeComponent();
    }
}

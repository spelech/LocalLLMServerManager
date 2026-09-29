using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class NavigationRailViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStudioSelected))]
    [NotifyPropertyChangedFor(nameof(IsModelsSelected))]
    [NotifyPropertyChangedFor(nameof(IsHardwareFitSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    private NavDomain _selectedDomain = NavDomain.Studio;

    [ObservableProperty]
    private bool _isExpanded = false;

    public bool IsStudioSelected => SelectedDomain == NavDomain.Studio;
    public bool IsModelsSelected => SelectedDomain == NavDomain.Models;
    public bool IsHardwareFitSelected => SelectedDomain == NavDomain.HardwareFit;
    public bool IsSettingsSelected => SelectedDomain == NavDomain.Settings;

    public IRelayCommand ToggleRailExpandedCommand { get; }
    public IRelayCommand<NavDomain> SelectDomainCommand { get; }
    public IRelayCommand DocumentationCommand { get; }
    public IRelayCommand AiAssistCommand { get; }

    public event Action? DocumentationRequested;
    public event Action? AiAssistRequested;

    public NavigationRailViewModel()
    {
        ToggleRailExpandedCommand = new RelayCommand(ToggleRailExpanded);
        SelectDomainCommand = new RelayCommand<NavDomain>(SelectDomain);
        DocumentationCommand = new RelayCommand(() => DocumentationRequested?.Invoke());
        AiAssistCommand = new RelayCommand(() => AiAssistRequested?.Invoke());
    }

    public void ToggleRailExpanded() => IsExpanded = !IsExpanded;

    public void SelectDomain(NavDomain domain) => SelectedDomain = domain;
}

using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class NavigationRailViewModel : ObservableObject
{
    [ObservableProperty]
    private NavDomain _selectedDomain = NavDomain.Studio;

    [ObservableProperty]
    private bool _isExpanded = false;

    public IRelayCommand ToggleRailExpandedCommand { get; }
    public IRelayCommand<NavDomain> SelectDomainCommand { get; }

    public NavigationRailViewModel()
    {
        ToggleRailExpandedCommand = new RelayCommand(ToggleRailExpanded);
        SelectDomainCommand = new RelayCommand<NavDomain>(SelectDomain);
    }

    public void ToggleRailExpanded() => IsExpanded = !IsExpanded;

    public void SelectDomain(NavDomain domain) => SelectedDomain = domain;
}

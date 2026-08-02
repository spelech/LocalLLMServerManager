using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LocalLLMServerManager.Shared.Services;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

public partial class ToastItem : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private ToastType _type = ToastType.Info;

    [ObservableProperty]
    private string _badgeColor = "#38BDF8";

    public ToastItem(string message, ToastType type)
    {
        Message = message;
        Type = type;
        BadgeColor = type switch
        {
            ToastType.Success => "#22C55E",
            ToastType.Warning => "#F59E0B",
            ToastType.Error => "#EF4444",
            _ => "#38BDF8"
        };
    }
}

public class ToastService
{
    public static ToastService Instance { get; } = new();

    public ObservableCollection<ToastItem> ActiveToasts { get; } = new();

    public void Show(string message, ToastType type = ToastType.Info)
    {
        var toast = new ToastItem(message, type);
        ActiveToasts.Add(toast);

        _ = Task.Delay(4000).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ActiveToasts.Remove(toast);
            });
        });
    }
}

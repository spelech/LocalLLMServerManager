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

    public void Remove(ToastItem toast)
    {
        ActiveToasts.Remove(toast);
    }

    public void Clear()
    {
        ActiveToasts.Clear();
    }

    public void Show(string message, ToastType type = ToastType.Info, int autoRemoveMs = 4000)
    {
        var toast = new ToastItem(message, type);
        ActiveToasts.Add(toast);

        if (autoRemoveMs == 0)
        {
            Remove(toast);
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(autoRemoveMs);
            try
            {
                if (Avalonia.Application.Current != null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Remove(toast));
                }
                else
                {
                    Remove(toast);
                }
            }
            catch
            {
                Remove(toast);
            }
        });
    }
}

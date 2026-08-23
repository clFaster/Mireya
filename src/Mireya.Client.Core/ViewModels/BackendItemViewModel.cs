using System;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class BackendItemViewModel : ViewModelBase
{
    public ApiClient.Data.BackendInstance Instance { get; }

    /// <summary>Fires when the user presses the per-item delete button.</summary>
    public IAsyncRelayCommand DeleteCommand { get; }

    [ObservableProperty]
    public partial bool IsOnline { get; set; }

    [ObservableProperty]
    public partial bool IsCheckingOnline { get; set; } = true;

    /// <summary>
    /// A brush reflecting the current online/checking state:
    /// yellow = checking, green = online, dark grey = offline.
    /// </summary>
    public IBrush StatusDotBrush => GetStatusDotBrush();

    partial void OnIsOnlineChanged(bool value) => OnPropertyChanged(nameof(StatusDotBrush));

    partial void OnIsCheckingOnlineChanged(bool value) => OnPropertyChanged(nameof(StatusDotBrush));

    private IBrush GetStatusDotBrush()
    {
        if (IsCheckingOnline)
            return Brush.Parse("#FFA726");

        return IsOnline ? Brush.Parse("#66BB6A") : Brush.Parse("#546E7A");
    }

    public BackendItemViewModel(
        ApiClient.Data.BackendInstance instance,
        Func<BackendItemViewModel, Task> onDelete
    )
    {
        Instance = instance;
        DeleteCommand = new AsyncRelayCommand(() => onDelete(this));
    }
}

using System;
using Avalonia.Controls;
using Mireya.Client.Avalonia.ViewModels;
using Mireya.Client.Avalonia.Views.Components;

namespace Mireya.Client.Avalonia.Views;

public partial class ContentDisplayView : UserControl
{
    public ContentDisplayView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ContentDisplayViewModel viewModel)
        {
            // Find the VideoAssetDisplay component
            var videoDisplay = this.FindControl<VideoAssetDisplay>("VideoDisplay");
            if (videoDisplay != null)
            {
                viewModel.VideoPlaybackRequested += videoDisplay.PlayVideo;
                viewModel.VideoStopRequested += videoDisplay.Stop;
            }
        }
    }
}

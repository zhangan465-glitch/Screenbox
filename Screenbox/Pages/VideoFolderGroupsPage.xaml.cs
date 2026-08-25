using System;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.WinUI;
using Screenbox.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Screenbox.Pages;

/// <summary>
/// Displays video library roots as vertically stacked groups with one-row previews.
/// </summary>
public sealed partial class VideoFolderGroupsPage : Page
{
    internal VideoFolderGroupsPageViewModel ViewModel => (VideoFolderGroupsPageViewModel)DataContext;

    internal CommonViewModel Common { get; }

    private const double PreviewItemSlotWidth = 236;
    private double _contentVerticalOffset;
    private ScrollViewer? _scrollViewer;

    public VideoFolderGroupsPage()
    {
        InitializeComponent();
        DataContext = Ioc.Default.GetRequiredService<VideoFolderGroupsPageViewModel>();
        Common = Ioc.Default.GetRequiredService<CommonViewModel>();
    }

    /// <inheritdoc/>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.NavigationMode == NavigationMode.Back
            && Common.TryGetPageState(nameof(VideoFolderGroupsPage), Frame.BackStackDepth, out object? state)
            && state is double verticalOffset)
        {
            _contentVerticalOffset = verticalOffset;
        }

        ViewModel.OnNavigatedTo();
        UpdatePreviewLimit();
        RestoreScrollVerticalOffset();
    }

    /// <inheritdoc/>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }

    /// <summary>
    /// Recalculates the one-row preview capacity when the page width changes.
    /// </summary>
    private void Page_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePreviewLimit();
    }

    /// <summary>
    /// Uses the same card width and page padding as the existing video grids.
    /// </summary>
    private void UpdatePreviewLimit()
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        double horizontalPadding = Common.NavigationViewDisplayMode == NavigationViewDisplayMode.Minimal ? 28 : 104;
        int previewLimit = (int)Math.Floor(Math.Max(PreviewItemSlotWidth, ActualWidth - horizontalPadding) /
                                           PreviewItemSlotWidth);
        ViewModel.SetPreviewLimit(Math.Clamp(previewLimit, 1, 5));
    }

    /// <summary>
    /// Captures the internal list scroll viewer for navigation-state restoration.
    /// </summary>
    private void FolderGroups_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is not null)
        {
            return;
        }

        _scrollViewer = FolderGroups.FindDescendant<ScrollViewer>();
        if (_scrollViewer is null)
        {
            return;
        }

        _scrollViewer.ViewChanging += ScrollViewerOnViewChanging;
        RestoreScrollVerticalOffset();
    }

    /// <summary>
    /// Restores the scroll position after the ListView visual tree is ready.
    /// </summary>
    private void RestoreScrollVerticalOffset()
    {
        if (_scrollViewer is not null && _contentVerticalOffset > 0 && _scrollViewer.VerticalOffset == 0)
        {
            _scrollViewer.ChangeView(null, _contentVerticalOffset, null, true);
        }
    }

    /// <summary>
    /// Persists the next vertical offset for back navigation.
    /// </summary>
    private void ScrollViewerOnViewChanging(object? sender, ScrollViewerViewChangingEventArgs e)
    {
        Common.SavePageState(e.NextView.VerticalOffset, nameof(VideoFolderGroupsPage), Frame.BackStackDepth);
    }
}

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.WinUI;
using Screenbox.Core.ViewModels;
using Screenbox.Dialogs;
using Screenbox.Extensions;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Screenbox.Controls;

/// <summary>
/// Displays the current playback queue and saved playlists inside the player.
/// </summary>
public sealed partial class PlayerSidebar : UserControl
{
    /// <summary>
    /// Identifies the <see cref="IsFlyout"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsFlyoutProperty = DependencyProperty.Register(
        nameof(IsFlyout),
        typeof(bool),
        typeof(PlayerSidebar),
        new PropertyMetadata(false));

    /// <summary>
    /// Occurs when the user requests that the sidebar be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    internal PlayerSidebarViewModel ViewModel => (PlayerSidebarViewModel)DataContext;

    /// <summary>
    /// Gets or sets whether the control is hosted inside a flyout.
    /// </summary>
    public bool IsFlyout
    {
        get => (bool)GetValue(IsFlyoutProperty);
        set => SetValue(IsFlyoutProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSidebar"/> class.
    /// </summary>
    public PlayerSidebar()
    {
        this.InitializeComponent();
        DataContext = Ioc.Default.GetRequiredService<PlayerSidebarViewModel>();
    }

    /// <summary>
    /// Scrolls the current queue to the active playback item.
    /// </summary>
    public async void PrepareForOpen()
    {
        SourceTabs.Focus(FocusState.Programmatic);
        if (ViewModel.IsCurrentQueueSelected)
        {
            await CurrentQueueView.SmoothScrollActiveItemIntoViewAsync();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void CreatePlaylistButton_OnClick(object sender, RoutedEventArgs e)
    {
        string? playlistName = await CreatePlaylistDialog.GetPlaylistNameAsync();
        if (playlistName is not null)
        {
            await ViewModel.CreatePlaylistAsync(playlistName);
        }
    }

    private async void RenamePlaylistButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPlaylist is not { } playlist)
        {
            return;
        }

        RenamePlaylistDialog dialog = new(playlist.Name);
        string? newName = await dialog.GetPlaylistNameAsync();
        if (!string.IsNullOrWhiteSpace(newName) && newName != playlist.Name)
        {
            await ViewModel.RenameSelectedPlaylistAsync(newName);
        }
    }

    private async void DeletePlaylistButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPlaylist is not { } playlist)
        {
            return;
        }

        var dialog = new DeletePlaylistDialog(playlist.Name);
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteSelectedPlaylistAsync();
        }
    }

    private void SourceMenu_OnOpening(object sender, object e)
    {
        if (sender is not MenuFlyout menu)
        {
            return;
        }

        menu.Items.Clear();
        foreach (PlayerSidebarSourceViewModel source in ViewModel.Sources)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = source.IsCurrentQueue ? Strings.Resources.CurrentPlayQueue : source.Playlist?.Name ?? string.Empty,
                IsChecked = ReferenceEquals(source, ViewModel.SelectedSource),
                Tag = source,
            };
            item.Click += SourceMenuItem_OnClick;
            menu.Items.Add(item);
        }
    }

    private void SourceMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PlayerSidebarSourceViewModel source })
        {
            ViewModel.SelectedSource = source;
        }
    }

    private void SavedPlaylistListView_OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (SavedPlaylistListView.SelectedItem is MediaViewModel item)
        {
            ViewModel.PlaySavedItemCommand.Execute(item);
            e.Handled = true;
        }
    }

    private async void SavedPlaylistListView_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.Handled = true;
        IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();
        if (items?.Count > 0)
        {
            int insertIndex = SavedPlaylistListView.GetDropIndex(e);
            await ViewModel.AddDroppedItemsAsync(items, insertIndex);
        }
    }

    private void SavedPlaylistListView_OnDragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        if (e.DragUIOverride is not null)
        {
            e.DragUIOverride.Caption = Strings.Resources.AddToPlaylist;
        }
        e.Handled = true;
    }
}

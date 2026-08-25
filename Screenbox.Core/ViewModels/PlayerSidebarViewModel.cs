using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Screenbox.Core.Contexts;
using Screenbox.Core.Coordinators;
using Screenbox.Core.Factories;
using Screenbox.Core.Helpers;
using Screenbox.Core.Messages;
using Screenbox.Core.Models;
using Screenbox.Core.Services;
using Windows.Storage;

namespace Screenbox.Core.ViewModels;

/// <summary>
/// Coordinates list selection and saved-playlist actions for the in-player sidebar.
/// </summary>
public sealed partial class PlayerSidebarViewModel : ObservableRecipient
{
    /// <summary>
    /// Gets the current queue followed by the available saved playlists.
    /// </summary>
    public ObservableCollection<PlayerSidebarSourceViewModel> Sources { get; } = new();

    /// <summary>
    /// Gets the active playback queue.
    /// </summary>
    public PlayQueueContext Queue { get; }

    /// <summary>
    /// Gets the items displayed for the selected source.
    /// </summary>
    public ObservableCollection<MediaViewModel>? SelectedItems => SelectedSource?.Items;

    /// <summary>
    /// Gets a value indicating whether the active playback queue is selected.
    /// </summary>
    public bool IsCurrentQueueSelected => SelectedSource?.IsCurrentQueue is true;

    /// <summary>
    /// Gets a value indicating whether a saved playlist is selected.
    /// </summary>
    public bool IsSavedPlaylistSelected => SelectedSource is { IsCurrentQueue: false };

    /// <summary>
    /// Gets a value indicating whether the selected saved playlist contains media.
    /// </summary>
    public bool HasSelectedPlaylistItems => SelectedSource is { IsCurrentQueue: false, Items.Count: > 0 };

    /// <summary>
    /// Gets the selected saved playlist, or <see langword="null"/> when the playback queue is selected.
    /// </summary>
    public PlaylistViewModel? SelectedPlaylist => SelectedSource?.Playlist;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedItems))]
    [NotifyPropertyChangedFor(nameof(IsCurrentQueueSelected))]
    [NotifyPropertyChangedFor(nameof(IsSavedPlaylistSelected))]
    [NotifyPropertyChangedFor(nameof(HasSelectedPlaylistItems))]
    [NotifyPropertyChangedFor(nameof(SelectedPlaylist))]
    public partial PlayerSidebarSourceViewModel? SelectedSource { get; set; }

    [ObservableProperty]
    public partial MediaViewModel? ContextMedia { get; set; }

    private readonly PlaylistsContext _playlistsContext;
    private readonly IPlaylistViewModelFactory _playlistFactory;
    private readonly IPlaylistService _playlistService;
    private readonly IFilesService _filesService;
    private readonly IMediaListFactory _mediaListFactory;
    private readonly MediaViewModelFactory _mediaFactory;
    private readonly IPlayQueueCoordinator _playQueueCoordinator;
    private readonly PlayerSidebarSourceViewModel _currentQueueSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSidebarViewModel"/> class.
    /// </summary>
    public PlayerSidebarViewModel(
        PlayQueueContext queue,
        PlaylistsContext playlistsContext,
        IPlaylistViewModelFactory playlistFactory,
        IPlaylistService playlistService,
        IFilesService filesService,
        IMediaListFactory mediaListFactory,
        MediaViewModelFactory mediaFactory,
        IPlayQueueCoordinator playQueueCoordinator)
    {
        Queue = queue;
        _playlistsContext = playlistsContext;
        _playlistFactory = playlistFactory;
        _playlistService = playlistService;
        _filesService = filesService;
        _mediaListFactory = mediaListFactory;
        _mediaFactory = mediaFactory;
        _playQueueCoordinator = playQueueCoordinator;

        _currentQueueSource = PlayerSidebarSourceViewModel.CreateCurrentQueue(queue.Items);
        RebuildSources();
        SelectedSource = _currentQueueSource;
        _playlistsContext.Playlists.CollectionChanged += PlaylistsOnCollectionChanged;
    }

    /// <summary>
    /// Creates and selects a saved playlist.
    /// </summary>
    public async Task CreatePlaylistAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        PlaylistViewModel playlist = _playlistFactory.Create();
        playlist.Name = displayName;
        await playlist.SaveAsync();
        _playlistsContext.Playlists.Insert(0, playlist);
        SelectedSource = Sources.FirstOrDefault(source => ReferenceEquals(source.Playlist, playlist));
        Messenger.Send(new PlaylistCreatedNotificationMessage(displayName));
    }

    /// <summary>
    /// Renames the selected saved playlist.
    /// </summary>
    public async Task RenameSelectedPlaylistAsync(string displayName)
    {
        if (SelectedPlaylist is not { } playlist || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        await playlist.RenameAsync(displayName);
        Messenger.Send(new PlaylistRenamedNotificationMessage(displayName));
    }

    /// <summary>
    /// Deletes the selected saved playlist.
    /// </summary>
    public async Task DeleteSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is not { } playlist)
        {
            return;
        }

        await _playlistService.DeletePlaylistAsync(playlist.Id);
        _playlistsContext.Playlists.Remove(playlist);
        Messenger.Send(new PlaylistDeletedNotificationMessage(playlist.Name));
    }

    /// <summary>
    /// Adds dropped storage items to the selected source.
    /// </summary>
    public async Task AddDroppedItemsAsync(IReadOnlyList<IStorageItem> items, int insertIndex = -1)
    {
        if (items.Count is 0)
        {
            return;
        }

        if (IsCurrentQueueSelected)
        {
            await _playQueueCoordinator.EnqueueAsync(items, insertIndex);
            return;
        }

        if (SelectedSource?.Playlist is not { } playlist)
        {
            return;
        }

        NextMediaList? parsed = await _mediaListFactory.TryParseMediaListAsync(items);
        if (parsed?.Items.Count > 0)
        {
            await playlist.AddItemsAtIndexAsync(parsed.Items, insertIndex);
        }
    }

    /// <summary>
    /// Selects the active playback queue tab.
    /// </summary>
    public void SelectCurrentQueue()
    {
        SelectedSource = _currentQueueSource;
    }

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        IReadOnlyList<StorageFile>? files = await _filesService.PickMultipleFilesAsync();
        if (files is null || files.Count is 0)
        {
            return;
        }

        if (IsCurrentQueueSelected)
        {
            await _playQueueCoordinator.EnqueueAsync(files);
            return;
        }

        if (SelectedSource?.Playlist is not { } playlist)
        {
            return;
        }

        List<MediaViewModel> media = files
            .Where(file => file.IsSupported())
            .Select(_mediaFactory.GetOrCreate)
            .ToList();
        await Task.WhenAll(media.Select(item => item.LoadDetailsAsync(_filesService)));
        await playlist.AddItemsAsync(media);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPlaylistItems))]
    private void PlaySelectedPlaylist()
    {
        if (SelectedSource?.Playlist is not { } playlist)
        {
            return;
        }

        Messenger.Send(new SetQueueMessage(playlist.ToPlaylist(), true));
        SelectCurrentQueue();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPlaylistItems))]
    private void AddSelectedPlaylistToQueue()
    {
        if (SelectedSource?.Playlist is { } playlist)
        {
            Messenger.SendAddToQueue(playlist.Items);
        }
    }

    [RelayCommand]
    private void PlaySavedItem(MediaViewModel? item)
    {
        if (item is null || SelectedSource?.Playlist is not { } playlist)
        {
            return;
        }

        Messenger.Send(new SetQueueMessage(new Playlist(item, playlist.Items), true));
        SelectCurrentQueue();
    }

    [RelayCommand]
    private void PlaySavedItemNext(MediaViewModel? item)
    {
        if (item is not null)
        {
            Messenger.SendPlayNext(item);
        }
    }

    [RelayCommand]
    private void AddSavedItemToQueue(MediaViewModel? item)
    {
        if (item is not null)
        {
            Messenger.SendAddToQueue(item);
        }
    }

    [RelayCommand]
    private async Task RemoveSavedItemAsync(MediaViewModel? item)
    {
        if (item is null || SelectedSource?.Playlist is not { } playlist || !playlist.Items.Remove(item))
        {
            return;
        }

        await playlist.SaveAsync();
        ContextMedia = null;
    }

    partial void OnSelectedSourceChanging(PlayerSidebarSourceViewModel? value)
    {
        if (SelectedSource is not null)
        {
            SelectedSource.Items.CollectionChanged -= SelectedItemsOnCollectionChanged;
        }
    }

    partial void OnSelectedSourceChanged(PlayerSidebarSourceViewModel? value)
    {
        ContextMedia = null;
        if (value is not null)
        {
            value.Items.CollectionChanged += SelectedItemsOnCollectionChanged;
        }

        NotifySelectedSourceCommands();
    }

    private void SelectedItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSelectedPlaylistItems));
        NotifySelectedSourceCommands();
    }

    private void PlaylistsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PlaylistViewModel? selectedPlaylist = SelectedSource?.Playlist;
        RebuildSources();
        SelectedSource = selectedPlaylist is null
            ? _currentQueueSource
            : Sources.FirstOrDefault(source => ReferenceEquals(source.Playlist, selectedPlaylist)) ?? _currentQueueSource;
    }

    private void RebuildSources()
    {
        Sources.Clear();
        Sources.Add(_currentQueueSource);
        foreach (PlaylistViewModel playlist in _playlistsContext.Playlists)
        {
            Sources.Add(PlayerSidebarSourceViewModel.CreateSavedPlaylist(playlist));
        }
    }

    private void NotifySelectedSourceCommands()
    {
        PlaySelectedPlaylistCommand.NotifyCanExecuteChanged();
        AddSelectedPlaylistToQueueCommand.NotifyCanExecuteChanged();
    }
}

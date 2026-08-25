using System.Collections.ObjectModel;

namespace Screenbox.Core.ViewModels;

/// <summary>
/// Represents one list source displayed by the player sidebar.
/// </summary>
public sealed class PlayerSidebarSourceViewModel
{
    /// <summary>
    /// Gets a value indicating whether this source is the active playback queue.
    /// </summary>
    public bool IsCurrentQueue { get; }

    /// <summary>
    /// Gets the saved playlist represented by this source, or <see langword="null"/> for the playback queue.
    /// </summary>
    public PlaylistViewModel? Playlist { get; }

    /// <summary>
    /// Gets the media items shown for this source.
    /// </summary>
    public ObservableCollection<MediaViewModel> Items { get; }

    private PlayerSidebarSourceViewModel(
        ObservableCollection<MediaViewModel> items,
        bool isCurrentQueue,
        PlaylistViewModel? playlist)
    {
        Items = items;
        IsCurrentQueue = isCurrentQueue;
        Playlist = playlist;
    }

    /// <summary>
    /// Creates a source for the active playback queue.
    /// </summary>
    public static PlayerSidebarSourceViewModel CreateCurrentQueue(ObservableCollection<MediaViewModel> items)
    {
        return new PlayerSidebarSourceViewModel(items, true, null);
    }

    /// <summary>
    /// Creates a source for a saved playlist.
    /// </summary>
    public static PlayerSidebarSourceViewModel CreateSavedPlaylist(PlaylistViewModel playlist)
    {
        return new PlayerSidebarSourceViewModel(playlist.Items, false, playlist);
    }
}

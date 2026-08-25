using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Screenbox.Core.Helpers;
using Screenbox.Core.Services;
using Windows.Storage;

namespace Screenbox.Core.ViewModels;

/// <summary>
/// Represents one root folder and the videos recursively assigned to it.
/// </summary>
public sealed partial class VideoFolderGroupViewModel : ObservableObject
{
    /// <summary>Gets the folder display name.</summary>
    public string Name => Folder.DisplayName;

    /// <summary>Gets the folder path shown below the group name.</summary>
    public string Path => Folder.Path;

    /// <summary>Gets whether the folder contains any indexed videos.</summary>
    public bool HasVideos => _videos.Count > 0;

    /// <summary>Gets whether the folder can currently be accessed.</summary>
    [ObservableProperty] public partial bool IsAvailable { get; set; } = true;

    /// <summary>Gets the number of videos recursively assigned to this folder.</summary>
    public int VideoCount => _videos.Count;

    /// <summary>Gets the bounded set of videos displayed in the preview row.</summary>
    public ObservableCollection<MediaViewModel> PreviewVideos { get; } = [];

    internal StorageFolder Folder { get; }

    private readonly INavigationService _navigationService;
    private IReadOnlyList<MediaViewModel> _videos;
    private int _previewLimit;

    internal VideoFolderGroupViewModel(StorageFolder folder, IReadOnlyList<MediaViewModel> videos,
        INavigationService navigationService, int previewLimit)
    {
        Folder = folder;
        _navigationService = navigationService;
        _videos = videos;
        _previewLimit = previewLimit;
        UpdatePreviewVideos();
    }

    /// <summary>
    /// Updates the recursively assigned videos while keeping the group instance stable for scrolling.
    /// </summary>
    internal void UpdateVideos(IReadOnlyList<MediaViewModel> videos)
    {
        _videos = videos;
        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(VideoCount));
        PlayAllCommand.NotifyCanExecuteChanged();
        UpdatePreviewVideos();
    }

    /// <summary>
    /// Changes the number of preview cards to match the available page width.
    /// </summary>
    internal void SetPreviewLimit(int previewLimit)
    {
        if (_previewLimit == previewLimit)
        {
            return;
        }

        _previewLimit = previewLimit;
        UpdatePreviewVideos();
    }

    /// <summary>
    /// Updates folder availability and the commands that depend on it.
    /// </summary>
    internal void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
        OpenCommand.NotifyCanExecuteChanged();
        PlayAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Rebuilds the small preview collection without exposing the complete group to the view.
    /// </summary>
    private void UpdatePreviewVideos()
    {
        PreviewVideos.Clear();
        foreach (MediaViewModel video in _videos.Take(_previewLimit))
        {
            PreviewVideos.Add(video);
        }
    }

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private void Open()
    {
        StorageFolder[] breadcrumbs = [Folder];
        _navigationService.Navigate(typeof(FolderViewPageViewModel),
            new NavigationMetadata(typeof(VideosPageViewModel), breadcrumbs));
    }

    [RelayCommand]
    private void Play(MediaViewModel media)
    {
        if (!IsAvailable || !HasVideos)
        {
            return;
        }

        WeakReferenceMessenger.Default.SendQueueAndPlay(media, _videos, true);
    }

    private bool CanPlayAll => IsAvailable && HasVideos;

    [RelayCommand(CanExecute = nameof(CanPlayAll))]
    private void PlayAll()
    {
        if (_videos.FirstOrDefault() is not { } firstVideo)
        {
            return;
        }

        WeakReferenceMessenger.Default.SendQueueAndPlay(firstVideo, _videos, true);
    }
}

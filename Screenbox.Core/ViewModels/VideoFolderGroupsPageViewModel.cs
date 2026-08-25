using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI;
using Microsoft.Extensions.Logging;
using Screenbox.Core.Contexts;
using Screenbox.Core.Helpers;
using Screenbox.Core.Messages;
using Screenbox.Core.Models;
using Screenbox.Core.Services;
using Windows.Storage;
using Windows.System;

namespace Screenbox.Core.ViewModels;

/// <summary>
/// Builds the Videos library home as root-folder groups with bounded previews.
/// </summary>
public sealed partial class VideoFolderGroupsPageViewModel : ObservableRecipient,
    IRecipient<PropertyChangedMessage<VideosLibrary>>,
    IRecipient<RefreshFolderMessage>
{
    /// <summary>Gets the ordered root-folder groups shown on the page.</summary>
    public ObservableCollection<VideoFolderGroupViewModel> Groups { get; } = [];

    [ObservableProperty] public partial bool IsEmpty { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    private readonly LibraryContext _libraryContext;
    private readonly INavigationService _navigationService;
    private readonly ILogger<VideoFolderGroupsPageViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _refreshTimer;
    private bool _isActive;
    private int _availabilityVersion;
    private int _previewLimit = 3;

    public VideoFolderGroupsPageViewModel(LibraryContext libraryContext, INavigationService navigationService,
        ILogger<VideoFolderGroupsPageViewModel> logger)
    {
        _libraryContext = libraryContext;
        _navigationService = navigationService;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _refreshTimer = _dispatcherQueue.CreateTimer();

        Messenger.Register<PropertyChangedMessage<VideosLibrary>>(this);
        Messenger.Register<RefreshFolderMessage>(this);
    }

    /// <inheritdoc/>
    public void Receive(PropertyChangedMessage<VideosLibrary> message)
    {
        if (_isActive)
        {
            _dispatcherQueue.TryEnqueue(UpdateGroups);
        }
    }

    /// <inheritdoc/>
    public void Receive(RefreshFolderMessage message)
    {
        if (_isActive)
        {
            _dispatcherQueue.TryEnqueue(UpdateGroups);
        }
    }

    /// <summary>
    /// Activates live library updates and creates the current group snapshot.
    /// </summary>
    public void OnNavigatedTo()
    {
        _isActive = true;
        UpdateGroups();
    }

    /// <summary>
    /// Stops progressive refresh work while the page is not visible.
    /// </summary>
    public void OnNavigatedFrom()
    {
        _isActive = false;
        _availabilityVersion++;
        _refreshTimer.Stop();
    }

    /// <summary>
    /// Changes the number of cards kept in every preview row.
    /// </summary>
    public void SetPreviewLimit(int previewLimit)
    {
        _previewLimit = Math.Clamp(previewLimit, 1, 5);
        foreach (VideoFolderGroupViewModel group in Groups)
        {
            group.SetPreviewLimit(_previewLimit);
        }
    }

    /// <summary>
    /// Rebuilds folder membership from the latest library roots and indexed videos.
    /// </summary>
    public void UpdateGroups()
    {
        StorageLibrary? library = _libraryContext.VideosStorageLibrary;
        IsLoading = _libraryContext.IsLoadingVideos;
        if (library is null)
        {
            Groups.Clear();
            IsEmpty = true;
            return;
        }

        try
        {
            IReadOnlyList<StorageFolder> roots = GetOrderedRoots(library);
            IReadOnlyList<MediaViewModel> videos = _libraryContext.Videos.Videos;
            List<MediaViewModel>[] groupedVideos = roots.Select(_ => new List<MediaViewModel>()).ToArray();
            string[] rootPaths = roots.Select(folder => folder.Path).ToArray();

            foreach (MediaViewModel video in videos)
            {
                int rootIndex = MediaPathGroupingHelper.FindBestMatchingRoot(video.Location, rootPaths);
                if (rootIndex < 0 && roots.Count > 0)
                {
                    rootIndex = 0;
                }

                if (rootIndex >= 0)
                {
                    groupedVideos[rootIndex].Add(video);
                }
            }

            Dictionary<string, VideoFolderGroupViewModel> existingGroups = Groups
                .GroupBy(group => group.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            List<VideoFolderGroupViewModel> nextGroups = new(roots.Count);
            for (int index = 0; index < roots.Count; index++)
            {
                StorageFolder root = roots[index];
                if (existingGroups.TryGetValue(root.Path, out VideoFolderGroupViewModel? existingGroup))
                {
                    existingGroup.UpdateVideos(groupedVideos[index]);
                    nextGroups.Add(existingGroup);
                }
                else
                {
                    nextGroups.Add(new VideoFolderGroupViewModel(
                        root, groupedVideos[index], _navigationService, _previewLimit));
                }
            }

            Groups.SyncItems(nextGroups);
            IsEmpty = Groups.Count == 0;
            int availabilityVersion = ++_availabilityVersion;
            _ = UpdateAvailabilityAsync(nextGroups, availabilityVersion);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to build video folder groups.");
            Messenger.Send(new ErrorMessage(null, exception.Message));
            IsLoading = false;
            IsEmpty = Groups.Count == 0;
            _refreshTimer.Stop();
            return;
        }

        if (IsLoading)
        {
            _refreshTimer.Debounce(UpdateGroups, TimeSpan.FromSeconds(5));
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    /// <summary>
    /// Probes each root without blocking group rendering and marks disconnected roots unavailable.
    /// </summary>
    private async Task UpdateAvailabilityAsync(IReadOnlyList<VideoFolderGroupViewModel> groups,
        int availabilityVersion)
    {
        foreach (VideoFolderGroupViewModel group in groups)
        {
            bool isAvailable = true;
            try
            {
                await group.Folder.GetBasicPropertiesAsync();
            }
            catch (Exception exception)
            {
                isAvailable = false;
                _logger.LogWarning(exception, "Video library folder {FolderPath} is unavailable.", group.Path);
            }

            if (!_isActive || availabilityVersion != _availabilityVersion)
            {
                return;
            }

            group.SetAvailability(isAvailable);
        }
    }

    /// <summary>
    /// Keeps the library save folder first and sorts additional roots for stable presentation.
    /// </summary>
    private static IReadOnlyList<StorageFolder> GetOrderedRoots(StorageLibrary library)
    {
        StorageFolder? saveFolder = library.SaveFolder;
        List<StorageFolder> roots = [];
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        if (saveFolder is not null && paths.Add(saveFolder.Path))
        {
            roots.Add(saveFolder);
        }

        roots.AddRange(library.Folders
            .Where(folder => paths.Add(folder.Path))
            .OrderBy(folder => folder.DisplayName, StringComparer.CurrentCultureIgnoreCase));
        return roots;
    }
}

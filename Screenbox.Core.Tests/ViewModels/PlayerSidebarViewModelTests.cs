using Screenbox.Core.Contexts;
using Screenbox.Core.ViewModels;

namespace Screenbox.Core.Tests.ViewModels;

public class PlayerSidebarViewModelTests
{
    [Test]
    public async Task Constructor_ShouldPlaceCurrentQueueBeforeSavedPlaylists()
    {
        var queue = new PlayQueueContext();
        var playlists = new PlaylistsContext();
        PlaylistViewModel savedPlaylist = CreatePlaylist("Saved");
        playlists.Playlists.Add(savedPlaylist);

        PlayerSidebarViewModel vm = CreateViewModel(queue, playlists);

        await Assert.That(vm.Sources.Count).IsEqualTo(2);
        await Assert.That(vm.Sources[0].IsCurrentQueue).IsTrue();
        await Assert.That(ReferenceEquals(vm.Sources[0].Items, queue.Items)).IsTrue();
        await Assert.That(vm.Sources[1].Playlist).IsEqualTo(savedPlaylist);
        await Assert.That(ReferenceEquals(vm.Sources[1].Items, savedPlaylist.Items)).IsTrue();
        await Assert.That(vm.SelectedSource).IsEqualTo(vm.Sources[0]);
    }

    [Test]
    public async Task PlaylistsCollection_WhenChanged_ShouldSynchronizeSourcesAndPreserveSelection()
    {
        var queue = new PlayQueueContext();
        var playlists = new PlaylistsContext();
        PlaylistViewModel firstPlaylist = CreatePlaylist("First");
        playlists.Playlists.Add(firstPlaylist);
        PlayerSidebarViewModel vm = CreateViewModel(queue, playlists);
        vm.SelectedSource = vm.Sources[1];

        PlaylistViewModel secondPlaylist = CreatePlaylist("Second");
        playlists.Playlists.Add(secondPlaylist);

        await Assert.That(vm.Sources.Count).IsEqualTo(3);
        await Assert.That(vm.Sources[2].Playlist).IsEqualTo(secondPlaylist);
        await Assert.That(vm.SelectedPlaylist).IsEqualTo(firstPlaylist);

        playlists.Playlists.Remove(firstPlaylist);

        await Assert.That(vm.Sources.Count).IsEqualTo(2);
        await Assert.That(vm.SelectedSource).IsEqualTo(vm.Sources[0]);
        await Assert.That(vm.IsCurrentQueueSelected).IsTrue();
    }

    [Test]
    public async Task SavedPlaylistItems_WhenChanged_ShouldRemainSharedWithSelectedItems()
    {
        var queue = new PlayQueueContext();
        var playlists = new PlaylistsContext();
        PlaylistViewModel savedPlaylist = CreatePlaylist("Saved");
        playlists.Playlists.Add(savedPlaylist);
        PlayerSidebarViewModel vm = CreateViewModel(queue, playlists);
        vm.SelectedSource = vm.Sources[1];
        var media = new MediaViewModel(new PlayerContext(), null!, new Uri("https://example.test/media.mp4"));

        savedPlaylist.Items.Add(media);

        await Assert.That(ReferenceEquals(vm.SelectedItems, savedPlaylist.Items)).IsTrue();
        await Assert.That(vm.SelectedItems!.Count).IsEqualTo(1);
        await Assert.That(vm.SelectedItems[0]).IsEqualTo(media);
        await Assert.That(vm.HasSelectedPlaylistItems).IsTrue();
    }

    private static PlayerSidebarViewModel CreateViewModel(PlayQueueContext queue, PlaylistsContext playlists)
    {
        return new PlayerSidebarViewModel(queue, playlists, null!, null!, null!, null!, null!, null!);
    }

    private static PlaylistViewModel CreatePlaylist(string name)
    {
        return new PlaylistViewModel(null!, null!, null!) { Name = name };
    }
}

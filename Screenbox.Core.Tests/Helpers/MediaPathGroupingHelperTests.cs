using Screenbox.Core.Helpers;

namespace Screenbox.Core.Tests.Helpers;

public sealed class MediaPathGroupingHelperTests
{
    [Test]
    public async Task FindBestMatchingRoot_ShouldChooseMostSpecificRoot()
    {
        string[] roots = [@"C:\Videos", @"C:\Videos\Projects"];

        int result = MediaPathGroupingHelper.FindBestMatchingRoot(
            @"C:\Videos\Projects\Demo\clip.mp4", roots);

        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task FindBestMatchingRoot_ShouldRequireDirectoryBoundary()
    {
        string[] roots = [@"C:\Video"];

        int result = MediaPathGroupingHelper.FindBestMatchingRoot(
            @"C:\Videos\clip.mp4", roots);

        await Assert.That(result).IsEqualTo(-1);
    }

    [Test]
    public async Task FindBestMatchingRoot_ShouldIgnoreCaseAndTrailingSeparators()
    {
        string[] roots = [@"c:\videos\"];

        int result = MediaPathGroupingHelper.FindBestMatchingRoot(
            @"C:\VIDEOS\Trips\clip.mp4", roots);

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task FindBestMatchingRoot_ShouldIgnoreEmptyRoots()
    {
        string[] roots = [string.Empty, @"D:\Media"];

        int result = MediaPathGroupingHelper.FindBestMatchingRoot(
            @"D:\Media\clip.mp4", roots);

        await Assert.That(result).IsEqualTo(1);
    }
}

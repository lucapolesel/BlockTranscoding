using System.Drawing;
using BlockTranscoding.Configuration;

namespace BlockTranscoding.Utilities
{
    /// <summary>
    /// Resolution utility.
    /// </summary>
    public static class ResolutionUtility
    {
        /// <summary>
        /// Returns the screen size per resolution.
        /// </summary>
        /// <param name="resolution">Resolution.</param>
        /// <returns>Screen size.</returns>
        public static Size GetSize(Resolutions resolution)
        {
            return resolution switch
            {
                Resolutions.StandardDefinition => new Size(640, 480),
                Resolutions.HighDefinition => new Size(1280, 720),
                Resolutions.FullHD => new Size(1920, 1080),
                Resolutions.QuadHD => new Size(2560, 1440),
                Resolutions.UltraHD => new Size(3840, 2160),
                _ => Size.Empty
            };
        }
    }
}

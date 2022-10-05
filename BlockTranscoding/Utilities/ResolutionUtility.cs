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
            switch (resolution)
            {
                case Resolutions.StandardDefinition:
                    return new Size(640, 480);

                case Resolutions.HighDefinition:
                    return new Size(1280, 720);

                case Resolutions.FullHD:
                    return new Size(1920, 1080);

                case Resolutions.QuadHD:
                    return new Size(2560, 1440);

                case Resolutions.UltraHD:
                    return new Size(3840, 2160);
            }

            return Size.Empty;
        }
    }
}

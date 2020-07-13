using System;
using System.Text.RegularExpressions;

namespace KubeUpdateCheck
{
    public class ImageReference
    {
        private static readonly string DefaultRegistry = "registry-1.docker.io";
        private static readonly Regex VersionMatch = new Regex(@"^(?:(?<image>[^/\n]+\/[^@/\n]+)|(?:(?<domain>[^/\n]+)\/)?(?<image>[^\n:@]+)):(?<version>.+)$");

        private ImageReference(string registry, string imageName, string version)
        {
            Registry = registry;
            ImageName = imageName;
            Version = version;
        }

        public Uri FullyQualifiedRegistry()
        {
            return new UriBuilder()
            {
                Scheme = "https",
                Host = Registry
            }.Uri;
        }
        public string Registry { get; }
        public string ImageName { get; }
        public string Version { get; }

        public override string ToString()
        {
            if (Registry == DefaultRegistry)
            {
                return string.Format("{0}:{1}", ImageName, Version);
            } else {
                return string.Format("{0}/{1}:{2}", Registry, ImageName, Version);
            }
        }

        public ImageReference WithVersion(string version)
        {
            return new ImageReference(Registry, ImageName, version);
        }

        public static ImageReference Parse(string input)
        {
            Match match = VersionMatch.Match(input);
            if (!match.Success)
            {
                return null;
            }

            string registry = match.Groups["domain"].Success ? match.Groups["domain"].Value : DefaultRegistry;
            string image = match.Groups["image"].Value;
            if (!image.Contains('/'))
            {
                image = "library/" + image;
            }

            return new ImageReference(registry, image, match.Groups["version"].Value);
        }
    }
}

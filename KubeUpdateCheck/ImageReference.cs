using System.Text.RegularExpressions;

namespace KubeUpdateCheck
{
    internal class ImageReference
    {
        private static readonly Regex VersionMatch = new Regex(@"^(?:(?<image>[^/\n]+\/[^@/\n]+)|(?:(?<domain>[^/\n]+)\/)?(?<image>[^\n:@]+)):(?<version>.+)$");

        private ImageReference(string registry, string imageName, string version)
        {
            Registry = registry;
            ImageName = imageName;
            Version = version;
        }

        public string Registry { get; }
        public string ImageName { get; }
        public string Version { get; }

        public override string ToString()
        {
            return string.Format("{0}/{1}:{2}", Registry, ImageName, Version);
        }

        public static ImageReference Parse(string input)
        {
            Match match = VersionMatch.Match(input);
            if (!match.Success)
            {
                return null;
            }

            string registry = match.Groups["domain"].Success ? "https://" + match.Groups["domain"].Value : "https://registry-1.docker.io";
            string image = match.Groups["image"].Value;
            if (!image.Contains('/'))
            {
                image = "library/" + image;
            }

            return new ImageReference(registry, image, match.Groups["version"].Value);
        }
    }
}

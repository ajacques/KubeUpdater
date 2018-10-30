using System;
using k8s;
using k8s.Models;
using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Models;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.JsonPatch;

namespace KubeUpdateCheck
{
    class Program
    {
        static string BASE_VERSION = @"(([0-9]+)\.([0-9]+)(?:\.([0-9]+))?)";
        static Regex DEFAULT_VERSION_PARSE = new Regex(string.Format("^{0}$", BASE_VERSION), RegexOptions.Compiled);

        static void Main(string[] args)
        {
            KubernetesClientConfiguration config = KubernetesClientConfiguration.BuildConfigFromConfigFile("../../../kubeconfig.yaml");
            IKubernetes kubernetes = new Kubernetes(config);

            var deployments = kubernetes.ListDeploymentForAllNamespaces();

            var images = from deployment in deployments.Items
                         from container in deployment.Spec.Template.Spec.Containers
                         let decomposedImage = container.Image.Split(':')
                         where decomposedImage[1] != "latest" && decomposedImage[0].IndexOf('.') == -1 && !decomposedImage[0].EndsWith("sha256")
                         where deployment.Metadata.NamespaceProperty != "kube-system"
                         let imageName = decomposedImage[0].Contains('/') ? decomposedImage[0] : "library/" + decomposedImage[0]
                         let versionMatchString = deployment.Metadata.Annotations
                         group new { Deployment = deployment, Container = container, Version = decomposedImage[1], VersionString = deployment.Metadata.Annotations }
                         by imageName;

            IRegistryClient registryClient = new RegistryClientConfiguration(new Uri("https://registry-1.docker.io")).CreateClient();

            Regex versionParse = DEFAULT_VERSION_PARSE;

            foreach (var image in images)
            {
                CatalogParameters catalogParameters = new CatalogParameters();
                ListImageTagsParameters tagsParameters = new ListImageTagsParameters
                {
                    Number = 100
                };
                var result = registryClient.Tags.ListImageTagsAsync(image.Key, tagsParameters);
                result.Wait();

                foreach (var container in image)
                {
                    var matcher = ExtractMatchString(container.Deployment, container.Container.Name);
                    var start = matcher.Match(container.Version);
                    if (!start.Success)
                    {
                        continue;
                    }
                    var currentVersion = new Version(start.Groups[1].Value);

                    var upgradeTarget = (from tag in result.Result.Tags
                                         let parsed = matcher.Match(tag)
                                         where parsed.Success
                                         let candidate = new Version(parsed.Groups[1].Value)
                                         where candidate > currentVersion
                                         orderby candidate descending
                                         select tag).FirstOrDefault();

                    if (upgradeTarget != null)
                    {
                        Console.WriteLine("Upgrade deployment {0} container {1} from version {2} to {3}.", container.Deployment.Metadata.Name, container.Container.Name, container.Container.Image, upgradeTarget);
                        container.Container.Image = upgradeTarget;
                    }
                }
            }

            versionParse.ToString();
        }

        private static Regex ExtractMatchString(V1Deployment deployment, string containerName)
        {
            string value = GetContainerAnnotationWithFallback(deployment, "version-match", containerName);
            if (value == null)
            {
                return DEFAULT_VERSION_PARSE;
            }

            string regex = string.Format("^{0}$", value.Replace("{version}", BASE_VERSION));

            return new Regex(regex);
        }

        private static string GetContainerAnnotationWithFallback(V1Deployment deployment, string settingName, string containerName)
        {
            string name = string.Format("net.technowizardry.upgrade/{0}", settingName);
            string value;
            var annotations = deployment.Spec.Template.Metadata.Annotations;
            if (annotations == null)
            {
                return null;
            }
            else if (annotations.TryGetValue(name + "-" + containerName, out value))
            {
                return value;
            } else if (annotations.TryGetValue(name, out value)) {
                return value;
            } else {
                return null;
            }
        }
    }
}

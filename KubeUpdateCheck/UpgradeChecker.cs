﻿using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Models;
using k8s;
using k8s.Models;
using KubeUpdateCheck.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace KubeUpdateCheck
{
    class UpgradeChecker
    {
        private static readonly string BaseVersion = @"(([0-9]+)\.([0-9]+)(?:\.([0-9]+))?)";
        private static readonly Regex DefaultVersionParse = new Regex(string.Format("^{0}$", BaseVersion), RegexOptions.Compiled);

        private IKubernetes kubernetes;

        public UpgradeChecker(IKubernetes kubernetes)
        {
            this.kubernetes = kubernetes;
        }

        public void PerformUpdate()
        {
            var deployments = kubernetes.ListDeploymentForAllNamespaces();

            Console.WriteLine("{0} deployments to be checked.", deployments.Items.Count);

            var registries = from deployment in deployments.Items
                             from container in deployment.Spec.Template.Spec.Containers
                             let decomposedImage = ImageReference.Parse(container.Image)
                             where ShouldProcess(deployment, decomposedImage)
                             let versionMatchString = deployment.Metadata.Annotations
                             group new { Deployment = deployment, Container = container, Image = decomposedImage, VersionString = deployment.Metadata.Annotations }
                             by decomposedImage.FullyQualifiedRegistry();

            ICollection<ContainerToUpdate> containerUpdates = new List<ContainerToUpdate>();

            Console.WriteLine("{0} deployments should be processed.", registries.Count());

            foreach (var registry in registries)
            {
                IRegistryClient registryClient = new RegistryClientConfiguration(registry.Key.Host).CreateClient();
                var images = from r in registry
                             group r
                             by r.Image;
                foreach (var image in images)
                {
                    ListImageTagsParameters tagsParameters = new ListImageTagsParameters
                    {
                        Number = 100
                    };
                    var result = registryClient.Tags.ListImageTagsAsync(image.Key.ImageName, tagsParameters);
                    try
                    {
                        result.Wait();
                    } catch (AggregateException e) {
                        throw new Exception(string.Format("Failed to list image tags for '{0}' at registry '{1}'.", image.Key.ImageName, image.Key.Registry), e.InnerException);
                    }

                    foreach (var container in image)
                    {
                        var matcher = ExtractMatchString(container.Deployment, container.Container.Name);
                        var start = matcher.Match(container.Image.Version);
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
                            Console.WriteLine("Upgrade deployment {0} container {1} from version {2} to {3}.", container.Deployment.Metadata.Name, container.Container.Name, container.Image.Version, upgradeTarget);
                            var toVersion = container.Image.WithVersion(upgradeTarget);
                            container.Container.Image = toVersion.ToString();
                            containerUpdates.Add(new ContainerToUpdate()
                            {
                                ContainerName = container.Container.Name,
                                FromVersion = container.Image,
                                ToVersion = toVersion
                            });

                            kubernetes.ReplaceNamespacedDeployment(container.Deployment, container.Deployment.Metadata.Name, container.Deployment.Metadata.NamespaceProperty);
                        }
                    }
                }
            }
            var mailConfig = EmailConfig.GetFromEnvironment();
            if (containerUpdates.Count > 0 && mailConfig.ShouldSend)
            {
                MailMessage mailMessage = new MailMessage
                {
                    DeliveryNotificationOptions = DeliveryNotificationOptions.Never,
                    Body = new EmailNotificationBuilder().BuildMessage(containerUpdates),
                    Subject = string.Format("{0} containers updated", containerUpdates.Count)
                };
                mailMessage.To.Add(mailConfig.ToAddress);
                mailMessage.From = mailConfig.FromAddress;
                SmtpClient smtpClient = new SmtpClient(mailConfig.RelayHost, mailConfig.RelayPort);
                smtpClient.Send(mailMessage);
            }
        }

        private bool ShouldProcess(V1Deployment deployment, ImageReference imageReference)
        {
            string shouldSkip = GetAnnotation(deployment, "skip");
            bool skipValue;
            bool hasSkipValue = bool.TryParse(shouldSkip, out skipValue);
            if (hasSkipValue && skipValue)
            {
                return false;
            }
            return (
                deployment.Metadata.NamespaceProperty != "kube-system" &&
                (imageReference != null && imageReference.Version != "latest"));
                
        }

        private Regex ExtractMatchString(V1Deployment deployment, string containerName)
        {
            string value = GetContainerAnnotationWithFallback(deployment, "version-match", containerName);
            if (value == null)
            {
                return DefaultVersionParse;
            }

            string regex = string.Format("^{0}$", value.Replace("{version}", BaseVersion));

            return new Regex(regex);
        }

        private static string GetAnnotation(V1Deployment deployment, string settingName)
        {
            string name = string.Format("net.technowizardry.upgrade/{0}", settingName);
            string value;
            var annotations = deployment.Spec.Template.Metadata.Annotations;
            if (annotations == null)
            {
                return null;
            }
            else if (annotations.TryGetValue(name, out value))
            {
                return value;
            }

            return null;
        }

        private string GetContainerAnnotationWithFallback(V1Deployment deployment, string settingName, string containerName)
        {
            string value = GetAnnotation(deployment, settingName + "-" + containerName);

            if (value != null)
            {
                return value;
            }

            return GetAnnotation(deployment, settingName);
        }
    }
}

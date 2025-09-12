using Godelian.Models;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Godelian.Helpers
{
    internal class HostFetcher
    {
        private static HttpClient client = InitHttpClient();

        private static HttpClient InitHttpClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            return client;
        }

        ulong IPIndex;
        string IPAddress;

        public HostFetcher(ulong IPIndex, string IPAddress)
        {
            this.IPIndex = IPIndex;
            this.IPAddress = IPAddress;
        }

        public static void SetTimeout(int seconds)
        {
            client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(seconds);
        }

        public async Task<HostRecordModel?> Fetch()
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync($"http://{IPAddress}");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string responseBody = await response.Content.ReadAsStringAsync();

                HostRecordModel hostRecord = new HostRecordModel
                {
                    IPIndex = this.IPIndex,
                    IPAddress = this.IPAddress,
                    Hostname = response.RequestMessage?.RequestUri?.Host ?? "",
                    FoundByClientId = Client.ClientState.ClientID!,
                    Features = ExtractFeatures(responseBody),
                };

                return hostRecord;
            }
            catch (Exception)
            {
                return null;
            }
        }
        private bool TryMakeAbsolute(string baseAddress, string candidate, out string absolute)
        {
            absolute = candidate;
            if (Uri.TryCreate(candidate, UriKind.Absolute, out _))
                return true;

            if (Uri.TryCreate(baseAddress, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, candidate, out var abs))
            {
                absolute = abs.ToString();
                return true;
            }
            return false;
        }

        public List<Feature> ExtractFeatures(string responseBody)
        {
            List<Feature> features = new();
            if (string.IsNullOrEmpty(responseBody)) return features;

            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(responseBody);

            StringBuilder textBuffer = new();
            HashSet<string> headings = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> links = new(StringComparer.OrdinalIgnoreCase); 
            HashSet<string> images = new(StringComparer.OrdinalIgnoreCase);
            
            // Title
            HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null)
            {
                string titleText = HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
                if (titleText.Length > 0)
                {
                    features.Add(new Feature { Content = titleText, Type = FeatureType.Title });
                }
            }

            foreach (HtmlNode node in htmlDocument.DocumentNode.Descendants())
            {
                // Collect visible text
                if (node.NodeType == HtmlNodeType.Text)
                {
                    string text = HtmlEntity.DeEntitize(node.InnerText);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    string parentName = node.ParentNode?.Name?.ToLowerInvariant() ?? "";

                    // Skip non-visible or undesirable parents
                    if (parentName is "script" or "style" or "noscript" or "head" or "meta" or "svg" or "title")
                        continue;

                    // Check if it's a heading
                    if (parentName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                    {
                        string headingText = text.Trim();
                        if (headingText.Length > 0 && headings.Add(headingText))
                        {
                            features.Add(new Feature { Content = headingText, Type = FeatureType.Heading });
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textBuffer.Append(text).Append(' ');
                    }
                }
                // Collect anchor hrefs
                else if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
                {
                    string? href = node.GetAttributeValue("href", null);
                    if (string.IsNullOrWhiteSpace(href)) continue;

                    href = href.Trim();

                    if (href.StartsWith("#") ||
                        href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    href = HtmlEntity.DeEntitize(href);

                    if (TryMakeAbsolute($"http://{IPAddress}", href, out var absolute))
                    {
                        links.Add(absolute);
                    }
                    else
                    {
                        links.Add(href);
                    }
                }
                // Collect image src
                else if (node.Name.Equals("img", StringComparison.OrdinalIgnoreCase))
                {
                    string? src = node.GetAttributeValue("src", null);
                    if (string.IsNullOrWhiteSpace(src)) continue;

                    src = HtmlEntity.DeEntitize(src.Trim());

                    // Skip inline data URIs
                    if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (TryMakeAbsolute($"http://{IPAddress}", src, out var absoluteImg))
                    {
                        images.Add(absoluteImg);
                    }
                    else
                    {
                        images.Add(src);
                    }
                }
            }

            // Normalize whitespace in collected text
            string normalizedText = Regex.Replace(textBuffer.ToString(), @"\s+", " ").Trim();
            if (normalizedText.Length > 0)
            {
                features.Add(new Feature { Content = normalizedText, Type = FeatureType.Text });
            }

            foreach (string heading in headings)
            {
                features.Add(new Feature { Content = heading, Type = FeatureType.Heading });
            }

            foreach (string link in links)
            {
                features.Add(new Feature { Content = link, Type = FeatureType.Link });
            }

            foreach (string img in images)
            {
                features.Add(new Feature { Content = img, Type = FeatureType.Image });
            }

            return features;
        }
    }
}

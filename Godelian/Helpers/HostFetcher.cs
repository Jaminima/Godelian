using Godelian.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        uint IPIndex;
        string IPAddress;

        public HostFetcher(uint IPIndex, string IPAddress)
        {
            this.IPIndex = IPIndex;
            this.IPAddress = IPAddress;
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

        // Single-pass feature extraction to avoid multiple scans over large HTML bodies.
        public List<Feature> ExtractFeatures(string responseBody)
        {
            List<Feature> features = new();
            if (string.IsNullOrEmpty(responseBody)) return features;

            StringBuilder textBuffer = new();
            StringBuilder tagBuffer = new();

            bool insideTag = false;
            bool insideScript = false;
            bool insideStyle = false;
            bool capturingTitle = false;
            bool capturingHeading = false;
            string? currentHeadingTag = null; // h1..h6

            void FlushPlainText()
            {
                if (capturingTitle || capturingHeading) return; // Wait until closing tag to add as specific feature
                if (textBuffer.Length == 0) return;
                string content = NormalizeWhitespace(textBuffer.ToString());
                textBuffer.Clear();
                if (content.Length == 0) return;
                // Heuristic: ignore extremely short snippets (like stray punctuation)
                if (content.Trim().Length < 3) return;
                features.Add(new Feature { Type = FeatureType.Text, Content = content });
            }

            static string NormalizeWhitespace(string s)
            {
                Span<char> bufferSpan = stackalloc char[Math.Min(s.Length, 4096)];
                // Fallback to simple path for long strings.
                if (s.Length > bufferSpan.Length)
                {
                    return string.Join(' ', s.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));
                }
                int w = 0;
                bool prevSpace = false;
                foreach (char c in s)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        if (!prevSpace && w > 0)
                        {
                            bufferSpan[w++] = ' ';
                        }
                        prevSpace = true;
                    }
                    else
                    {
                        bufferSpan[w++] = c;
                        prevSpace = false;
                    }
                }
                if (w == 0) return string.Empty;
                if (bufferSpan[w - 1] == ' ') w--;
                return new string(bufferSpan[..w]);
            }

            Dictionary<string, string> ParseAttributes(string tagContent)
            {
                Dictionary<string, string> dict = new(StringComparer.OrdinalIgnoreCase);
                int i = 0;
                while (i < tagContent.Length)
                {
                    // Skip whitespace
                    while (i < tagContent.Length && char.IsWhiteSpace(tagContent[i])) i++;
                    int startName = i;
                    while (i < tagContent.Length && (char.IsLetterOrDigit(tagContent[i]) || tagContent[i] == '-' || tagContent[i] == '_' || tagContent[i] == ':')) i++;
                    if (i == startName) break;
                    string name = tagContent[startName..i];
                    while (i < tagContent.Length && char.IsWhiteSpace(tagContent[i])) i++;
                    string value = "";
                    if (i < tagContent.Length && tagContent[i] == '=')
                    {
                        i++; // skip '='
                        while (i < tagContent.Length && char.IsWhiteSpace(tagContent[i])) i++;
                        if (i < tagContent.Length && (tagContent[i] == '"' || tagContent[i] == '\''))
                        {
                            char quote = tagContent[i++];
                            int startVal = i;
                            while (i < tagContent.Length && tagContent[i] != quote) i++;
                            value = tagContent[startVal..Math.Min(i, tagContent.Length)];
                            if (i < tagContent.Length && tagContent[i] == quote) i++;
                        }
                        else
                        {
                            int startVal = i;
                            while (i < tagContent.Length && !char.IsWhiteSpace(tagContent[i]) && tagContent[i] != '>' && tagContent[i] != '/') i++;
                            value = tagContent[startVal..i];
                        }
                    }
                    dict[name] = value;
                }
                return dict;
            }

            for (int idx = 0; idx < responseBody.Length; idx++)
            {
                char c = responseBody[idx];

                if (!insideTag)
                {
                    if (c == '<')
                    {
                        insideTag = true;
                        tagBuffer.Clear();
                        FlushPlainText();
                    }
                    else
                    {
                        if (!insideScript && !insideStyle)
                        {
                            // Capture only human-visible text segments
                            textBuffer.Append(c);
                        }
                    }
                }
                else
                {
                    if (c == '>')
                    {
                        insideTag = false;
                        string rawTag = tagBuffer.ToString();
                        bool isClosing = rawTag.StartsWith('/');
                        string tagContent = isClosing ? rawTag[1..] : rawTag;
                        // Extract tag name
                        string tagName = tagContent;
                        int spaceIdx = tagContent.IndexOfAny([' ', '\t', '\r', '\n', '/']);
                        if (spaceIdx >= 0)
                        {
                            tagName = tagContent[..spaceIdx];
                        }
                        tagName = tagName.ToLowerInvariant();

                        if (isClosing)
                        {
                            if (tagName == "script") insideScript = false;
                            else if (tagName == "style") insideStyle = false;
                            else if (tagName == "title" && capturingTitle)
                            {
                                string title = NormalizeWhitespace(textBuffer.ToString());
                                textBuffer.Clear();
                                if (title.Length > 0)
                                    features.Add(new Feature { Type = FeatureType.Title, Content = title });
                                capturingTitle = false;
                            }
                            else if (capturingHeading && tagName == currentHeadingTag)
                            {
                                string heading = NormalizeWhitespace(textBuffer.ToString());
                                textBuffer.Clear();
                                if (heading.Length > 0)
                                    features.Add(new Feature { Type = FeatureType.Heading, Content = heading });
                                capturingHeading = false;
                                currentHeadingTag = null;
                            }
                        }
                        else
                        {
                            // Opening tag or self-closing
                            string remainder = tagContent.Length > tagName.Length ? tagContent[tagName.Length..] : string.Empty;
                            var attrs = ParseAttributes(remainder);

                            switch (tagName)
                            {
                                case "script":
                                    insideScript = true;
                                    if (attrs.TryGetValue("src", out var src) && src.Length > 0)
                                    {
                                        features.Add(new Feature { Type = FeatureType.Script, Content = src });
                                    }
                                    break;
                                case "style":
                                    insideStyle = true;
                                    break;
                                case "title":
                                    capturingTitle = true;
                                    break;
                                case "img":
                                    if (attrs.TryGetValue("src", out var imgSrc) && imgSrc.Length > 0)
                                    {
                                        string alt = attrs.TryGetValue("alt", out var altVal) ? altVal : string.Empty;
                                        string combined = string.IsNullOrWhiteSpace(alt) ? imgSrc : $"{alt} ({imgSrc})";
                                        features.Add(new Feature { Type = FeatureType.Image, Content = NormalizeWhitespace(combined) });
                                    }
                                    break;
                                case "a":
                                    if (attrs.TryGetValue("href", out var href) && href.Length > 0)
                                    {
                                        features.Add(new Feature { Type = FeatureType.Link, Content = href });
                                    }
                                    break;
                                default:
                                    if (tagName.Length == 2 && tagName[0] == 'h' && char.IsDigit(tagName[1]) && tagName[1] >= '1' && tagName[1] <= '6')
                                    {
                                        capturingHeading = true;
                                        currentHeadingTag = tagName;
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // Still inside tag declaration
                        tagBuffer.Append(c);
                    }
                }
            }

            // Final flush for any stray text (not title/heading because would need closing tag to finalize)
            FlushPlainText();

            return features;
        }
    }
}

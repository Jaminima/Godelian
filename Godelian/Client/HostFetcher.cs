using Godelian.Models;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using static System.Net.Mime.MediaTypeNames;

namespace Godelian.Client
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
        int Iteration;
        HostRequestMethod HostRequestMethod;

        public HostFetcher(ulong IPIndex, string IPAddress, int iteration, HostRequestMethod hostRequestMethod)
        {
            this.IPIndex = IPIndex;
            this.IPAddress = IPAddress;
            this.Iteration = iteration;
            this.HostRequestMethod = hostRequestMethod;
        }

        public static void SetTimeout(int seconds)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(seconds);
        }

        public async Task<HostRecordModelDTO?> Fetch()
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync($"{(HostRequestMethod == HostRequestMethod.HTTP ? "http" : "https")}://{IPAddress}");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string responseBody = await response.Content.ReadAsStringAsync();

                string hostnameFromResponse = response.RequestMessage?.RequestUri?.Host ?? "";

                // Normalize / reject unreliable hostnames that can come from redirects or router responses.
                if (!string.IsNullOrEmpty(hostnameFromResponse))
                {
                    string hostLower = hostnameFromResponse.Trim().ToLowerInvariant();

                    // Consider "localhost" unreliable
                    if (hostLower == "localhost")
                    {
                        hostnameFromResponse = IPAddress;
                    }
                    // If the host is a numeric IPv4 address but not the one we requested, prefer the original IP.
                    else if (Regex.IsMatch(hostLower, @"^\d{1,3}(\.\d{1,3}){3}$") && hostLower != IPAddress)
                    {
                        hostnameFromResponse = IPAddress;
                    }
                }

                // If HTTPS and hostname looks like an IP (or empty), try to derive hostname from TLS certificate
                if (HostRequestMethod == HostRequestMethod.HTTPS && (string.IsNullOrEmpty(hostnameFromResponse) || hostnameFromResponse == IPAddress))
                {
                    string? certHost = await GetHostnameFromCertificateAsync(IPAddress);
                    if (!string.IsNullOrEmpty(certHost))
                    {
                        hostnameFromResponse = certHost;
                    }
                }

                List<HeaderRecordDTO> headers = response.Headers.Select(x=>new HeaderRecordDTO() { Name = x.Key, Value = x.Value.FirstOrDefault() ?? "" }).Where(x=>!String.IsNullOrWhiteSpace(x.Value)).ToList();

                HostRecordModelDTO hostRecord = new HostRecordModelDTO
                {
                    IPIndex = IPIndex,
                    IPAddress = IPAddress,
                    Iteration = Iteration,
                    Hostname = hostnameFromResponse,
                    FoundByClientId = ClientState.ClientID!,
                    Features = ExtractFeatures(responseBody),
                    HostRequestMethod = HostRequestMethod,
                    HeaderRecords = headers
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

            if (Uri.TryCreate(baseAddress, UriKind.Absolute, out Uri? baseUri) &&
                Uri.TryCreate(baseUri, candidate, out Uri? abs))
            {
                absolute = abs.ToString();
                return true;
            }
            return false;
        }

        private async Task<string?> GetHostnameFromCertificateAsync(string ip)
        {
            // Attempt to connect to the IP on port 443 and fetch the remote certificate
            using TcpClient tcp = new TcpClient();
            try
            {
                var connectTask = tcp.ConnectAsync(ip, 443);
                var timeoutTask = Task.Delay(3000);
                var finished = await Task.WhenAny(connectTask, timeoutTask);
                if (finished != connectTask)
                {
                    // timed out
                    return null;
                }

                await connectTask;

                using NetworkStream ns = tcp.GetStream();
                using SslStream ssl = new SslStream(ns, false, (sender, certificate, chain, sslPolicyErrors) => true);

                // Use the IP as the target host for the handshake. This will still retrieve the certificate.
                var authTask = ssl.AuthenticateAsClientAsync(ip, null, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, false);
                var authFinished = await Task.WhenAny(authTask, Task.Delay(3000));
                if (authFinished != authTask)
                {
                    return null;
                }

                await authTask;

                var remoteCert = ssl.RemoteCertificate;
                if (remoteCert == null) return null;

                var cert2 = new X509Certificate2(remoteCert);

                // Try SAN DNS first
                try
                {
                    string sanDns = cert2.GetNameInfo(X509NameType.DnsFromAlternativeName, false);
                    if (!string.IsNullOrEmpty(sanDns))
                        return sanDns;
                }
                catch { }

                // Fall back to CN
                try
                {
                    string cn = cert2.GetNameInfo(X509NameType.SimpleName, false);
                    if (!string.IsNullOrEmpty(cn))
                        return cn;
                }
                catch { }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public List<FeatureDTO> ExtractFeatures(string responseBody)
        {
            List<FeatureDTO> features = new();
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
                    features.Add(new FeatureDTO { Content = titleText, Type = FeatureType.Title });
                }
            }

            foreach (HtmlNode node in htmlDocument.DocumentNode.Descendants())
            {
                // Collect visible text
                if (node.NodeType == HtmlNodeType.Text)
                {
                    string parentName = node.ParentNode?.Name?.ToLowerInvariant() ?? ""; 
                    
                    // Skip non-visible or undesirable parents
                    if (parentName is "script" or "style" or "noscript" or "head" or "meta" or "svg" or "title")
                        continue;

                    string text = HtmlEntity.DeEntitize(node.InnerText);

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

                    if (TryMakeAbsolute($"http://{IPAddress}", href, out string? absolute))
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

                    if (TryMakeAbsolute($"http://{IPAddress}", src, out string? absoluteImg))
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
                features.Add(new FeatureDTO { Content = normalizedText, Type = FeatureType.Text });
            }

            //foreach (string heading in headings)
            //{
            //    features.Add(new FeatureDTO { Content = heading, Type = FeatureType.Heading });
            //}

            foreach (string link in links)
            {
                features.Add(new FeatureDTO { Content = link, Type = FeatureType.Link });
            }

            foreach (string img in images)
            {
                features.Add(new FeatureDTO { Content = img, Type = FeatureType.Image });
            }

            return features;
        }
    }
}

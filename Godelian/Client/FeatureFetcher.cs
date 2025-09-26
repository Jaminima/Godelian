using Godelian.Models;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Client
{
    internal class FeatureFetcher
    {
        private static HttpClient client = InitHttpClient();
        private static HttpClient InitHttpClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            return client;
        }


        private FeatureDTO Feature;
        private HostRecordModelDTO HostRecord;
        public FeatureFetcher(FeatureDTO feature)
        {
            Feature = feature;
            HostRecord = feature.HostRecord!;
        }

        private bool GetAbsoluteUriFromUnsure(string path, out Uri? uri)
        {
            uri = null;

            if (path.StartsWith("/"))
            {
                return Uri.TryCreate((HostRecord.HostRequestMethod == HostRequestMethod.HTTP ? "http" : "https") + "://" + HostRecord.Hostname + path, UriKind.Absolute, out uri);
            }

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                return Uri.TryCreate(path, UriKind.Absolute, out uri);
            }

            return false;
        }

        public async Task<FeatureDTO[]?> GetSubFeatures()
        {
            try
            {
                switch (Feature.Type)
                {
                    case FeatureType.Image:
                        {
                            if (!GetAbsoluteUriFromUnsure(Feature.Content, out Uri imgUri))
                                return null;

                            HttpResponseMessage resp = await client.GetAsync(imgUri);
                            if (!resp.IsSuccessStatusCode)
                                return null;

                            byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
                            string b64 = Convert.ToBase64String(bytes);

                            return new FeatureDTO[] {new FeatureDTO
                            {
                                Base64Content = b64,
                                Type = FeatureType.Base64
                            }};
                        }
                    case FeatureType.Link:
                        {
                            if (!GetAbsoluteUriFromUnsure(Feature.Content, out Uri linkUri))
                                return null;

                            HttpResponseMessage resp = await client.GetAsync(linkUri);
                            if (!resp.IsSuccessStatusCode)
                                return null;

                            string body = await resp.Content.ReadAsStringAsync();

                            HostFetcher hostFetcher = new HostFetcher(0, linkUri.Host, 0, Feature.HostRecord!.HostRequestMethod);
                            List<FeatureDTO> subFeatures = hostFetcher.ExtractFeatures(body);

                            return subFeatures.Select(f => new FeatureDTO
                            {
                                Content = f.Content,
                                Type = f.Type,
                            }).ToArray();
                        }
                    default:
                        break;
                }
            }
            catch
            {
                // Swallow and return the feature as-is on failure
            }

            return null;
        }
    }
}

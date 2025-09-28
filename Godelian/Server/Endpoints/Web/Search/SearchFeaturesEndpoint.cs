using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints.Web.Search.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Web.Search
{
    internal static class SearchFeaturesEndpoint
    {
        public static async Task<ServerResponse<SearchResults>> SearchFeatures(ClientRequest<SearchQuery> clientRequest)
        {
            List<Feature> features = await DB.Collection<Feature>().Aggregate().Match(new BsonDocument("$text", new BsonDocument { { "$search", clientRequest.Data!.Query } })).Limit(10).ToListAsync();

            List<string> parentFeatureIDs = features.Where(f => !string.IsNullOrEmpty(f.ParentFeatureID)).Select(f => f.ParentFeatureID!).Distinct().ToList();
            List<Feature> parentFeatures = await DB.Collection<Feature>().Find(f => parentFeatureIDs.Contains(f.ID)).ToListAsync();

            List<string> hostRecordIDs = features.Select(f => f.HostRecordID).Concat(parentFeatures.Select(pf => pf.HostRecordID)).Distinct().ToList();
            List<HostRecordModel> hostRecords = await DB.Collection<HostRecordModel>().Find(hr => hostRecordIDs.Contains(hr.ID)).ToListAsync();

            FeatureDTO[] featureDTOs = features.Select(f => new FeatureDTO
            {
                ID = f.ID,
                Type = f.Type,
                Content = f.Content,
                Base64Content = f.Base64Content,
                Elaborated = f.Elaborated,
            }).ToArray();

            string query = clientRequest.Data?.Query ?? string.Empty;

            foreach (var featureDTO in featureDTOs)
            {
                // Attach virtual ParentFeature
                Feature? parentFeature = parentFeatures.FirstOrDefault(pf => pf.ID == features.First(f => f.ID == featureDTO.ID).ParentFeatureID);
                if (parentFeature != null)
                {
                    featureDTO.ParentFeature = new FeatureDTO
                    {
                        ID = parentFeature.ID,
                        Type = parentFeature.Type,
                        Content = parentFeature.Content,
                        Base64Content = parentFeature.Base64Content,
                        Elaborated = parentFeature.Elaborated,
                    };
                }

                // Attach HostRecord
                HostRecordModel? hostRecord = hostRecords.FirstOrDefault(hr => hr.ID == features.First(f => f.ID == featureDTO.ID).HostRecordID);
                if (hostRecord != null)
                {
                    featureDTO.HostRecord = new HostRecordModelDTO
                    {
                        ID = hostRecord.ID,
                        IPIndex = hostRecord.IPIndex,
                        Iteration = hostRecord.Iteration,
                        IPAddress = hostRecord.IPAddress,
                        Hostname = hostRecord.Hostname,
                        FoundByClientId = hostRecord.FoundByClientId,
                        FoundAt = hostRecord.FoundAt,
                        FeaturesElaborated = hostRecord.FeaturesElaborated,
                        HostRequestMethod = hostRecord.HostRequestMethod
                    };
                }

                // Build content snippets around the query for text-based features
                if (!string.IsNullOrWhiteSpace(query))
                {
                    if (featureDTO.Type is FeatureType.Text or FeatureType.Title)
                    {
                        featureDTO.Content = BuildSnippet(featureDTO.Content, query);
                    }

                    if (featureDTO.ParentFeature != null && featureDTO.ParentFeature.Type is FeatureType.Text or FeatureType.Title)
                    {
                        featureDTO.ParentFeature.Content = BuildSnippet(featureDTO.ParentFeature.Content, query);
                    }
                }
            }

            return new ServerResponse<SearchResults>
            {
                Success = true,
                Data = new SearchResults
                {
                    Features = featureDTOs
                }
            };

        }

        private static string? BuildSnippet(string? content, string query, int context = 80)
        {
            if (string.IsNullOrEmpty(content)) return content;
            if (string.IsNullOrWhiteSpace(query)) return content;

            // Try to find the query or one of its tokens
            (int index, int length) = FindFirstMatch(content, query);

            if (index < 0)
            {
                // Fallback: return a head snippet if content is long
                if (content.Length <= context * 2) return content;
                return content[..(context * 2)] + "...";
            }

            int start = Math.Max(0, index - context);
            int end = Math.Min(content.Length, index + length + context);

            // Expand to word boundaries where possible
            if (start > 0)
            {
                int prevSpace = content.LastIndexOf(' ', start - 1);
                if (prevSpace >= 0) start = prevSpace + 1;
            }
            if (end < content.Length)
            {
                int nextSpace = content.IndexOf(' ', end);
                if (nextSpace >= 0) end = nextSpace;
            }

            string snippet = content[start..end];

            if (start > 0) snippet = "..." + snippet;
            if (end < content.Length) snippet += "...";
            snippet = snippet.Replace("\n", " ").Replace("\r", " "); // Remove new lines

            return snippet;
        }

        private static (int index, int length) FindFirstMatch(string content, string query)
        {
            // Direct match first
            int idx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return (idx, query.Length);

            // Try individual tokens (ignore very short tokens)
            foreach (var token in query.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim('"', '\'', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '<', '>', '/', '\\');
                if (t.Length < 2) continue;
                idx = content.IndexOf(t, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) return (idx, t.Length);
            }

            return (-1, 0);
        }
    }
}

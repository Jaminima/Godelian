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

            foreach (var featureDTO in featureDTOs)
            {
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

    }
}

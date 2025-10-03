using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints.Client.Connection.DTOs;
using Godelian.Server.Endpoints.Client.FeatureRange.DTOs;
using Godelian.Server.Endpoints.Client.HostRecords.DTOs;
using Godelian.Server.Endpoints.Client.IPAddressing.DTOs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Client
{
    internal class ClientHandler
    {
        private readonly HTTPClient httpClient;
        private string? clientId;
        private int retryCount = 0;
        private const int maxRetries = 5;
        private bool isFirstLoop = true;

        public ClientHandler()
        {
            httpClient = new HTTPClient(Config.GodelianServerIP, Config.GodelianServerPort);
        }

        public async Task Start()
        {
            await Handshake();

            while (retryCount < maxRetries)
            {
                try
                {
                    await MainLoop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Something went wrong:");
                    Console.WriteLine(ex.ToString());
                    retryCount++;
                }
            }
        }

        private async Task MainLoop()
        {
            const double elaborateProbability = 0.95;

            while (true)
            {
                double searchOrElaborate = Rand._random.NextDouble();

                if (searchOrElaborate < elaborateProbability)
                {
                    await FetchAndSearchIPRange();
                }
                else
                {
                    await FetchAndElaborateFeatureRange();
                }
            }
        }

        private async Task FetchAndSearchIPRange()
        {
            ServerResponse<NewIPRange> newIPRange = await RequestNewIPRange();

            if (newIPRange.Success)
            {
                Console.WriteLine($"{newIPRange.Message} Start={newIPRange.Data.Start}, Count={newIPRange.Data.Count}");
            }
            else
            {
                Console.WriteLine($"Failed to get new IP range: {newIPRange.Message}");
            }

            // Enumerate IPs lazily and throttle concurrency to avoid resource exhaustion
            IEnumerable<KeyValuePair<ulong, string>> ipSequence = IPAddressEnumerator.EnumerateIPRange(newIPRange.Data.Start, newIPRange.Data.Count);

            HostFetcher.SetTimeout(newIPRange.Data.IsValidation ? 10 : 5);

            ConcurrentBag<HostRecordModelDTO> results = new ConcurrentBag<HostRecordModelDTO>();
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Config.MaxConcurrentFetches
            };

            await Parallel.ForEachAsync(ipSequence, parallelOptions, async (kvp, ct) =>
            {
                //HostRecordModelDTO? hostHTTP = await new HostFetcher(kvp.Key, kvp.Value, newIPRange.Data.Iteration, HostRequestMethod.HTTP).Fetch();
                HostRecordModelDTO? hostHTTPS = await new HostFetcher(kvp.Key, kvp.Value, newIPRange.Data.Iteration, HostRequestMethod.HTTPS).Fetch();

                //if (hostHTTP != null)
                //{
                //    results.Add(hostHTTP);
                //}
                if (hostHTTPS != null)
                {
                    results.Add(hostHTTPS);
                }
            });

            await SendHostRecords(results.ToList(), newIPRange.Data.IPBatchID);

            Console.WriteLine("Found Hosts:" + results.Count);
        }

        private async Task FetchAndElaborateFeatureRange()
        {
            ServerResponse<FeatureRangeCollection> featureRangeResponse = await httpClient.SendRequest<object, FeatureRangeCollection>(new ClientRequest<object>
            {
                RequestType = ClientRequestType.NewFeatureRange,
                ClientId = clientId,
                ClientNickname = Config.GodelianNickname,
                TaskId = Config.TaskSlot
            });

            if (featureRangeResponse.Success)
            {
                Console.WriteLine($"Fetched {featureRangeResponse.Data!.featureRecords.Length} feature range records.");
            }
            else
            {
                Console.WriteLine($"Failed to fetch feature range records: {featureRangeResponse.Message}");
                return;
            }

            ConcurrentBag<FeatureDTO> newFeatures = new ConcurrentBag<FeatureDTO>();

            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Config.MaxConcurrentFetches
            };

            await Parallel.ForEachAsync(featureRangeResponse.Data.featureRecords, parallelOptions, async (feature, ct) =>
            {
                FeatureFetcher fetcher = new FeatureFetcher(feature);

                FeatureDTO[]? updatedFeatures = await fetcher.GetSubFeatures();

                if (updatedFeatures == null || updatedFeatures.Length == 0)
                    return;

                foreach (FeatureDTO f in updatedFeatures)
                {
                    f.ParentFeature = feature;
                    newFeatures.Add(f);
                }
            });

            await SendFeatureRange(new FeatureRangeCollection() { featureRecords = newFeatures.ToArray() });

            Console.WriteLine("Updated Features:" + newFeatures.Count);
        }

        private async Task Handshake()
        {
            ServerResponse<ClientConnectsResponse> connectToServer = await ConnectToServer();

            Console.WriteLine($"Connected As ClientID '{connectToServer.Data.AssignedClientId}'");

            clientId = connectToServer.Data.AssignedClientId;
            // Store globally for other helpers that rely on it
            ClientState.ClientID = clientId;
        }

        private async Task<ServerResponse<ClientConnectsResponse>> ConnectToServer()
        {
            ClientRequest<object> req = new ClientRequest<object>
            {
                RequestType = ClientRequestType.Connect,
                ClientNickname = Config.GodelianNickname,
                TaskId = Config.TaskSlot
            };

            return await httpClient.SendRequest<object,ClientConnectsResponse>(req);
        }

        private async Task<ServerResponse<NewIPRange>> RequestNewIPRange()
        {
            ClientRequest<object> req = new ClientRequest<object>
            {
                RequestType = ClientRequestType.NewIpRange,
                ClientId = clientId,
                ClientNickname = Config.GodelianNickname,
                TaskId = Config.TaskSlot
            };
            return await httpClient.SendRequest<object, NewIPRange>(req);
        }

        private async Task<ServerResponse<SubmitHostRecordsResponse>> SendHostRecords(List<HostRecordModelDTO> records, string ipBatchID)
        {
            ClientRequest<SubmitHostRecordsRequest> req = new ClientRequest<SubmitHostRecordsRequest>
            {
                RequestType = ClientRequestType.SubmitIpRange,
                ClientId = clientId,
                ClientNickname = Config.GodelianNickname,
                TaskId = Config.TaskSlot,
                Data = new SubmitHostRecordsRequest() { HostRecords = records, IPBatchID = ipBatchID }
            };

            return await httpClient.SendRequest<SubmitHostRecordsRequest, SubmitHostRecordsResponse>(req);
        }

        private async Task<ServerResponse<FeatureRangeCollection>> SendFeatureRange(FeatureRangeCollection updatedHosts)
        {
            ClientRequest<FeatureRangeCollection> req = new ClientRequest<FeatureRangeCollection>
            {
                RequestType = ClientRequestType.SubmitFeatureRange,
                ClientId = clientId,
                ClientNickname = Config.GodelianNickname,
                TaskId = Config.TaskSlot,
                Data = updatedHosts
            };
            return await httpClient.SendRequest<FeatureRangeCollection, FeatureRangeCollection>(req);
        }
    }
}

using Godelian.Endpoints.Connection.DTOs;
using Godelian.Endpoints.HostRecords.DTOs;
using Godelian.Endpoints.IPAddreessing.DTOs;
using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking;
using Godelian.Networking.DTOs;
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
            while (true)
            {
                ServerResponse<NewIPRange> newIPRange = await RequestNewIPRange();

                if (newIPRange.Success)
                {
                    Console.WriteLine($"Received New IP Range: Start={newIPRange.Data.Start}, Count={newIPRange.Data.Count}");
                }
                else
                {
                    Console.WriteLine($"Failed to get new IP range: {newIPRange.Message}");
                }

                // Enumerate IPs lazily and throttle concurrency to avoid resource exhaustion
                IEnumerable<KeyValuePair<uint, string>> ipSequence = IPAddressEnumerator.EnumerateIPRange(newIPRange.Data.Start, newIPRange.Data.Count);

                ConcurrentBag<HostRecordModel> results = new ConcurrentBag<HostRecordModel>();
                ParallelOptions parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Config.MaxConcurrentFetches
                };

                await Parallel.ForEachAsync(ipSequence, parallelOptions, async (kvp, ct) =>
                {
                    HostRecordModel? host = await new HostFetcher(kvp.Key, kvp.Value).Fetch();
                    if (host != null)
                    {
                        results.Add(host);
                    }
                });

                await SendHostRecords(results.ToList(), newIPRange.Data.IPBatchID);

                Console.WriteLine("Found Hosts:" + results.Count);
            }
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
                ClientNickname = Config.GodelianNickname
            };

            return await httpClient.SendRequest<object,ClientConnectsResponse>(req);
        }

        private async Task<ServerResponse<NewIPRange>> RequestNewIPRange()
        {
            ClientRequest<object> req = new ClientRequest<object>
            {
                RequestType = ClientRequestType.NewIpRange,
                ClientId = clientId,
                ClientNickname = Config.GodelianNickname
            };
            return await httpClient.SendRequest<object, NewIPRange>(req);
        }

        private async Task<ServerResponse<SubmitHostRecordsResponse>> SendHostRecords(List<HostRecordModel> records, string ipBatchID)
        {
            ClientRequest<SubmitHostRecordsRequest> req = new ClientRequest<SubmitHostRecordsRequest>
            {
                RequestType = ClientRequestType.SubmitIpRange,
                ClientId = clientId,
                ClientNickname = Config.GodelianNickname,
                Data = new SubmitHostRecordsRequest() { HostRecords = records, IPBatchID = ipBatchID }
            };

            return await httpClient.SendRequest<SubmitHostRecordsRequest, SubmitHostRecordsResponse>(req);
        }
    }
}

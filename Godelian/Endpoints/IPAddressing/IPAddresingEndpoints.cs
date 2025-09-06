using Godelian.Endpoints.Connection.DTOs;
using Godelian.Endpoints.IPAddreessing.DTOs;
using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Godelian.Models.IPBatchValidation;

namespace Godelian.Endpoints.IPAddreessing
{
    internal static class IPAddresingEndpoints
    {
        private const int AddressStepSize = 1024 * 4;

        private static async Task<IPBatch?> GetStaleIPBatch()
        {
            DateTime tenMinsAgo = DateTime.UtcNow.AddMinutes(-10);

            return await DB.Find<IPBatch>()
                           .Match(x => !x.Completed && x.IssuedAt < tenMinsAgo)
                           .Sort(x => x.IssuedAt, Order.Ascending)
                           .ExecuteFirstAsync();
        }

        private static async Task<IPBatch> GetNextIPBatch(string clientId)
        {
            IPBatch? latestBatch = await DB.Find<IPBatch>()
                                      .Sort(x => x.Start, Order.Descending)
                                      .ExecuteFirstAsync();

            uint nextStart = latestBatch != null ? (uint)(latestBatch.Start + latestBatch.Count) : IPAddressEnumerator.FirstIPIndex;

            return new IPBatch
            {
                IssuedToClientId = clientId,
                Start = nextStart,
                Count = AddressStepSize,
                StartIP = IPAddressEnumerator.GetIndexAsIP(nextStart),
                EndIP = IPAddressEnumerator.GetIndexAsIP(nextStart + AddressStepSize - 1),
                IssuedAt = DateTime.UtcNow,
                Completed = false,
                CompletedAt = null
            };
        }

        private static async Task<IPBatch?> MaybeValidateIPBatch(string clientID)
        {
            int RandomThreshold = 10; // 10% chance to validate a batch
            int roll = Random.Shared.Next(0, 100);
            if (roll >= RandomThreshold) return null;

            long count = await DB.CountAsync<IPBatch>(x=>x.Completed && x.Validation.Status == ValidationStatus.NotValidated && x.IssuedToClientId != clientID);
            if (count == 0) return null;

            int skip = Random.Shared.Next(0, (int)count);

            return await DB.Find<IPBatch>()
                           .Match(x => x.Completed && x.Validation.Status == ValidationStatus.NotValidated && x.IssuedToClientId != clientID)
                           .Skip(skip)
                           .Limit(1)
                           .ExecuteFirstAsync();
        }

        public static async Task<ServerResponse<NewIPRange>> GetNewIPRange(ClientRequest<object> clientRequest)
        {
            ServerResponse<NewIPRange> response = new ServerResponse<NewIPRange>();

            IPBatch? batchToValidate = await MaybeValidateIPBatch(clientRequest.ClientId);

            if (batchToValidate != null)
            {
                batchToValidate.Validation.Status = ValidationStatus.Validating;
                batchToValidate.Validation.IssuedToClientId = clientRequest.ClientId!;
                batchToValidate.Validation.IssuedAt = batchToValidate.IssuedAt;
                batchToValidate.Validation.CompletedAt = DateTime.UtcNow;

                await batchToValidate.SaveAsync();

                response.Data = new NewIPRange()
                {
                    IPBatchID = batchToValidate.ID,
                    Count = batchToValidate.Count,
                    Start = batchToValidate.Start,
                };

                response.Message = "Validating IP range assigned";
            } else { 
                IPBatch? staleBatch = await GetStaleIPBatch();

                if (staleBatch != null)
                {

                    staleBatch.IssuedToClientId = clientRequest.ClientId!;
                    staleBatch.IssuedAt = DateTime.UtcNow;

                    await staleBatch.SaveAsync();

                    response.Data = new NewIPRange()
                    {
                        IPBatchID = staleBatch.ID,
                        Count = staleBatch.Count,
                        Start = staleBatch.Start,
                    };

                    response.Message = "Stale IP range assigned.";
                }
                else
                {
                    IPBatch nextBatch = await GetNextIPBatch(clientRequest.ClientId!);

                    await nextBatch.SaveAsync();

                    response.Data = new NewIPRange()
                    {
                        IPBatchID = nextBatch.ID,
                        Count = nextBatch.Count,
                        Start = nextBatch.Start
                    };

                    response.Message = "New IP range assigned.";
                }
            }

            response.Success = true;

            Console.WriteLine($"Assigned IP range {IPAddressEnumerator.GetIndexAsIP(response.Data.Start)} - {IPAddressEnumerator.GetIndexAsIP((uint)(response.Data.Start + response.Data.Count - 1))} to {clientRequest.ClientNickname ?? clientRequest.ClientId}");

            return response;
        }
    }
}

using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server;
using Godelian.Server.Endpoints.Client.IPAddressing.DTOs;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Godelian.Models.IPBatchValidation;

namespace Godelian.Server.Endpoints.Client.IPAddressing
{
    internal static class IPAddresingEndpoints
    {
        private const int AddressStepSize = 1024 * 4;

        // Local cache for next-start index to avoid querying DB for every request
        private static readonly SemaphoreSlim NextIndexLock = new(1, 1);
        private static int? CachedIteration = null;
        private static ulong? NextStartIndex = null;

        private static async Task<IPBatch?> GetStaleIPBatch(int currentIteration)
        {
            DateTime tenMinsAgo = DateTime.UtcNow.AddMinutes(-10);

            return await DB.Find<IPBatch>()
                           .Match(x => !x.Completed && x.IssuedAt < tenMinsAgo && x.Iteration == currentIteration)
                           .Sort(x => x.IssuedAt, Order.Ascending)
                           .ExecuteFirstAsync();
        }

        private static async Task<IPBatch> GetNextIPBatch(string clientId, int currentIteration)
        {
            await NextIndexLock.WaitAsync();
            try
            {
                // Initialize cache for iteration if needed
                if (CachedIteration != currentIteration || !NextStartIndex.HasValue)
                {
                    IPBatch? latestBatch = await DB.Find<IPBatch>()
                                              .Match(x => x.Iteration == currentIteration)
                                              .Sort(x => x.ID, Order.Descending)
                                              .ExecuteFirstAsync();

                    NextStartIndex = latestBatch != null ? latestBatch.Start + latestBatch.Count : IPAddressEnumerator.FirstIPIndex;
                    CachedIteration = currentIteration;
                }

                ulong nextStart = NextStartIndex!.Value;
                NextStartIndex = nextStart + AddressStepSize;

                return new IPBatch
                {
                    IssuedToClientId = clientId,
                    Start = nextStart,
                    Count = AddressStepSize,
                    Iteration = currentIteration,
                    StartIP = IPAddressEnumerator.GetIndexAsIP(nextStart),
                    EndIP = IPAddressEnumerator.GetIndexAsIP(nextStart + AddressStepSize - 1),
                    IssuedAt = DateTime.UtcNow,
                    Completed = false,
                    CompletedAt = null
                };
            }
            finally
            {
                NextIndexLock.Release();
            }
        }

        private static async Task<IPBatch?> MaybeValidateIPBatch(string clientID, int currentIteration)
        {
            int RandomThreshold = 2; // % chance to validate a batch
            int roll = Random.Shared.Next(0, 100);
            if (roll >= RandomThreshold) return null;

            Expression<Func<IPBatch, bool>> completedBatches = x => x.Completed && x.Validation.Status == ValidationStatus.NotValidated && x.IssuedToClientId != clientID && x.FoundIps != 0 && x.Iteration == currentIteration;

            ulong count = (ulong)await DB.CountAsync(completedBatches);
            if (count == 0) return null;

            int skip = Random.Shared.Next(0, (int)count);

            return await DB.Find<IPBatch>()
                           .Match(completedBatches)
                           .Skip(skip)
                           .Limit(1)
                           .ExecuteFirstAsync();
        }

        public static async Task<ServerResponse<NewIPRange>> GetNewIPRange(ClientRequest<object> clientRequest)
        {
            IterationTracker iteration = await IterationService.GetCurrentIteration();
            int currentIteration = iteration.Iteration;

            ServerResponse<NewIPRange> response = new ServerResponse<NewIPRange>();

            await DB.Update<ClientModel>()
              .Match(x => x.ClientId == clientRequest.ClientId)
              .Modify(x => x.LastActiveAt, DateTime.UtcNow)
              .ExecuteAsync();

            IPBatch? batchToValidate = await MaybeValidateIPBatch(clientRequest.ClientId, currentIteration);

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
                    Iteration = currentIteration,
                    IsValidation = true,
                };

                response.Message = "Validating IP range assigned";
            } else { 
                IPBatch? staleBatch = await GetStaleIPBatch(currentIteration);

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
                        Iteration = currentIteration,
                        IsValidation = false
                    };

                    response.Message = "Stale IP range assigned.";
                }
                else
                {
                    IPBatch nextBatch = await GetNextIPBatch(clientRequest.ClientId!, currentIteration);

                    await nextBatch.SaveAsync();

                    response.Data = new NewIPRange()
                    {
                        IPBatchID = nextBatch.ID,
                        Count = nextBatch.Count,
                        Start = nextBatch.Start,
                        Iteration = currentIteration,
                        IsValidation = false
                    };

                    response.Message = "New IP range assigned.";

                    ulong newIndex = nextBatch.Start + nextBatch.Count;

                    if (newIndex >= IPAddressEnumerator.LastIPIndex)
                    {
                        iteration.CompletedAt = DateTime.UtcNow;
                        await iteration.SaveAsync();

                        IterationTracker newIteration = new IterationTracker
                        {
                            Iteration = currentIteration + 1,
                            StartedAt = DateTime.UtcNow,
                            CompletedAt = null
                        };

                        await newIteration.SaveAsync();

                        // Reset local index cache for the new iteration
                        await NextIndexLock.WaitAsync();
                        try
                        {
                            CachedIteration = newIteration.Iteration;
                            NextStartIndex = IPAddressEnumerator.FirstIPIndex;
                        }
                        finally
                        {
                            NextIndexLock.Release();
                        }

                        ProgressEstimatorService.Reset();
                    }
                    else
                    {
                        ProgressEstimatorService.UpdateCurrentIndex(response.Data.Start + response.Data.Count);
                    }
                }
            }

            response.Success = true;
            
            return response;
        }
    }
}

using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints.Client.HostRecords.DTOs;
using Godelian.Server.Endpoints.Web.Search; // enqueue recent host records for web endpoint
using MongoDB.Bson;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Godelian.Models.IPBatchValidation;

namespace Godelian.Server.Endpoints.Client.HostRecords
{
    internal static class HostRecordEndpoints
    {
        public static async Task<ServerResponse<SubmitHostRecordsResponse>> SubmitHostRecords(ClientRequest<SubmitHostRecordsRequest> clientRequest)
        {
            ServerResponse<SubmitHostRecordsResponse> response = new ServerResponse<SubmitHostRecordsResponse>();

            await DB.Update<ClientModel>()
              .Match(x => x.ClientId == clientRequest.ClientId)
              .Modify(x => x.LastActiveAt, DateTime.UtcNow)
              .ExecuteAsync();

            IPBatch? ipBatch = await DB.Find<IPBatch>()
                                      .Match(x => x.ID == clientRequest.Data!.IPBatchID)
                                      .ExecuteFirstAsync();

            if (ipBatch == null)
            {
                response.Success = false;
                response.Message = "Invalid or Completed IP Batch ID";
                return response;
            }

            if (ipBatch.Completed && ipBatch.Validation.Status == ValidationStatus.Validating)
            {
                ipBatch.Validation.Status = ipBatch.FoundIps == clientRequest.Data!.HostRecords.Count
                    ? ValidationStatus.Validated
                    : ValidationStatus.Failed;
                ipBatch.Validation.CompletedAt = DateTime.UtcNow;
                ipBatch.Validation.IssuedToClientId = clientRequest.ClientId;
                ipBatch.Validation.FoundIps = clientRequest.Data.HostRecords.Count;

                Console.WriteLine($"IP Batch {ipBatch.ID} was {ipBatch.Validation.Status.ToString().ToUpper()} ({clientRequest.Data.HostRecords.Count}/{ipBatch.FoundIps}) when checked by {clientRequest.ClientNickname ?? clientRequest.ClientId}");
            }
            else if (!ipBatch.Completed)
            {
                ipBatch.Completed = true;
                ipBatch.CompletedAt = DateTime.UtcNow;
                ipBatch.Validation.IssuedToClientId = clientRequest.ClientId;
                ipBatch.FoundIps = clientRequest.Data!.HostRecords.Count;
            }
            else
            {
                response.Success = false;
                response.Message = "IP Batch already completed and validated.";
                return response;
            }

            // Persist the batch state synchronously so the system reflects completion immediately
            await ipBatch.SaveAsync();

            // Offload saving of host records to a background task to avoid blocking the response
            _ = Task.Run(() => SaveHostRecordsAsync(clientRequest));

            response.Success = true;
            response.Message = "Host records accepted for processing.";
            response.Data = new SubmitHostRecordsResponse();
            return response;            
        }

        private static async Task SaveHostRecordsAsync(ClientRequest<SubmitHostRecordsRequest> clientRequest)
        {
            if (clientRequest.Data == null || clientRequest.Data.HostRecords == null || clientRequest.Data.HostRecords.Count == 0)
            {
                return;
            }

            try
            {
                HostRecordModel[] newRecords = clientRequest.Data!.HostRecords
                    .Select(x => new HostRecordModel
                    {
                        IPIndex = x.IPIndex,
                        IPAddress = x.IPAddress,
                        Iteration = x.Iteration,
                        Hostname = x.Hostname,
                        FoundByClientId = clientRequest.ClientId!,
                        HostRequestMethod = x.HostRequestMethod,
                        HeaderKeys = x.HeaderRecords.Select(h => h.Name).ToList(),
                    })
                    .ToArray();

                await DB.SaveAsync(newRecords);

                List<HeaderRecord> newHeaders = new();
                List<Feature> newFeatures = new();

                foreach (HostRecordModel hostRecord in newRecords)
                {
                    HostRecordModelDTO record = clientRequest.Data.HostRecords
                        .First(x => x.IPIndex == hostRecord.IPIndex && x.Iteration == hostRecord.Iteration && x.Hostname == hostRecord.Hostname && x.IPAddress == hostRecord.IPAddress);

                    List<HeaderRecord> headerRecords = record.HeaderRecords.Select(x => new HeaderRecord
                    {
                        HostRecordID = hostRecord.ID,
                        Name = x.Name,
                        Value = x.Value,
                    }).ToList();

                    newHeaders.AddRange(headerRecords);

                    List<Feature> features = record.Features.Select(x => new Feature
                    {
                        HostRecordID = hostRecord.ID,
                        Type = x.Type,
                        Content = x.Content,
                    }).ToList();

                    newFeatures.AddRange(features);

                    RandomHostRecordEndpoint.EnqueueRecentHostRecord(record);
                }

                if (newHeaders.Count > 0)
                    await DB.SaveAsync(newHeaders);

                if (newFeatures.Count > 0)
                    await DB.SaveAsync(newFeatures);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled error while saving host records: {ex}");
            }
        }
    }
}

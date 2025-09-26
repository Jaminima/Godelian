using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints.Client.HostRecords.DTOs;
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

            await ipBatch.SaveAsync();

            foreach (HostRecordModelDTO record in clientRequest.Data.HostRecords)
            {
                try
                {

                    HostRecordModel hostRecordModel = new HostRecordModel
                    {
                        IPIndex = record.IPIndex,
                        IPAddress = record.IPAddress,
                        Iteration = record.Iteration,
                        Hostname = record.Hostname,
                        FoundByClientId = clientRequest.ClientId!,
                        HostRequestMethod = record.HostRequestMethod,
                        HeaderKeys = record.HeaderRecords.Select(x=>x.Name).ToList(),
                    };

                    await hostRecordModel.SaveAsync();

                    List<HeaderRecord> headerRecords = record.HeaderRecords.Select(x => new HeaderRecord
                    {
                        HostRecordID = hostRecordModel.ID,
                        Name = x.Name,
                        Value = x.Value,
                    }).ToList();

                    foreach (HeaderRecord header in headerRecords)
                    {
                        await header.SaveAsync();
                    }

                    List<Feature> features = record.Features.Select(x => new Feature
                    {
                        HostRecordID = hostRecordModel.ID,
                        Type = x.Type,
                        Content = x.Content,
                    }).ToList();

                    foreach (Feature feature in features)
                    {
                        await feature.SaveAsync();
                    }

                    await hostRecordModel.SaveAsync();
                }
                catch (BsonSerializationException ex)
                {
                    //Record was too big so lets just not save it
                }
            }

            return response;            
        }
    }
}

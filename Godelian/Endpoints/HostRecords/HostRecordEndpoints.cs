using Godelian.Endpoints.HostRecords.DTOs;
using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Godelian.Models.IPBatchValidation;

namespace Godelian.Endpoints.HostRecords
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
                                      .Match(x => x.ID == clientRequest.Data.IPBatchID)
                                      .ExecuteFirstAsync();

            if (ipBatch == null)
            {
                response.Success = false;
                response.Message = "Invalid or Completed IP Batch ID";
                return response;
            }

            if (ipBatch.Completed && ipBatch.Validation.Status == ValidationStatus.Validating)
            {
                ipBatch.Validation.Status = ipBatch.FoundIps == clientRequest.Data.HostRecords.Count
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
                ipBatch.FoundIps = clientRequest.Data.HostRecords.Count;
            }
            else
            {
                response.Success = false;
                response.Message = "IP Batch already completed and validated.";
                return response;
            }

            await ipBatch.SaveAsync();

            foreach (HostRecordModel record in clientRequest.Data.HostRecords)
            {
                await record.SaveAsync();
            }

            return response;            
        }
    }
}

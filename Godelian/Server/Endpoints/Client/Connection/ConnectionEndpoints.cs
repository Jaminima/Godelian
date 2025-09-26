using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints.Client.Connection.DTOs;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Client.Connection
{
    internal static class ConnectionEndpoints
    {
        public static async Task<ServerResponse<ClientConnectsResponse>> ClientConnects(ClientRequest<object> clientRequest)
        {
            ServerResponse<ClientConnectsResponse> response = new ServerResponse<ClientConnectsResponse>() {
                Data = new ClientConnectsResponse() { WelcomeMessage = "Welcome to Godelian!" }
            };

            if (!string.IsNullOrWhiteSpace(clientRequest.ClientId) || !string.IsNullOrWhiteSpace(clientRequest.ClientNickname))
            {
                ClientModel? existingClient = clientRequest.ClientNickname != null ?
                    clientRequest.TaskId != null ? await DB.Find<ClientModel>().Match(c => c.Nickname == clientRequest.ClientNickname && c.TaskId == clientRequest.TaskId).ExecuteFirstAsync() :
                    await DB.Find<ClientModel>().Match(c => c.Nickname == clientRequest.ClientNickname).ExecuteFirstAsync() :
                await DB.Find<ClientModel>().Match(c => c.ClientId == clientRequest.ClientId).ExecuteFirstAsync();

                if (existingClient != null)
                {
                    response.Data.AssignedClientId = existingClient.ClientId;
                    response.Message = $"Welcome back {existingClient.Nickname ?? existingClient.ClientId}{(existingClient.TaskId != null ? $"#{existingClient.TaskId}" : "")}!";
                    response.Success = true;

                    existingClient.IsConnected = true;
                    existingClient.LastActiveAt = DateTime.UtcNow;
                    await existingClient.SaveAsync();

                    Console.WriteLine($"Client Reconnected: {existingClient.Nickname ?? existingClient.ClientId}{(existingClient.TaskId != null ? $"#{existingClient.TaskId}" : "")}");

                    return response;
                }
            }

            response.Data.AssignedClientId = Rand.RandomString();
            response.Message = $"New client {clientRequest.ClientNickname ?? response.Data.AssignedClientId}{(clientRequest.TaskId != null ? $"#{clientRequest.TaskId}" : "")}! connected.";
            response.Success = true;

            ClientModel newClient = new ClientModel
            {
                ClientId = response.Data.AssignedClientId,
                Nickname = clientRequest.ClientNickname,
                TaskId = clientRequest.TaskId,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                IsConnected = true
            };

            await newClient.SaveAsync();

            Console.WriteLine($"New Client Connected: {newClient.Nickname ?? newClient.ClientId}{(clientRequest.TaskId != null ? $"#{clientRequest.TaskId}" : "")}");

            return response;
        }

    }
}

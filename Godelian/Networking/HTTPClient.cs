using Godelian.Networking.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Networking
{
    internal class HTTPClient
    {
        public string IP { get; set; }
        public int Port { get; set; }

        public HTTPClient(string ip = "127.0.0.1", int port = 9000)
        {
            IP = ip;
            Port = port;
        }

        public async Task<ServerResponse<TO>> SendRequest<TI,TO>(ClientRequest<TI> clientRequest) where TO : class where TI : class
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri($"http://{IP}:{Port}/");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "");

                string body = System.Text.Json.JsonSerializer.Serialize(clientRequest);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                ServerResponse<TO> serverResponse = System.Text.Json.JsonSerializer.Deserialize<ServerResponse<TO>>(responseBody)!;

                return serverResponse;
            }
        }
    }
}

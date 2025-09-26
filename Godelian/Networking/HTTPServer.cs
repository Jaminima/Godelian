using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Text.Json;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints;

namespace Godelian.Networking
{
    internal class HTTPServer
    {
        public int Port { get; set; }
        public HTTPServer(int port = 9000)
        {
            Port = port;
        }

        public async Task Start()
        {
            Console.WriteLine($"Starting TCP server on port {Port}...");

            HttpListener listener = new HttpListener();

#if DEBUG
            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
#else
            listener.Prefixes.Add($"http://*:{Port}/");
#endif

            listener.Start();

            Console.WriteLine($"Server started. Listening on port {Port}...");

            listener.BeginGetContext(new AsyncCallback(HandleContextCallback), listener);
        }

        private void HandleContextCallback(IAsyncResult ar)
        {
            HttpListener listener = (HttpListener)ar.AsyncState!;
            HttpListenerContext context = listener!.EndGetContext(ar);

            // Call the async handler, but do not await (fire and forget)
            _ = HandleContext(context);

            // Continue listening for the next request
            listener.BeginGetContext(new AsyncCallback(HandleContextCallback), listener);
        }

        public async Task HandleContext(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405; // Method Not Allowed
                response.OutputStream.Close();
                return;
            }

            StreamReader streamReader = new StreamReader(request.InputStream);
            string requestBody = await streamReader.ReadToEndAsync();

            ClientRequest<object>? clientRequest = null;

            try
            {
                clientRequest = JsonSerializer.Deserialize<ClientRequest<object>>(requestBody);
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize request body: {ex.Message}");

                response.StatusCode = 400; // Bad Request
                response.OutputStream.Close();
                return;
            }

            //Console.WriteLine($"Received {clientRequest.RequestType.ToString() ?? "Unknown"} request from {clientRequest.ClientNickname ?? clientRequest.ClientId ?? request.RemoteEndPoint.Address.ToString()}");

            object responseObject = null;

            try
            {
                responseObject = await EndpointRouter.RouteRequest(clientRequest!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.ToString()}");

                response.StatusCode = 500; // Internal Server Error
                response.OutputStream.Close();
                return;
            }

            response.StatusCode = 200;

            string jsonResponse = JsonSerializer.Serialize(responseObject);

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(jsonResponse);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "application/json";
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}

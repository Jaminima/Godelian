using Godelian.Client;
using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking;
using Godelian.Networking.DTOs;
using Godelian.Server;
using MongoDB.Driver;
using MongoDB.Entities;
using System.Net.NetworkInformation;

namespace GodelianAPI
{
    internal class Program
    {
        static HTTPServer httpServer;
        static ClientHandler clientHandler;

        public static void Main(string[] args)
        {
            if (Config.IsServer)
            {
                for (int i = 0; i < 10; i++)
                {
                    Console.WriteLine($"Connecting To DB {i}...");
                    try
                    {
                        // If credentials are provided, use a connection string with authSource=admin
                        if (!string.IsNullOrWhiteSpace(Config.MongoUsername) && !string.IsNullOrWhiteSpace(Config.MongoPassword))
                        {
                            MongoClientSettings settings = new MongoClientSettings
                            {
                                Server = new MongoServerAddress(Config.MongoIP, Config.MongoPort),
                                Credential = MongoCredential.CreateCredential(
                                    "admin",
                                    Config.MongoUsername,
                                    Config.MongoPassword
                                ),
                                RetryWrites = true
                            };

                            DB.InitAsync("godelian-v2", settings).Wait();
                        }
                        else
                        {
                            DB.InitAsync("godelian-v2", Config.MongoIP, Config.MongoPort).Wait();
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to connect to DB: {ex.Message}");
                        Thread.Sleep(5000);
                        continue;
                    }
                }

                IterationTracker iteration = IterationService.GetCurrentIteration().Result;
                int currentIteration = iteration.Iteration;

                IPBatch? latestBatch = DB.Find<IPBatch>()
                        .Match(x=>x.Iteration == currentIteration)
                        .Sort(x => x.Start, Order.Descending)
                        .ExecuteFirstAsync().Result;

                ProgressEstimatorService.Init(latestBatch != null ? (ulong)(latestBatch.Start + latestBatch.Count) : IPAddressEnumerator.FirstIPIndex);

                httpServer = new HTTPServer(9000);
                _ = httpServer.Start();
            }
            else
            {
                Console.WriteLine($"Starting Client...");

                clientHandler = new ClientHandler();
                _ = clientHandler.Start();
            }

            while (true) {
                if (Config.IsServer)
                {
                    TimeSpan remaining = ProgressEstimatorService.EstimateTimeRemaining();

                    string remainingText = remaining.TotalDays >= 1
                        ? remaining.ToString(@"d\.hh\:mm\:ss")
                        : remaining.ToString(@"hh\:mm\:ss");

                    Console.WriteLine($"Progress: {ProgressEstimatorService.GetPercentageProgress():0.000}% | Est: {remainingText} | Current IP: {IPAddressEnumerator.GetIndexAsIP(ProgressEstimatorService.CurrentIndex)}");
                }

                Thread.Sleep(20000);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Helpers
{
    internal static class Config
    {
        public static bool IsServer => Environment.GetEnvironmentVariable("GODELIAN_IS_SERVER") == "TRUE";
        public static string MongoIP => Environment.GetEnvironmentVariable("MONGO_IP") ?? "100.73.169.56";
        public static int MongoPort
        {
            get
            {
                string mongoPort = Environment.GetEnvironmentVariable("MONGO_PORT") ?? "27017";
                return int.TryParse(mongoPort, out int port) ? port : 27017;
            }
        }
        public static string? MongoUsername => Environment.GetEnvironmentVariable("MONGO_USERNAME") ?? "root";
        public static string? MongoPassword => Environment.GetEnvironmentVariable("MONGO_PASSWORD") ?? "changeme";
        public static string GodelianServerIP => Environment.GetEnvironmentVariable("GODELIAN_SERVER_IP") ?? "100.73.169.56";
        public static int GodelianServerPort
        {
            get
            {
                string serverPort = Environment.GetEnvironmentVariable("GODELIAN_SERVER_PORT") ?? "9000";
                return int.TryParse(serverPort, out int port) ? port : 9000;
            }
        }
        public static string? GodelianNickname => Environment.GetEnvironmentVariable("GODELIAN_NICKNAME") ?? null;
        public static string? TaskSlot => Environment.GetEnvironmentVariable("GODELIAN_TASK_SLOT") ?? null;

        public static int MaxConcurrentFetches
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("GODELIAN_MAX_CONCURRENT_FETCHES") ?? "64";
                return int.TryParse(env, out int value) && value > 0 ? value : 64;
            }
        }
    }
}

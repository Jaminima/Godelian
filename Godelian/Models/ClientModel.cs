using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Models
{
    internal class ClientModel : Entity
    {
        static ClientModel()
        {
            // Useful single-field indexes for common queries
            DB.Index<ClientModel>()
              .Key(x => x.ClientId, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<ClientModel>()
              .Key(x => x.TaskId, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();
        }

        public required string ClientId { get; set; }
        public string? Nickname { get; set; }
        public string? TaskId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
        public bool IsConnected { get; set; } = true;
    }
}

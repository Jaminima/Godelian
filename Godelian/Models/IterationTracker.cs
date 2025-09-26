using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Models
{
    internal class IterationTracker : Entity
    {
        static IterationTracker()
        {
            // Indexes to speed up iteration lookups and ordering
            DB.Index<IterationTracker>()
              .Key(x => x.Iteration, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();
        }

        public int Iteration { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; } = null;

    }
}

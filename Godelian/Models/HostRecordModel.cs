using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Models
{
    internal class HostRecordModel : Entity
    {
        public ulong IPIndex { get; set; }
        public required string IPAddress { get; set; }
        public required string Hostname { get; set; }
        public required string FoundByClientId { get; set; }
        public DateTime FoundAt { get; set; } = DateTime.UtcNow;

        // Info from the actual page
        public List<Feature> Features { get; set; } = new();
    }

    internal class Feature
    {
        public required string Content { get; set; }
        public FeatureType Type { get; set; }
    }

    internal enum FeatureType
    {
        Title,
        Heading,
        Text,
        Script,
        Image,
        Link
    }
}


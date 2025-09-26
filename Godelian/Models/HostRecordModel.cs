using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Models
{
    internal class HostRecordModelDTO
    {
        public string? ID { get; set; }
        public ulong IPIndex { get; set; }
        public int Iteration { get; set; }
        public required string IPAddress { get; set; }
        public required string Hostname { get; set; }
        public required string FoundByClientId { get; set; }
        public DateTime FoundAt { get; set; } = DateTime.UtcNow;
        public bool FeaturesElaborated { get; set; } = false;
        public required HostRequestMethod HostRequestMethod { get; set; }

        //Virtual
        public List<HeaderRecordDTO> HeaderRecords { get; set; } = new();
        public List<FeatureDTO> Features { get; set; } = new();
    }

    internal class HeaderRecordDTO
    {
        public string? ID { get; set; }
        public string? HostRecordID { get; set; }
        public required string Name { get; set; }
        public required string Value { get; set; }
    }

    internal class FeatureDTO
    {
        public string? ID { get; set; }
        // Content used for searchable/text features (title, text, links, etc.)
        public string? Content { get; set; }
        // Base64Content used only for binary/image payloads so we avoid indexing large blobs
        public string? Base64Content { get; set; }
        public FeatureType Type { get; set; }
        public bool Elaborated { get; set; } = false;

        //Virtual
        public List<FeatureDTO> SubFeatures { get; set; } = new();
        public FeatureDTO? ParentFeature { get; set; } = null;
        public HostRecordModelDTO? HostRecord { get; set; } = null;
    }

    internal class HostRecordModel : Entity
    {
        static HostRecordModel()
        {
            // Separate single-field indexes instead of a compound index
            DB.Index<HostRecordModel>()
              .Key(x => x.IPIndex, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<HostRecordModel>()
              .Key(x => x.Iteration, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<HostRecordModel>()
              .Key(x => x.Hostname, KeyType.Text)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<HostRecordModel>()
              .Key(x => x.FoundAt, KeyType.Descending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<HostRecordModel>()
              .Key(x => x.FeaturesElaborated, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<HostRecordModel>()
              .Key(x => x.HeaderKeys, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();
        }

        public ulong IPIndex { get; set; }
        public int Iteration { get; set; }
        public required string IPAddress { get; set; }
        public required string Hostname { get; set; }
        public required string FoundByClientId { get; set; }
        public DateTime FoundAt { get; set; } = DateTime.UtcNow;
        public bool FeaturesElaborated { get; set; } = false;
        public required HostRequestMethod HostRequestMethod { get; set; }
        public required List<string> HeaderKeys { get; set; } = new();
    }

    internal class HeaderRecord : Entity
    {
        static HeaderRecord()
        {
            DB.Index<HeaderRecord>()
              .Key(x => x.HostRecordID, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<HeaderRecord>()
                .Key(x => x.Name, KeyType.Ascending)
                .Option(o => o.Unique = false)
                .CreateAsync().Wait();

            DB.Index<HeaderRecord>()
                .Key(x => x.Value, KeyType.Ascending)
                .Option(o => o.Unique = false)
                .CreateAsync().Wait();
        }
        public required string HostRecordID { get; set; }
        public required string Name { get; set; }
        public required string Value { get; set; }
    }

    internal class Feature : Entity
    {
        static Feature()
        {
            // Separate single-field indexes instead of a compound index
            DB.Index<Feature>()
              .Key(x => x.Type, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            // Keep text index on Content (searchable text). Do NOT index Base64Content to avoid large indexed blobs.
            DB.Index<Feature>()
              .Key(x => x.Content, KeyType.Text)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<Feature>()
              .Key(x => x.Elaborated, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<Feature>()
              .Key(x => x.HostRecordID, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();

            DB.Index<Feature>()
              .Key(x => x.ParentFeatureID, KeyType.Ascending)
              .Option(o => o.Unique = false)
              .CreateAsync().Wait();
        }

        // Content is nullable and used for searchable/text features (title, text, image URLs, links, etc.)
        public string? Content { get; set; }
        // Base64Content is nullable and used only for binary image payloads; intentionally not indexed.
        public string? Base64Content { get; set; }
        public FeatureType Type { get; set; }
        public required string HostRecordID { get; set; }
        public string? ParentFeatureID { get; set; }
        public bool Elaborated { get; set; } = false;
    }

    internal enum FeatureType
    {
        Title=0,
        //Heading=1,
        Text=2,
        Script=3,
        Image=4,
        Link=5,
        Base64=6,
    }

    internal enum HostRequestMethod
    {
        HTTP=0,
        HTTPS=1,
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Web.Statistics.DTOs
{
    // Request DTOs
    internal class HeaderStatsRequest
    {
        public int TopN { get; set; } = 20;
    }

    internal class HeaderValueStatsRequest
    {
        public required string HeaderName { get; set; }
        public int TopN { get; set; } = 20;
    }

    // Response DTOs
    internal class HeaderNameCount
    {
        public required string Name { get; set; }
        public required long Count { get; set; }
    }

    internal class HeaderValueCount
    {
        public string? Value { get; set; }
        public required long Count { get; set; }
    }

    internal class HeaderStatsResponse
    {
        public HeaderNameCount[] TopHeaderNames { get; set; } = Array.Empty<HeaderNameCount>();
    }

    internal class HeaderValueStatsResponse
    {
        public HeaderValueCount[] TopValues { get; set; } = Array.Empty<HeaderValueCount>();
    }
}

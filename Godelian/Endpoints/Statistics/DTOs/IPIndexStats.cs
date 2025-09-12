using Godelian.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Endpoints.Statistics.DTOs
{
    internal class IPIndexStats
    {
        public required HostRecordModel[] hostRecords { get; set; }
    }
}

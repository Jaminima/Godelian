using Godelian.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Client.HostRecords.DTOs
{
    internal class SubmitHostRecordsRequest
    {
        public required string IPBatchID { get; set; }
        public required List<HostRecordModelDTO> HostRecords { get; set; }
    }
}

using Godelian.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Client.IPAddressing.DTOs
{
    internal class NewIPRange
    {
        public required string IPBatchID { get; set; }
        public required ulong Start { get; set; }
        public required ulong Count { get; set; }
        public required int Iteration { get; set; }  
        public required bool IsValidation { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Endpoints.IPAddreessing.DTOs
{
    internal class NewIPRange
    {
        public string IPBatchID { get; set; }
        public ulong Start { get; set; }
        public ulong Count { get; set; }
        public bool IsValidation { get; set; }
    }
}

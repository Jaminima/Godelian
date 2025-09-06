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
        public uint Start { get; set; }
        public int Count { get; set; }
    }
}

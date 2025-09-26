using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Networking.DTOs
{
    internal class ClientRequest<T> where T : class
    {
        public ClientRequestType RequestType { get; set; }
        public string? ClientId { get; set; }
        public string? ClientNickname { get; set; }
        public string? TaskId { get; set; }
        public T? Data { get; set; }
    }
}

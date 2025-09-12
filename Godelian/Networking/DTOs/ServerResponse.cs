using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Networking.DTOs
{
    internal class ServerResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public void ThrowIfFailed()
        {
            if (!Success)
                throw new Exception(Message ?? "Server Error Occured");
        }
        public object? Data { get; set; } = null;
    }

    internal class ServerResponse<T> : ServerResponse
    {
        public new T? Data { get; set; } = default(T);
    }
}

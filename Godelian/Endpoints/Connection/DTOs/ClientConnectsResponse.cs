using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Endpoints.Connection.DTOs
{
    internal class ClientConnectsResponse
    {
        public string? AssignedClientId { get; set; }
        public string? WelcomeMessage { get; set; }
    }
}

using Godelian.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Web.Statistics.DTOs
{
    internal class RecentlyActiveClients
    {
        public required ClientModel[] Clients { get; set; }
    }
}

using Godelian.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Web.Search.DTOs
{
    internal class SearchResults
    {
        public required FeatureDTO[] Features { get; set; }
    }
}

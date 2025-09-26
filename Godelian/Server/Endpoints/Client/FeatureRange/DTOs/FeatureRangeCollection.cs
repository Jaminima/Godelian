using Godelian.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Client.FeatureRange.DTOs
{
    internal class FeatureRangeCollection
    {
        public required FeatureDTO[] featureRecords { get; set; }
    }
}

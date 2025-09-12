using Godelian.Helpers;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Models
{
    internal class IPBatch : Entity
    {
        public required string IssuedToClientId { get; set; }
        public ulong Start { get; set; }
        public ulong Count { get; set; }
        public required string StartIP { get; set; }
        public required string EndIP { get; set; }
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public bool Completed { get; set; } = false;
        public DateTime? CompletedAt { get; set; } = null;
        public int? FoundIps { get; set; } = null;
        public IPBatchValidation Validation { get; set; } = new IPBatchValidation();
    }

    internal class IPBatchValidation
    {
        public ValidationStatus Status { get; set; } = ValidationStatus.NotValidated;
        public string? IssuedToClientId { get; set; }
        public DateTime? IssuedAt { get; set; } = null;
        public DateTime? CompletedAt { get; set; } = null;
        public int? FoundIps { get; set; } = null;
    }

    internal enum ValidationStatus
    {
        NotValidated,
        Validating,
        Validated,
        Failed
    }
}

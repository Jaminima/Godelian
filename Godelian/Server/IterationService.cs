using Godelian.Models;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server
{
    internal static class IterationService
    {
        public static async Task<IterationTracker> GetCurrentIteration()
        {
            IterationTracker? latestIteration = await DB.Find<IterationTracker>()
                                                          .Sort(x => x.Iteration, Order.Descending)
                                                          .ExecuteFirstAsync();

            if (latestIteration == null || latestIteration.CompletedAt != null)
            {
                IterationTracker newIteration = new IterationTracker
                {
                    Iteration = (latestIteration?.Iteration ?? 0) + 1,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = null
                };
                await newIteration.SaveAsync();

                return newIteration;
            }

            return latestIteration;
        }

    }
}

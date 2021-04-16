using System;
using System.Collections.Generic;
using BookingApp.Services;
using Microsoft.Extensions.Options;

namespace BookingApp.Data
{
    public class TeamService
    {
        private readonly Dictionary<string, Team> teams = new Dictionary<string, Team>();

        private void InitializeTeamsFromConfiguration(List<TeamOption> teamConfig)
        {
            teamConfig.ForEach(e=>
                teams.Add(e.Id, new()
                {
                    Duration = e.Duration,
                    Id = e.Id,
                    Name = e.Name,
                    Limits = ParseLimits(e.Limits)
                })
                );
        }

        private static Dictionary<TeamLimit, int> ParseLimits(Dictionary<string, int> limits)
        {
            var res = new Dictionary<TeamLimit, int>();
            if (limits == null)
                return res;
            
            foreach (var keyValuePair in limits)
            {
                if (Enum.TryParse(keyValuePair.Key, out TeamLimit limitType))
                {
                    res.Add(limitType, keyValuePair.Value);
                }
            }
            return res;
        }

        public TeamService(IOptions<List<TeamOption>> options)
        {
            InitializeTeamsFromConfiguration(options.Value);
        }

        public Team GetTeam(string id)
        {
            return teams[id];
        }
    }
}
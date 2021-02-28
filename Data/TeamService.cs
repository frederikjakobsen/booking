using System;
using System.Collections.Generic;

namespace BookingApp.Data
{
    public class TeamService
    {
        private readonly Dictionary<string, Team> teams = new Dictionary<string, Team>();

        public static readonly Team Open = new Team
        {
            Duration = TimeSpan.FromHours(1),
            Id = "open",
            Name = "Open",
            Limits = new Dictionary<TeamLimit, int>()
        };

        public TeamService()
        {
            teams.Add("open", Open);
            teams.Add("1", new Team
            {
                Duration = TimeSpan.FromMinutes(90),
                Id = "1",
                Name = "Beginner",
                Limits =
                    new Dictionary<TeamLimit, int>
                    {
                        { TeamLimit.Size, 1},
                        { TeamLimit.ActiveBookings, 2}
                    }
            });
            teams.Add("2", new Team { Duration = TimeSpan.FromMinutes(90), Id = "2", Name = "Intermediate",
                Limits =
                    new Dictionary<TeamLimit, int>
                    {
                        { TeamLimit.Size, 2},
                        { TeamLimit.ActiveBookings, 2}
                    }
            });
            teams.Add("3", new Team { Duration = TimeSpan.FromMinutes(90), Id = "3", Name = "Elite",
                Limits =
                    new Dictionary<TeamLimit, int>
                    {
                        { TeamLimit.Size, 3},
                        { TeamLimit.ActiveBookings, 2}
                    }
            });
        }

        public Team GetTeam(string id)
        {
            return teams[id];
        }
    }
}
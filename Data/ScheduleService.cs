using System;
using System.Collections.Generic;

namespace BookingApp.Data
{
    public class ScheduleService
    {
        public readonly WeeklyTeamSchedule ActiveSchedule =
            new WeeklyTeamSchedule
            {
                ScheduledTeams = new List<WeeklyScheduledTeam>
                {
                    new WeeklyScheduledTeam
                    {
                        Day = DayOfWeek.Monday, TimeOfDay = TimeSpan.FromHours(16) + TimeSpan.FromMinutes(30),
                        TeamId = "1"
                    },
                    new WeeklyScheduledTeam
                        {Day = DayOfWeek.Monday, TimeOfDay = TimeSpan.FromHours(18), TeamId = "2"},
                    new WeeklyScheduledTeam
                        {Day = DayOfWeek.Saturday, TimeOfDay = TimeSpan.FromHours(18), TeamId = "3"},
                    new WeeklyScheduledTeam
                        {Day = DayOfWeek.Saturday, TimeOfDay = TimeSpan.FromHours(17), TeamId = "1"},
                }
            };
    }
}
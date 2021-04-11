using System;
using System.Collections.Generic;

namespace BookingApp.Data
{
    public class ScheduleService
    {
        public readonly WeeklyTeamSchedule ActiveSchedule =
            new()
            {
                ScheduledTeams = new List<WeeklyScheduledTeam>
                {
                    new()
                    {
                        Day = DayOfWeek.Monday, TimeOfDay = TimeSpan.FromHours(16) + TimeSpan.FromMinutes(30),
                        TeamId = "1"
                    },
                    new() {Day = DayOfWeek.Monday, TimeOfDay = TimeSpan.FromHours(18), TeamId = "2"},
                    new() {Day = DayOfWeek.Saturday, TimeOfDay = TimeSpan.FromHours(18), TeamId = "3"},
                    new() {Day = DayOfWeek.Saturday, TimeOfDay = TimeSpan.FromHours(17), TeamId = "1"},
                }
            };
    }
}
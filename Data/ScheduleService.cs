using System;
using System.Collections.Generic;
using System.Linq;
using BookingApp.Services;
using Microsoft.Extensions.Options;

namespace BookingApp.Data
{
    public class ScheduleService
    {
        public WeeklyTeamSchedule ActiveSchedule { get; private set; }

        public ScheduleService(IOptions<List<WeeklyScheduledTeamOption>> options)
        {
            var res = options.Value.Select(e=>
            new WeeklyScheduledTeam
            {
                Day = e.Day,
                TeamId = e.TeamId,
                TimeOfDay =  e.TimeOfDay
            }).ToList();
            ActiveSchedule = new WeeklyTeamSchedule
            {
                ScheduledTeams = res
            };
        }
    }
}
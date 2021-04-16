using System;
using System.Collections.Generic;
using System.Linq;
using BookingApp.Services;
using Microsoft.Extensions.Options;

namespace BookingApp.Data
{
    public class SpaceSchedule
    {
        private readonly TeamSessionGenerator _sessionGenerator;
        private readonly TeamService _teamService;
        private readonly int _spaceSize;

        public SpaceSchedule(ScheduleService scheduleService, TeamService teamService, IOptions<SpaceOptions> options)
        {
            _sessionGenerator = new TeamSessionGenerator(scheduleService.ActiveSchedule.ScheduledTeams);
            _teamService = teamService;
            _spaceSize = options.Value.Size;
        }

        private IEnumerable<TeamSession> GetTeamSessionsActiveDuring(TimeSlot timeSlot)
        {
            // a team can at most last for one week, so we start by finding the teams starting at start time minus one week
            var maxTeamDuration = TimeSpan.FromDays(7);
            var possiblyOverlappingTeams = _sessionGenerator.GetTeamSlots(timeSlot.StartTime - maxTeamDuration, maxTeamDuration + timeSlot.Duration);
            return possiblyOverlappingTeams.Where(e => ConvertToTimeSlot(e).Overlaps(timeSlot));
        }

        public int GetFreeSpace(TimeSlot timeSlot)
        {
            var sessions = GetTeamSessionsActiveDuring(timeSlot);
            return _spaceSize - sessions.Sum(e => _teamService.GetTeam(e.TeamId).Limits.GetValueOrDefault(TeamLimit.Size));
        }

        private TimeSlot ConvertToTimeSlot(TeamSession session)
        {
            return new TimeSlot { Duration = _teamService.GetTeam(session.TeamId).Duration, StartTime = session.StartTime };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;

namespace BookingApp.Data
{
    public class SpaceSchedule
    {
        private readonly TeamSessionGenerator sessionGenerator;
        private readonly TeamService teamService;
        private readonly int spaceSize = 24;

        public SpaceSchedule(ScheduleService scheduleService, TeamService teamService)
        {
            this.sessionGenerator = new TeamSessionGenerator(scheduleService.ActiveSchedule.ScheduledTeams);
            this.teamService = teamService;
        }

        private IEnumerable<TeamSession> GetTeamSessionsActiveDuring(TimeSlot timeSlot)
        {
            // a team can at most last for one week, so we start by finding the teams starting at start time minus one week
            var maxTeamDuration = TimeSpan.FromDays(7);
            var possiblyOverlappingTeams = sessionGenerator.GetTeamSlots(timeSlot.StartTime - maxTeamDuration, maxTeamDuration + timeSlot.Duration);
            return possiblyOverlappingTeams.Where(e => ConvertToTimeSlot(e).Overlaps(timeSlot));
        }

        public int GetFreeSpace(TimeSlot timeSlot)
        {
            var sessions = GetTeamSessionsActiveDuring(timeSlot);
            return spaceSize - sessions.Sum(e => teamService.GetTeam(e.TeamId).Limits.GetValueOrDefault(TeamLimit.Size));
        }

        private TimeSlot ConvertToTimeSlot(TeamSession session)
        {
            return new TimeSlot { Duration = teamService.GetTeam(session.TeamId).Duration, StartTime = session.StartTime };
        }
    }
}
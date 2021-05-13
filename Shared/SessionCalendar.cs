using BookingApp.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BookingApp.Shared
{
    public class ResolvedTeamSession
    {
        public TimeSlot TimeSlot { get; set; }
        public Team team { get; set; }
        
        public int SizeLimit { get; set; }

        public TeamSession ToSession() =>
            new TeamSession
            {
                StartTime = TimeSlot.StartTime,
                TeamId = team.Id
            };
    }

    public class SessionResolver
    {
        private readonly SpaceSchedule _spaceSchedule;
        private readonly TeamService _teamService;

        public SessionResolver(SpaceSchedule spaceSchedule, TeamService teamService)
        {
            _spaceSchedule = spaceSchedule;
            _teamService = teamService;
        }
        
        private int GetSizeLimit(Team team, TimeSlot startTime)
        {
            if(team.Limits.TryGetValue(TeamLimit.Size, out var sizeLimit))
            {
                return sizeLimit;
            }
            return _spaceSchedule.GetFreeSpace(startTime);
        }

        public IEnumerable<ResolvedTeamSession> ResolveSessions(IEnumerable<TeamSession> sessions)
        {
            return sessions.Select(e =>
            {
                 var team = _teamService.GetTeam(e.TeamId);
                 var slot = new TimeSlot {StartTime = e.StartTime, Duration = team.Duration};
                 return new ResolvedTeamSession
                 {
                     team = team,
                     TimeSlot = slot,
                     SizeLimit = GetSizeLimit(team, slot)
                 };
             });
        }
    }

    public class TeamSessionCalendar:ISessionCalendar
    {
        private readonly ScheduleService _scheduleService;
        private readonly TeamSessionGenerator _sessionGenerator;

        public TeamSessionCalendar(ScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
            _sessionGenerator = new TeamSessionGenerator(scheduleService.ActiveSchedule.ScheduledTeams);
        }

        public IEnumerable<TeamSession> DailySessions(DateTime from)
        {
            var teamsessiondate = from;
            var sessions = _sessionGenerator.GetTeamSlots(from, TimeSpan.FromDays(1)).ToList();

            while (!sessions.Any())
            {
                teamsessiondate += TimeSpan.FromDays(1);
                sessions = _sessionGenerator.GetTeamSlots(teamsessiondate, TimeSpan.FromDays(1)).ToList();
            }
            return sessions.OrderBy(e => e.StartTime);
        }
        
        public DateTime NextDayWithSessions(DateTime date)
        {
            var teamsessiondate = date;

            while (true)
            {
                teamsessiondate += TimeSpan.FromDays(1);
                if (_sessionGenerator.GetTeamSlots(teamsessiondate, TimeSpan.FromDays(1)).Any())
                    return teamsessiondate;
            }
        }

        public DateTime PreviousDayWithSessions(DateTime date)
        {
            var teamsessiondate = date;

            while (true)
            {
                teamsessiondate -= TimeSpan.FromDays(1);
                if (_sessionGenerator.GetTeamSlots(teamsessiondate, TimeSpan.FromDays(1)).Any())
                    return teamsessiondate;
            }
        }


    }

    public interface ISessionCalendar
    {
        public IEnumerable<TeamSession> DailySessions(DateTime from);
        public DateTime NextDayWithSessions(DateTime date);
        public DateTime PreviousDayWithSessions(DateTime date);

    }

    public class OpenSessionCalendar:ISessionCalendar
    {
        private readonly Team _openTeam;

        private readonly TimeSpan _firstSessionOfDay = TimeSpan.FromHours(6);
        private readonly TimeSpan _lastSessionOfDayEnd = TimeSpan.FromHours(23);

        public OpenSessionCalendar(Team openTeam)
        {
            this._openTeam = openTeam;
        }


        public IEnumerable<TeamSession> DailySessions(DateTime from)
        {
            var res = new List<TeamSession>();
            var currentTime = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0, from.Kind); // hourly rounded time
            var latestPossibleStart = from + TimeSpan.FromDays(1) - _openTeam.Duration;
            while (currentTime <= latestPossibleStart)
            {
                if (currentTime.TimeOfDay >= _firstSessionOfDay && currentTime.TimeOfDay + _openTeam.Duration <= _lastSessionOfDayEnd)
                {
                    res.Add(new TeamSession { StartTime = currentTime, TeamId=_openTeam.Id });
                }
                currentTime = currentTime + _openTeam.Duration;
            }
            return res;
        }

        public DateTime NextDayWithSessions(DateTime date)
        {
            return (date + TimeSpan.FromDays(1));
        }

        public DateTime PreviousDayWithSessions(DateTime date)
        {
            return (date - TimeSpan.FromDays(1));
        }
    }
}

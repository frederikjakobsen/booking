using BookingApp.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace BookingApp.Shared
{
    public class SessionNavigator
    {
        private readonly ISessionCalendar sessionCalendar;
        private readonly string _path;

        public SessionNavigator(ISessionCalendar sessionCalendar, string path)
        {
            this.sessionCalendar = sessionCalendar;
            this._path = path;
        }

        private string UrlForDate(DateTime date)
        {
            return  $"/{_path}/{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        }

        public string GetNextSessionDayUrl(DateTime date)
        {
            return  UrlForDate(sessionCalendar.NextDayWithSessions(date));
        }

        public string GetPreviousSessionDayUrl(DateTime date)
        {
            var previousDayWithSessions = sessionCalendar.PreviousDayWithSessions(date);
            if (previousDayWithSessions < DateTime.Today)
                return null;
            return UrlForDate(previousDayWithSessions);
        }
    }

    public class ResolvedTeamSession
    {
        public TeamSession session { get; set; }
        public Team team { get; set; }
        public int FreeSpace { get; set; }
    }

    public class SessionResolver
    {
        private readonly BookingService bookingService;
        private readonly TeamService teamService;

        public SessionResolver(BookingService bookingService, TeamService teamService)
        {
            this.bookingService = bookingService;
            this.teamService = teamService;
        }

        public IEnumerable<ResolvedTeamSession> ResolveSessions(IEnumerable<TeamSession> sessions)
        {
            return sessions.Select(e =>
             {
                 var team = teamService.GetTeam(e.TeamId);
                 return new ResolvedTeamSession
                 {
                     team = team,
                     session = e,
                     FreeSpace = bookingService.GetFreeSpace(new TimeSlot { StartTime = e.StartTime, Duration = team.Duration })
                 };
             });
        }
    }

    public class TeamSessionCalendar:ISessionCalendar
    {
        private readonly BookingService bookingService;
        public TeamSessionCalendar(BookingService bookingService)
        {
            this.bookingService = bookingService;
        }

        public IEnumerable<TeamSession> DailySessions(DateTime from)
        {
            var teamsessiondate = from;
            var sessions = (bookingService.GetTeamSessions(from, TimeSpan.FromDays(1))).ToList();

            while (!sessions.Any())
            {
                teamsessiondate += TimeSpan.FromDays(1);
                sessions = (bookingService.GetTeamSessions(teamsessiondate, TimeSpan.FromDays(1))).ToList();
            }
            return sessions.OrderBy(e => e.StartTime);
        }
        
        public DateTime NextDayWithSessions(DateTime date)
        {
            var teamsessiondate = date;

            while (true)
            {
                teamsessiondate += TimeSpan.FromDays(1);
                if (bookingService.GetTeamSessions(teamsessiondate, TimeSpan.FromDays(1)).Any())
                    return teamsessiondate;
            }
        }

        public DateTime PreviousDayWithSessions(DateTime date)
        {
            var teamsessiondate = date;

            while (true)
            {
                teamsessiondate -= TimeSpan.FromDays(1);
                if (bookingService.GetTeamSessions(teamsessiondate, TimeSpan.FromDays(1)).Any())
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
        private readonly Team openTeam;

        private readonly TimeSpan FirstSessionOfDay = TimeSpan.FromHours(6);
        private readonly TimeSpan LastSessionOfDayEnd = TimeSpan.FromHours(23);

        public OpenSessionCalendar(Team openTeam)
        {
            this.openTeam = openTeam;
        }


        public IEnumerable<TeamSession> DailySessions(DateTime from)
        {
            var res = new List<TeamSession>();
            var currentTime = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0, from.Kind); // hourly rounded time
            var latestPossibleStart = from + TimeSpan.FromDays(1) - openTeam.Duration;
            while (currentTime <= latestPossibleStart)
            {
                if (currentTime.TimeOfDay >= FirstSessionOfDay && currentTime.TimeOfDay + openTeam.Duration <= LastSessionOfDayEnd)
                {
                    res.Add(new TeamSession { StartTime = currentTime, TeamId=openTeam.Id });
                }
                currentTime = currentTime + openTeam.Duration;
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

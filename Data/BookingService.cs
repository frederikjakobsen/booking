using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookingApp.Data
{

    public class WeeklyScheduledTeam
    {
        public string TeamId { get; set; }
        public DayOfWeek Day { get; set; }
        public TimeSpan TimeOfDay { get; set; }
    }

    public class TeamSession
    {
        public string TeamId { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class WeeklyTeamSchedule
    {
        public IEnumerable<WeeklyScheduledTeam> ScheduledTeams { get; set; }
        public DateTime ValidityStart { get; set; }
        public DateTime ValidityEnd { get; set; }
    }

    public class TeamSessionGenerator
    {
        private readonly IEnumerable<WeeklyScheduledTeam> scheduledTeams;

        public TeamSessionGenerator(IEnumerable<WeeklyScheduledTeam> ScheduledTeams)
        {
            this.scheduledTeams = ScheduledTeams;
        }

        // TODO make DST aware!

        public IEnumerable<TeamSession> GetTeamSlots(DateTime from, TimeSpan duration)
        {
            var currentTime = from;
            var endTime = from + duration;

            var startDay = from.DayOfWeek;
            var numberOfDays = (int)Math.Ceiling(1 + duration.TotalDays);

            var res = new List<TeamSession>();
            var currentDay = startDay;
            for (var day = 0; day < numberOfDays; day++)
            {
                foreach (var scheduledTeam in scheduledTeams)
                {
                    if (scheduledTeam.Day == currentDay)
                    {
                        var startTime = currentTime - currentTime.TimeOfDay + scheduledTeam.TimeOfDay;
                        if (startTime >= from && startTime <= endTime)
                        {
                            res.Add(new TeamSession
                            {
                                TeamId = scheduledTeam.TeamId,
                                StartTime = startTime
                            });
                        }
                    }
                }
                currentDay = (DayOfWeek)(((int)currentDay + 1) % 7);
                currentTime += TimeSpan.FromDays(1);
            }
            return res;
        }
    }

    public class OpenSession
    {
        public DateTime StartTime { get; set; }
        public int Size { get; set; }
    }

    public class OpenSessionGenerator
    {

        private TimeSpan FirstSessionOfDay = TimeSpan.FromHours(6);
        private TimeSpan LastSessionOfDayEnd = TimeSpan.FromHours(23);
        private int OpenSessionSize = 24;

        public IEnumerable<OpenSession> GetHourlyOpenSessions(DateTime from, TimeSpan duration)
        {
            var res = new List<OpenSession>();
            var currentTime = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0, from.Kind); // hourly rounded time
            var latestPossibleStart = from + duration - TimeSpan.FromHours(1);
            while(currentTime <= latestPossibleStart)
            {
                if (currentTime.TimeOfDay >= FirstSessionOfDay && currentTime.TimeOfDay + TimeSpan.FromHours(1) <= LastSessionOfDayEnd )
                {
                    res.Add(new OpenSession { Size = OpenSessionSize, StartTime = currentTime });
                }
                currentTime = currentTime.AddHours(1);
            }
            return res;
        }
    }

    public class TeamBookingTimeSlot
    {
        public TeamSession Session { get; set; }
        public IEnumerable<string> Reservations { get; set; }
    }

    public class HourlyBookingTimeSlot
    {
        public OpenSession Session { get; set; }
        public IEnumerable<string> Reservations { get; set; }
    }

    public class BookingSchedule
    {
        public IEnumerable<HourlyBookingTimeSlot> OpenSlots { get; set; }
        public IEnumerable<TeamBookingTimeSlot> TeamSlots { get; set; }
    }

    public class UserReservation
    {
        public DateTime StartTime { get; set; }
        public string TeamId { get; set; }
    }

    public class BookedTimeSlot
    {
        public List<string> OpenReservations { get; set; }
        public Dictionary<string, List<string>> TeamReservations { get; set; }
    }


    public class BookingStorage
    {
        private readonly Dictionary<string, Dictionary<DateTime, UserReservation>> userReservations = new Dictionary<string, Dictionary<DateTime,UserReservation>>();

        private readonly Dictionary<DateTime, BookedTimeSlot> bookingsWithReservations = new Dictionary<DateTime, BookedTimeSlot>();


        public IEnumerable<UserReservation> GetReservationsFor(string userId)
        {
            return userReservations.GetValueOrDefault(userId, new Dictionary<DateTime, UserReservation>()).Values;
        }

        public IEnumerable<BookedTimeSlot> GetAllReservationsBetween(DateTime from, TimeSpan duration)
        {
            return bookingsWithReservations.Where(kvpair => kvpair.Key >= from && kvpair.Key <= from + duration).Select(kvpair => kvpair.Value);
        }

        public void AddReservation(string userId, UserReservation reservation)
        {
            var reservationsForUser = userReservations.GetValueOrDefault(userId, new Dictionary<DateTime, UserReservation>());
            reservationsForUser.Add(reservation.StartTime, reservation);
            userReservations[userId] = reservationsForUser;

            var currentBookingsAtSameTime = bookingsWithReservations.GetValueOrDefault(reservation.StartTime, new BookedTimeSlot { OpenReservations = new List<string>(), TeamReservations = new Dictionary<string, List<string>>()});
            currentBookingsAtSameTime.TeamReservations.GetValueOrDefault(reservation.TeamId, new List<string>()).Add(userId);
            bookingsWithReservations[reservation.StartTime] = currentBookingsAtSameTime;
        }
    }

    public class BookingService
    {

        private BookingStorage bookingStorage;
        private readonly AuthenticationStateProvider authenticationStateProvider;
        private readonly UserManager<IdentityUser> userManager;

        public BookingService(AuthenticationStateProvider authenticationStateProvider, UserManager<IdentityUser> userManager, BookingStorage bookingStorage)
        {
            Console.WriteLine("New booking service!");
            this.authenticationStateProvider = authenticationStateProvider;
            this.userManager = userManager;
            this.bookingStorage = bookingStorage;
        }


        public void MakeOpenReservation(HourlyBookingTimeSlot timeSlot)
        {

        }

        public async Task MakeTeamReservation(TeamSession session)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            bookingStorage.AddReservation(userId, new UserReservation {StartTime = session.StartTime, TeamId = session.TeamId });
        }

        public async Task<IEnumerable<UserReservation>> GetLoggedOnUserReservations()
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            return bookingStorage.GetReservationsFor(userId);
        }

        private static WeeklyTeamSchedule ActiveSchedule = new WeeklyTeamSchedule
        {
            ValidityStart = new DateTime(2020, 12, 1),
            ValidityEnd = new DateTime(2021, 12, 31),
            ScheduledTeams = new List<WeeklyScheduledTeam> {
                new WeeklyScheduledTeam { Day = DayOfWeek.Monday, TimeOfDay = TimeSpan.FromHours(16) + TimeSpan.FromMinutes(30), TeamId = "1"},
                new WeeklyScheduledTeam { Day = DayOfWeek.Monday, TimeOfDay = TimeSpan.FromHours(18), TeamId = "2"},
                new WeeklyScheduledTeam { Day = DayOfWeek.Saturday, TimeOfDay = TimeSpan.FromHours(18), TeamId = "3"},
            }
        };


        public Task<BookingSchedule> GetBookingSchedule(DateTime from, TimeSpan duration)
        {
            // get all the team slots based on the time interval and active schedule
            var res = new BookingSchedule();
            res.TeamSlots = new TeamSessionGenerator(ActiveSchedule.ScheduledTeams).GetTeamSlots(from, duration).Select(e => new TeamBookingTimeSlot { Session = e, Reservations = Enumerable.Empty<string>() }) ;
            res.OpenSlots = new OpenSessionGenerator().GetHourlyOpenSessions(from, duration).Select(e => new HourlyBookingTimeSlot { Session = e, Reservations = Enumerable.Empty<string>() });

            return Task.FromResult(res);
        }
    }

    public class TeamService
    {
        private readonly Dictionary<string, Team> teams = new Dictionary<string, Team>();

        public TeamService()
        {
            teams.Add("1", new Team { Duration = TimeSpan.FromMinutes(90), Id = "1", Name = "Beginner", Size = 10});
            teams.Add("2", new Team { Duration = TimeSpan.FromMinutes(90), Id = "2", Name = "Intermediate", Size = 10});
            teams.Add("3", new Team { Duration = TimeSpan.FromMinutes(90), Id = "3", Name = "Elite", Size = 10});
        }

        public Team GetTeam(string id)
        {
            return teams[id];
        }
    }

    public class Team
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TimeSpan Duration { get; set; }
        public int Size { get; set; }
    }
}

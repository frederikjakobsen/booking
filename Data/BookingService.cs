using BookingApp.Areas.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            while (currentTime <= latestPossibleStart)
            {
                if (currentTime.TimeOfDay >= FirstSessionOfDay && currentTime.TimeOfDay + TimeSpan.FromHours(1) <= LastSessionOfDayEnd)
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

    public class TimeSlotReservations
    {
        public HashSet<string> OpenReservations { get; set; }
        public Dictionary<string, HashSet<string>> TeamReservations { get; set; }
    }

    public class BookedTimeSlot
    {
        public DateTime StartTime { get; set; }
        public TimeSlotReservations Reservations { get; set; }
    }


    public class BookingStorage
    {
        private readonly ConcurrentDictionary<string, Dictionary<DateTime, UserReservation>> userReservations = new ConcurrentDictionary<string, Dictionary<DateTime, UserReservation>>();

        private readonly ConcurrentDictionary<DateTime, TimeSlotReservations> bookingsWithReservations = new ConcurrentDictionary<DateTime, TimeSlotReservations>();

        private readonly SemaphoreSlim reservationLock = new SemaphoreSlim(1);

        public IEnumerable<UserReservation> GetReservationsFor(string userId)
        {
            return userReservations.GetValueOrDefault(userId, new Dictionary<DateTime, UserReservation>()).Values;
        }

        private bool IsTimeWithin(DateTime slot, DateTime from, TimeSpan duration)
        {
            return slot >= from && slot <= from + duration;
        }

        public async Task<IEnumerable<BookedTimeSlot>> GetAllReservationsBetweenAsync(DateTime from, TimeSpan duration)
        {
            await reservationLock.WaitAsync();
            try
            {
                return bookingsWithReservations.Where(bookedTimeSlot => IsTimeWithin(bookedTimeSlot.Key, from, duration))
                    .Select(bookedTimeSlot => new BookedTimeSlot { Reservations = bookedTimeSlot.Value, StartTime = bookedTimeSlot.Key })
                    .ToList();
            }
            finally
            {
                reservationLock.Release();
            }
        }

        public async Task RemoveReservation(string userId, UserReservation reservation)
        {
            await reservationLock.WaitAsync();
            try
            {
                var reservationsForUser = userReservations.GetValueOrDefault(userId, new Dictionary<DateTime, UserReservation>());
                if (reservationsForUser.Remove(reservation.StartTime))
                {
                    var currentBookingsAtSameTime = bookingsWithReservations.GetValueOrDefault(reservation.StartTime,
                            new TimeSlotReservations { OpenReservations = new HashSet<string>(), TeamReservations = new Dictionary<string, HashSet<string>>() }
                        );
                    var reservationsForTeam = currentBookingsAtSameTime.TeamReservations.GetValueOrDefault(reservation.TeamId, new HashSet<string>());
                    if (reservationsForTeam.Remove(userId))
                        return;
                    currentBookingsAtSameTime.OpenReservations.Remove(userId);
                }
            }
            finally
            {
                reservationLock.Release();
            }
        }

        public async Task AddTeamReservation(string userId, UserReservation reservation)
        {
            await reservationLock.WaitAsync();
            try
            {
                var reservationsForUser = userReservations.GetValueOrDefault(userId, new Dictionary<DateTime, UserReservation>());
                if (!reservationsForUser.TryAdd(reservation.StartTime, reservation))
                    return;
                userReservations[userId] = reservationsForUser;

                var currentBookingsAtSameTime = bookingsWithReservations.GetValueOrDefault(reservation.StartTime,
                    new TimeSlotReservations { OpenReservations = new HashSet<string>(), TeamReservations = new Dictionary<string, HashSet<string>>() }
                    );
                var teamReservations = currentBookingsAtSameTime.TeamReservations.GetValueOrDefault(reservation.TeamId, new HashSet<string>());
                teamReservations.Add(userId);
                currentBookingsAtSameTime.TeamReservations[reservation.TeamId] = teamReservations;
                bookingsWithReservations[reservation.StartTime] = currentBookingsAtSameTime;
            }
            finally
            {
                reservationLock.Release();
            }
        }

        public async Task AddOpenReservation(string userId, UserReservation reservation)
        {
            await reservationLock.WaitAsync();
            try
            {
                var reservationsForUser = userReservations.GetValueOrDefault(userId, new Dictionary<DateTime, UserReservation>());
                if (!reservationsForUser.TryAdd(reservation.StartTime, reservation))
                    return;
                userReservations[userId] = reservationsForUser;

                var currentBookingsAtSameTime = bookingsWithReservations.GetValueOrDefault(reservation.StartTime,
                        new TimeSlotReservations { OpenReservations = new HashSet<string>(), TeamReservations = new Dictionary<string, HashSet<string>>() }
                    );
                currentBookingsAtSameTime.OpenReservations.Add(userId);
                bookingsWithReservations[reservation.StartTime] = currentBookingsAtSameTime;
            }
            finally
            {
                reservationLock.Release();
            }
        }
    }

    public class BookingService
    {

        private BookingStorage bookingStorage;
        private readonly AuthenticationStateProvider authenticationStateProvider;
        private readonly UserManager<ApplicationUser> userManager;

        public BookingService(AuthenticationStateProvider authenticationStateProvider, UserManager<ApplicationUser> userManager, BookingStorage bookingStorage)
        {
            this.authenticationStateProvider = authenticationStateProvider;
            this.userManager = userManager;
            this.bookingStorage = bookingStorage;
        }

        public async Task MakeOpenReservation(OpenSession session)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            await bookingStorage.AddOpenReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = "open" });
            OnBookingsChanged();
        }

        public async Task CancelOpenReservationAt(DateTime starttime)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            await bookingStorage.RemoveReservation(userId, new UserReservation { StartTime = starttime, TeamId = "open" });
            OnBookingsChanged();
        }

        public async Task CancelOpenReservation(OpenSession session)
        {
            await CancelOpenReservationAt(session.StartTime);
        }

        public async Task MakeTeamReservation(TeamSession session)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            await bookingStorage.AddTeamReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
            OnBookingsChanged();
        }

        public async Task CancelUserReservation(UserReservation reservation)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            await bookingStorage.RemoveReservation(userId, reservation);
            OnBookingsChanged();
        }

        public async Task CancelTeamReservation(TeamSession session)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            await bookingStorage.RemoveReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
            OnBookingsChanged();
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

        private readonly OpenSessionGenerator openSessionGenerator = new OpenSessionGenerator();

        public Task<IEnumerable<OpenSession>> GetOpenSessions(DateTime from, TimeSpan duration)
        {
            return Task.FromResult(openSessionGenerator.GetHourlyOpenSessions(from, duration));
        }

        private readonly TeamSessionGenerator teamSessionGenerator = new TeamSessionGenerator(ActiveSchedule.ScheduledTeams);

        public Task<IEnumerable<TeamSession>> GetTeamSessions(DateTime from, TimeSpan duration)
        {
            return Task.FromResult(teamSessionGenerator.GetTeamSlots(from, duration));
        }

        public Task<IEnumerable<BookedTimeSlot>> GetAllReservations(DateTime from, TimeSpan duration)
        {
            return bookingStorage.GetAllReservationsBetweenAsync(from, duration);
        }

        public delegate void BookingsChangedDelegate();

        public static event BookingsChangedDelegate OnBookingsChanged = delegate { };
    }

    public class TeamService
    {
        private readonly Dictionary<string, Team> teams = new Dictionary<string, Team>();

        public TeamService()
        {
            teams.Add("1", new Team { Duration = TimeSpan.FromMinutes(90), Id = "1", Name = "Beginner", Size = 10 });
            teams.Add("2", new Team { Duration = TimeSpan.FromMinutes(90), Id = "2", Name = "Intermediate", Size = 10 });
            teams.Add("3", new Team { Duration = TimeSpan.FromMinutes(90), Id = "3", Name = "Elite", Size = 10 });
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

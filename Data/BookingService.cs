using BookingApp.Areas.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    //dto?
    public class WeeklyTeamSchedule
    {
        public IEnumerable<WeeklyScheduledTeam> ScheduledTeams { get; set; }
        public DateTime ValidityStart { get; set; }
        public DateTime ValidityEnd { get; set; }
    }

    public class SpaceSchedule
    {
        private readonly TeamSessionGenerator sessionGenerator;
        private readonly TeamService teamService;
        private readonly int spaceSize = 24;

        public SpaceSchedule(TeamSessionGenerator sessionGenerator, TeamService teamService)
        {
            this.sessionGenerator = sessionGenerator;
            this.teamService = teamService;
        }

        public IEnumerable<TeamSession> GetTeamSessionsActiveDuring(TimeSlot timeSlot)
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


    public class TeamSessionGenerator
    {
        private readonly IEnumerable<WeeklyScheduledTeam> scheduledTeams;

        public TeamSessionGenerator(IEnumerable<WeeklyScheduledTeam> scheduledTeams)
        {
            this.scheduledTeams = scheduledTeams;
        }

        // get team sessions starting in the given interval
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


    public class OpenSessionGenerator
    {
        private readonly Team openTeam;

        private TimeSpan FirstSessionOfDay = TimeSpan.FromHours(6);
        private TimeSpan LastSessionOfDayEnd = TimeSpan.FromHours(23);

        public OpenSessionGenerator(Team openTeam)
        {
            this.openTeam = openTeam;
        }


        public IEnumerable<TeamSession> GetHourlyOpenSessions(DateTime from, TimeSpan duration)
        {
            var res = new List<TeamSession>();
            var currentTime = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0, from.Kind); // hourly rounded time
            var latestPossibleStart = from + duration - openTeam.Duration;
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
    }

    public class TimeSlot
    {
        public DateTime StartTime;
        public TimeSpan Duration;
        public bool Overlaps(TimeSlot other)
        {
            return (StartTime < other.StartTime + other.Duration) && (StartTime + Duration > other.StartTime);
        }
    }


    public class UserReservation: IEquatable<UserReservation>
    {
        public DateTime StartTime { get; set; }
        public string TeamId { get; set; }

        public bool Equals([AllowNull] UserReservation other)
        {
            if (other == null)
                return false;
            return other.StartTime == StartTime && other.TeamId == TeamId;
        }
    }

    public class BookedTimeSlot
    {
        public DateTime StartTime { get; set; }
        public Dictionary<string, List<string>> TeamReservations { get; set; }
    }


    public class BookingStorage
    {
        private readonly ConcurrentDictionary<string, List<UserReservation>> userReservations = new ConcurrentDictionary<string, List<UserReservation>>();

        private readonly ConcurrentDictionary<DateTime, Dictionary<string, List<string>>> bookingsWithReservations = new ConcurrentDictionary<DateTime, Dictionary<string, List<string>>>();

        private readonly SemaphoreSlim reservationLock = new SemaphoreSlim(1);

        public IEnumerable<UserReservation> GetReservationsFor(string userId)
        {
            return userReservations.GetValueOrDefault(userId, new List<UserReservation>());
        }

        public async Task<List<string>> GetReservationsForSession(string teamId, DateTime startTime)
        {
            await reservationLock.WaitAsync();
            try
            {
                Dictionary<string, List<string>> reservations;
                if (!bookingsWithReservations.TryGetValue(startTime, out reservations))
                    return new List<string>();
                return reservations.GetValueOrDefault(teamId, new List<string>());
            }
            finally
            {
                reservationLock.Release();
            }
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
                    .Select(bookedTimeSlot => new BookedTimeSlot { TeamReservations = bookedTimeSlot.Value, StartTime = bookedTimeSlot.Key })
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
                var reservationsForUser = userReservations.GetValueOrDefault(userId, new List<UserReservation>());
                if (reservationsForUser.Remove(reservation))
                {
                    var currentBookingsAtSameTime = bookingsWithReservations.GetValueOrDefault(reservation.StartTime,
                            new Dictionary<string, List<string>>()
                        );
                    var reservationsForTeam = currentBookingsAtSameTime.GetValueOrDefault(reservation.TeamId, new List<string>());
                    reservationsForTeam.Remove(userId);
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
                var reservationsForUser = userReservations.GetValueOrDefault(userId, new List<UserReservation>());
                reservationsForUser.Add(reservation);
                userReservations[userId] = reservationsForUser;

                var currentBookingsAtSameTime = bookingsWithReservations.GetValueOrDefault(reservation.StartTime,
                    new Dictionary<string, List<string>>()
                    );
                var teamReservations = currentBookingsAtSameTime.GetValueOrDefault(reservation.TeamId, new List<string>());
                teamReservations.Add(userId);
                currentBookingsAtSameTime[reservation.TeamId] = teamReservations;
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
        private readonly TeamService teamService;
        private readonly SpaceSchedule spaceSchedule;
        private readonly AuthenticationStateProvider authenticationStateProvider;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly OpenSessionGenerator openSessionGenerator;

        private readonly SemaphoreSlim reservationLock = new SemaphoreSlim(1);

        public BookingService(AuthenticationStateProvider authenticationStateProvider, UserManager<ApplicationUser> userManager, BookingStorage bookingStorage, TeamService teamService)
        {
            this.authenticationStateProvider = authenticationStateProvider;
            this.userManager = userManager;
            this.bookingStorage = bookingStorage;
            this.teamService = teamService;
            this.spaceSchedule = new SpaceSchedule(teamSessionGenerator, teamService);
            this.openSessionGenerator = new OpenSessionGenerator(teamService.GetTeam("open"));
        }

        public async Task MakeTeamReservation(TeamSession session)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            int maxTeamReservations = 0;
            if (teamService.GetTeam(session.TeamId).Limits.TryGetValue(TeamLimit.ActiveBookings, out maxTeamReservations))
            {
                await reservationLock.WaitAsync();
                try
                {
                    var reservationsForUser = bookingStorage.GetReservationsFor(userId);
                    if (reservationsForUser.Count(e => e.TeamId == session.TeamId && e.StartTime > DateTime.Now) < maxTeamReservations)
                        await bookingStorage.AddTeamReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
                    else
                        return;
                }
                finally
                {
                    reservationLock.Release();
                }
            }
            else 
            {
                await bookingStorage.AddTeamReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
            }
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

        public async Task<int> GetLoggedOnUserPositionForReservedSession(UserReservation userReservation)
        {
            var allreservations= await bookingStorage.GetReservationsForSession(userReservation.TeamId, userReservation.StartTime);
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await userManager.GetUserAsync(state.User)).Id;
            return allreservations.IndexOf(userId);
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
                new WeeklyScheduledTeam { Day = DayOfWeek.Saturday, TimeOfDay = TimeSpan.FromHours(17), TeamId = "1"},
            }
        };

        // this assumes that from is the start of the day and that teams do not span across days
        public Task<IEnumerable<TeamSession>> GetOpenSessions(DateTime from, TimeSpan duration)
        {
            return Task.FromResult(openSessionGenerator.GetHourlyOpenSessions(from, duration));
        }

        public int GetFreeSpace(TimeSlot timeSlot)
        {
            return spaceSchedule.GetFreeSpace(timeSlot);
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

    public class Team
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TimeSpan Duration { get; set; }

        public Dictionary<TeamLimit, int> Limits = new Dictionary<TeamLimit, int>();
    }

    public enum TeamLimit
    {
        Size=1,
        ActiveBookings=2
    }

    public class IntegerBookingLimit
    {
        public TeamLimit Type { get; set; }
        public int Value { get; set; }
    }
}

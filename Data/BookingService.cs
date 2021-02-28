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
    
    public class WeeklyTeamSchedule
    {
        public IEnumerable<WeeklyScheduledTeam> ScheduledTeams { get; set; }
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
        private readonly BookingStorage _bookingStorage;
        private readonly TeamService _teamService;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly SemaphoreSlim _reservationLock = new SemaphoreSlim(1);

        public BookingService(AuthenticationStateProvider authenticationStateProvider, UserManager<ApplicationUser> userManager, BookingStorage bookingStorage, TeamService teamService)
        {
            this._authenticationStateProvider = authenticationStateProvider;
            this._userManager = userManager;
            this._bookingStorage = bookingStorage;
            this._teamService = teamService;
        }

        public async Task MakeTeamReservation(TeamSession session)
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await _userManager.GetUserAsync(state.User)).Id;
            if (_teamService.GetTeam(session.TeamId).Limits.TryGetValue(TeamLimit.ActiveBookings, out var maxTeamReservations))
            {
                await _reservationLock.WaitAsync();
                try
                {
                    var reservationsForUser = _bookingStorage.GetReservationsFor(userId);
                    if (reservationsForUser.Count(e => e.TeamId == session.TeamId && e.StartTime > DateTime.Now) < maxTeamReservations)
                        await _bookingStorage.AddTeamReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
                    else
                        return;
                }
                finally
                {
                    _reservationLock.Release();
                }
            }
            else 
            {
                await _bookingStorage.AddTeamReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
            }
            OnBookingsChanged();
        }

        public async Task CancelUserReservation(UserReservation reservation)
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await _userManager.GetUserAsync(state.User)).Id;
            await _bookingStorage.RemoveReservation(userId, reservation);
            OnBookingsChanged();
        }

        public async Task CancelTeamReservation(TeamSession session)
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await _userManager.GetUserAsync(state.User)).Id;
            await _bookingStorage.RemoveReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
            OnBookingsChanged();
        }

        public async Task<int> GetLoggedOnUserPositionForReservedSession(UserReservation userReservation)
        {
            var allreservations= await _bookingStorage.GetReservationsForSession(userReservation.TeamId, userReservation.StartTime);
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await _userManager.GetUserAsync(state.User)).Id;
            return allreservations.IndexOf(userId);
        }

        public async Task<IEnumerable<UserReservation>> GetLoggedOnUserReservations()
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await _userManager.GetUserAsync(state.User)).Id;
            return _bookingStorage.GetReservationsFor(userId);
        }

        public Task<IEnumerable<BookedTimeSlot>> GetAllReservations(DateTime from, TimeSpan duration)
        {
            return _bookingStorage.GetAllReservationsBetweenAsync(from, duration);
        }

        public delegate void BookingsChangedDelegate();

        public static event BookingsChangedDelegate OnBookingsChanged = delegate { };
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
}

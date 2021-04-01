using BookingApp.Areas.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
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


    public class BookingService
    {
        private readonly IBookingStorage _bookingStorage;
        private readonly TeamService _teamService;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly SemaphoreSlim _reservationLock = new SemaphoreSlim(1);

        public BookingService(AuthenticationStateProvider authenticationStateProvider, UserManager<ApplicationUser> userManager, IBookingStorage bookingStorage, TeamService teamService)
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
                    var reservationsForUser = await _bookingStorage.GetReservationsFor(userId);
                    if (reservationsForUser.Count(e => e.TeamId == session.TeamId && e.StartTime > DateTime.Now) < maxTeamReservations)
                        await _bookingStorage.AddReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
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
                await _bookingStorage.AddReservation(userId, new UserReservation { StartTime = session.StartTime, TeamId = session.TeamId });
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
        
        public async Task CancelAllUserReservations(string userId)
        {
            await _bookingStorage.RemoveAllReservations(userId);
            OnBookingsChanged();
        }

        public async Task CancelTeamReservation(TeamSession session)
        {
            await CancelUserReservation(new UserReservation {StartTime = session.StartTime, TeamId = session.TeamId});
        }

        public async Task<int> GetLoggedOnUserPositionForReservedSession(UserReservation userReservation)
        {
            var allReservations= await _bookingStorage.GetReservationsForSession(userReservation.TeamId, userReservation.StartTime);
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await _userManager.GetUserAsync(state.User)).Id;
            return allReservations.IndexOf(userId);
        }

        public async Task<List<UserReservation>> GetLoggedOnUserReservations()
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var userId = (await _userManager.GetUserAsync(state.User)).Id;
            return await _bookingStorage.GetReservationsFor(userId);
        }

        public async Task<List<BookedTimeSlot>> GetAllReservations(DateTime from, TimeSpan duration)
        {
            return await _bookingStorage.GetAllReservationsBetweenAsync(from, duration);
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

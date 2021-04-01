using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Data
{
    public interface IBookingStorage
    {
        Task<List<UserReservation>> GetReservationsFor(string userId);
        Task<List<string>> GetReservationsForSession(string teamId, DateTime startTime);
        Task<List<BookedTimeSlot>> GetAllReservationsBetweenAsync(DateTime from, TimeSpan duration);
        Task RemoveReservation(string userId, UserReservation reservation);
        Task AddReservation(string userId, UserReservation reservation);
        Task RemoveAllReservations(string userId);
    }

    public class BookingStorage : IBookingStorage
    {
        private readonly ConcurrentDictionary<string, List<UserReservation>> userReservations = new ConcurrentDictionary<string, List<UserReservation>>();

        private readonly ConcurrentDictionary<DateTime, Dictionary<string, List<string>>> bookingsWithReservations = new ConcurrentDictionary<DateTime, Dictionary<string, List<string>>>();

        private readonly SemaphoreSlim reservationLock = new SemaphoreSlim(1);

        public Task<List<UserReservation>> GetReservationsFor(string userId)
        {
            return Task.FromResult(userReservations.GetValueOrDefault(userId, new List<UserReservation>()));
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

        public async Task<List<BookedTimeSlot>> GetAllReservationsBetweenAsync(DateTime from, TimeSpan duration)
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

        public async Task AddReservation(string userId, UserReservation reservation)
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

        public Task RemoveAllReservations(string userId)
        {
            throw new NotImplementedException();
        }
    }
    
    
    public class EfBookingStorage :IBookingStorage
    {
        private readonly TeamService _teamService;
        private readonly BookingsDbContext db;

        public EfBookingStorage(TeamService teamService, BookingsDbContext dbContext)
        {
            _teamService = teamService;
            db = dbContext;
        }
        
        public async Task<List<UserReservation>> GetReservationsFor(string userId)
        {
            return await db.UserReservations.Where(e => e.UserId == userId).Select(e=>new UserReservation
            {
                StartTime = e.StartTime,
                TeamId = e.TeamId
            }).ToListAsync();
        }

        public async Task<List<string>> GetReservationsForSession(string teamId, DateTime startTime)
        {
            return await db.UserReservations.Where(e => e.TeamId == teamId && e.StartTime == startTime).Select(e => e.UserId).ToListAsync();
        }

        public async Task<List<BookedTimeSlot>> GetAllReservationsBetweenAsync(DateTime from, TimeSpan duration)
        {
            var endTime = from + duration;
            var allReservationsBetween = await db.UserReservations.Where(e => (e.StartTime < endTime) && (e.EndTime > from)).ToListAsync();
            
            // convert to booked time slots (client side)
            return allReservationsBetween.GroupBy(e =>
                new {e.StartTime, e.TeamId}
            ).Select(e => new BookedTimeSlot()
            {
                StartTime = e.Key.StartTime,
                TeamReservations = new Dictionary<string, List<string>>()
                {
                    {
                        e.Key.TeamId,
                        e.Select(u => u.UserId).ToList()
                    }
                }
            }).ToList();
        }

        public async Task RemoveReservation(string userId, UserReservation reservation)
        {
            var itemToRemove = await db.UserReservations.FirstOrDefaultAsync(res =>
                res.UserId == userId && res.TeamId == reservation.TeamId && res.StartTime == reservation.StartTime);
            if (itemToRemove == null)
                return;
            db.Remove(itemToRemove);
            await db.SaveChangesAsync();
        }

        public async Task AddReservation(string userId, UserReservation reservation)
        {
            var reservationEntity = new UserReservationEntity
            {
                StartTime = reservation.StartTime, 
                TeamId = reservation.TeamId, 
                UserId = userId,
                EndTime = reservation.StartTime+_teamService.GetTeam(reservation.TeamId).Duration
            };
            db.Add(reservationEntity);
            await db.SaveChangesAsync();
        }

        public async Task RemoveAllReservations(string userId)
        {
            var allReservations= db.UserReservations.Where(e => e.UserId == userId);
            db.UserReservations.RemoveRange(allReservations);
            await db.SaveChangesAsync();
        }
    }
    
}
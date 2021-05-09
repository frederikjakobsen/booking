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
    
    public class EfBookingStorage :IBookingStorage
    {
        private readonly TeamService _teamService;
        private readonly BookingsDbContext _db;
        private readonly DbContextOptions<BookingsDbContext> _dbContextOptions;

        public EfBookingStorage(TeamService teamService, BookingsDbContext dbContext, DbContextOptions<BookingsDbContext> dbContextOptions)
        {
            _teamService = teamService;
            _db = dbContext;
            _dbContextOptions = dbContextOptions;
        }
        
        public async Task<List<UserReservation>> GetReservationsFor(string userId)
        {
            return await _db.UserReservations.Where(e => e.UserId == userId).Select(e=>new UserReservation
            {
                StartTime = e.StartTime,
                TeamId = e.TeamId
            }).AsNoTracking().ToListAsync();
        }

        public async Task<List<string>> GetReservationsForSession(string teamId, DateTime startTime)
        {
            var res= (await _db.UserReservations.Where(e => e.TeamId == teamId && e.StartTime == startTime).AsNoTracking().ToListAsync());
            return res.Select(e => e.UserId).ToList();
        }

        public async Task<List<BookedTimeSlot>> GetAllReservationsBetweenAsync(DateTime from, TimeSpan duration)
        {
            var endTime = from + duration;
            var allReservationsBetween = await _db.UserReservations.Where(e => (e.StartTime < endTime) && (e.EndTime > from)).AsNoTracking().ToListAsync();
            
            // convert to booked time slots (client side)
            return allReservationsBetween.GroupBy(e =>
                new {e.StartTime}
            ).Select(e => new BookedTimeSlot()
            {
                StartTime = e.Key.StartTime,
                TeamReservations = e.GroupBy(u=>u.TeamId).ToDictionary(y=>y.Key, y=>y.Select(z=>z.UserId).ToList())
            }).ToList();
        }

        public async Task RemoveReservation(string userId, UserReservation reservation)
        {
            await using var dbCtx = new BookingsDbContext(_dbContextOptions);
            var itemToRemove = await dbCtx.UserReservations.FirstOrDefaultAsync(res =>
                res.UserId == userId && res.TeamId == reservation.TeamId && res.StartTime == reservation.StartTime);
            if (itemToRemove == null)
                return;
            dbCtx.UserReservations.Remove(itemToRemove);
            await dbCtx.SaveChangesAsync();
        }

        public async Task AddReservation(string userId, UserReservation reservation)
        {
            await using var dbCtx = new BookingsDbContext(_dbContextOptions);
            var reservationEntity = new UserReservationEntity
            {
                StartTime = reservation.StartTime, 
                TeamId = reservation.TeamId, 
                UserId = userId,
                EndTime = reservation.StartTime+_teamService.GetTeam(reservation.TeamId).Duration
            };
            dbCtx.UserReservations.Add(reservationEntity);
            await dbCtx.SaveChangesAsync();
        }

        public async Task RemoveAllReservations(string userId)
        {
            await using var dbCtx = new BookingsDbContext(_dbContextOptions);
            var allReservations= _db.UserReservations.Where(e => e.UserId == userId);
            dbCtx.UserReservations.RemoveRange(allReservations);
            await dbCtx.SaveChangesAsync();
        }
    }
    
}
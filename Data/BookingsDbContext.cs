using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Data
{
    public class BookingsDbContext : DbContext
    {
        public DbSet<UserReservationEntity> UserReservations { get; set; }

        public BookingsDbContext(DbContextOptions<BookingsDbContext> options):base(options) 
        {
            
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserReservationEntity>()
                .HasKey(c => new { c.UserId, c.StartTime, c.TeamId });
        }
        
    }

    [Index(nameof(UserId), nameof(StartTime),nameof(TeamId), IsUnique = true)]
    public class UserReservationEntity
    {
        public string UserId { get; init; }
        public DateTime StartTime { get; init; }
        public string TeamId { get; init; }
        public DateTime EndTime { get; init; }
    }
}
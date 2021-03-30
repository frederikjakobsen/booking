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
    }

    public class UserReservationEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string UserId { get; set; }
        public DateTime StartTime { get; set; }
        public string TeamId { get; set; }
        public DateTime EndTime { get; set; }
    }
}
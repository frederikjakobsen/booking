using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookingApp.Areas.Identity;
using Microsoft.AspNetCore.Identity;

namespace BookingApp.Data
{
    public class BookingUser
    {
        public string OwnerId { get; set; }
        public string Name { get; set; }

        public string Email { get; set; }
        public bool Approved { get; set; }
        
        public bool EmailConfirmed { get; set; }
    }

    public class UserManagerService
    {

        public UserManagerService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, BookingService bookingService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _bookingService = bookingService;
        }

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly BookingService _bookingService;

        private static async Task<BookingUser> InitUser(UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            return new() { OwnerId = user.Id, Name = user.Name, Email = user.Email, Approved = await userManager.IsInRoleAsync(user, "approved"), EmailConfirmed = user.EmailConfirmed};
        }

        public async Task<IEnumerable<BookingUser>> GetUsers()
        {

            var res = _userManager.Users.Select(user => InitUser(_userManager, user));
            return await Task.WhenAll(res);
        }

        public async Task ApproveUser(BookingUser user)
        {
            var idUser= await _userManager.FindByIdAsync(user.OwnerId);
            await EnsureRole(idUser, "approved");
        }

        public async Task RejectUser(BookingUser user)
        {
            var idUser = await _userManager.FindByIdAsync(user.OwnerId);
            await EnsureNoRole(idUser, "approved");
        }
        
        // note that the user does not get signed out by calling this
        public async Task DeleteUser(BookingUser user)
        {
            await _bookingService.CancelAllUserReservations(user.OwnerId);
            var idUser = await _userManager.FindByIdAsync(user.OwnerId);
            await _userManager.UpdateSecurityStampAsync(idUser);
            await _userManager.DeleteAsync(idUser);
        }

        private async Task EnsureNoRole(ApplicationUser user, string role)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                return;
            }

            var IR = await _userManager.RemoveFromRoleAsync(user, role);
            if (!IR.Succeeded)
                throw new Exception("Failed to remove role");
        }

        private async Task EnsureRole(ApplicationUser user, string role)
        {
            IdentityResult IR = null;

            if (!await _roleManager.RoleExistsAsync(role))
            {
                IR = await _roleManager.CreateAsync(new IdentityRole(role));
                if (!IR.Succeeded)
                    throw new Exception("Failed to create role");
            }

            IR = await _userManager.AddToRoleAsync(user, role);
            if (!IR.Succeeded)
                throw new Exception("Failed to add role");
        }
    }
}

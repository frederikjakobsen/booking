using BookingApp.Areas.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookingApp.Data
{
    public class BookingUser
    {
        public string OwnerId { get; set; }
        public string Name { get; set; }
        public bool Approved { get; set; }
    }

    public class UserManagerService
    {

        public UserManagerService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AuthenticationStateProvider authenticationStateProvider)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.authenticationStateProvider = authenticationStateProvider;
        }

        private readonly UserManager<ApplicationUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly AuthenticationStateProvider authenticationStateProvider;

        private static async Task<BookingUser> InitUser(UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            return new BookingUser { OwnerId = user.Id, Name = user.Name, Approved = await userManager.IsInRoleAsync(user, "approved") };
        }

        public async Task<IEnumerable<BookingUser>> GetUsers()
        {

            var res = userManager.Users.Select(user => InitUser(userManager, user));
            return await Task.WhenAll(res);
        }

        public async Task ApproveUser(BookingUser user)
        {
            var idUser= await userManager.FindByIdAsync(user.OwnerId);
            await EnsureRole(idUser, "approved");
        }

        public async Task RejectUser(BookingUser user)
        {
            var idUser = await userManager.FindByIdAsync(user.OwnerId);
            await EnsureNoRole(idUser, "approved");
        }

        private async Task EnsureNoRole(ApplicationUser user, string role)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                return;
            }

            var IR = await userManager.RemoveFromRoleAsync(user, role);
            if (!IR.Succeeded)
                throw new Exception("Failed to remove role");
        }

        private async Task EnsureRole(ApplicationUser user, string role)
        {
            IdentityResult IR = null;

            if (!await roleManager.RoleExistsAsync(role))
            {
                IR = await roleManager.CreateAsync(new IdentityRole(role));
                if (!IR.Succeeded)
                    throw new Exception("Failed to create role");
            }

            IR = await userManager.AddToRoleAsync(user, role);
            if (!IR.Succeeded)
                throw new Exception("Failed to add role");
        }
    }
}

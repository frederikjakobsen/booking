using BookingApp.Areas.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;


namespace BookingApp.Data
{

    public class UserService
    {

        public UserService(UserManager<ApplicationUser> userManager)
        {
            this.userManager = userManager;
        }

        private readonly UserManager<ApplicationUser> userManager;

        public async Task<IEnumerable<string>> GetUsersAsync(IEnumerable<string> userIds)
        {
            var res = new List<string>();
            foreach(var userId in userIds)
            {
                var user = await userManager.Users.FirstOrDefaultAsync(user => user.Id == userId);
                if (user!=null)
                    res.Add(user.Name);
            }
            return res;
        }
    }
}

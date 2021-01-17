using BookingApp.Areas.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace BookingApp.Data
{

    public class UserService
    {

        public UserService(UserManager<ApplicationUser> userManager)
        {
            this.userManager = userManager;
        }

        private readonly UserManager<ApplicationUser> userManager;

        public IEnumerable<string> GetUsers(IEnumerable<string> userIds)
        {
            var res = new List<string>();
            foreach(var userId in userIds)
            {
                res.Add(userManager.Users.First(user => user.Id == userId).Name);
            }
            return res;
        }
    }
}

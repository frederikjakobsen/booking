using System;
using System.Collections.Generic;
using BookingApp.Data;

namespace BookingApp.Services
{
    public class WeeklyScheduledTeamOption
    {
        public string TeamId { get; init; }
        public DayOfWeek Day { get; init; }
        public TimeSpan TimeOfDay { get; init; }
    }
    
    public class TeamOption
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public TimeSpan Duration { get; init; }
        public Dictionary<string, int> Limits { get; init; }
    }

    public class SpaceOptions
    {
        public int Size { get; init; }
       
    }
    
}
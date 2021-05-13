using System.Collections.Generic;
using System.Linq;

namespace BookingApp.Shared
{
    public class ReservationStateSimplifier
    {
        
        public enum UserSessionReservationState
        {
            None,
            Joined,
            InQueue
        }
    
        public enum SessionReservationState
        {
            Empty,
            Partial,
            Full,
            Queue
        }
        
        private readonly List<string> _reservations;
        private readonly int _sessionSize;

        public ReservationStateSimplifier(List<string> reservations, int sessionSize)
        {
            _reservations = reservations;
            _sessionSize = sessionSize;
        }
        
        public UserSessionReservationState GetUserState(string userId)
        {
            var userIdx = _reservations.IndexOf(userId);
            if (userIdx == -1)
            {
                return UserSessionReservationState.None;
            }
            if (userIdx < _sessionSize)
                return UserSessionReservationState.Joined;
            return UserSessionReservationState.InQueue;
        }

        public SessionReservationState GetSessionState()
        {
            var available = _sessionSize - _reservations.Count();
            if (available < 0)
                return SessionReservationState.Queue;
            if (available == 0)
                return SessionReservationState.Full;
            if (available == _sessionSize)
                return SessionReservationState.Empty;
            return SessionReservationState.Partial;
        }
    }
}
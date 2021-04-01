namespace BookingApp.Services
{
    public class AuthMessageSenderOptions
    {
        public string SendGridInfoAddress { get; set; }
        public string SendGridUser { get; set; }
        public string SendGridKey { get; set; }
    }
}
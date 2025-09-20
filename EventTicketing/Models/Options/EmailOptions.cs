namespace EventTicketing.Models.Options
{
    public class EmailOptions
    {
        public string FromName { get; set; } = "Star Events";
        public string FromEmail { get; set; } = "no-reply@starevents.local";
        public string SmtpHost { get; set; } = "localhost";
        public int SmtpPort { get; set; } = 1025;
        public bool UseSsl { get; set; } = false;
        public string? User { get; set; }
        public string? Pass { get; set; }
    }
}
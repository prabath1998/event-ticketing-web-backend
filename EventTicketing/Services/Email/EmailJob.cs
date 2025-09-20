namespace EventTicketing.Services.Email
{
    public record EmailJob(string To, string Subject, string HtmlBody);
}
namespace TishtryaCMS.Modules.Tickets.Domain;

public static class TicketStatuses
{
    public const string Open = "open";
    public const string Answered = "answered";
    public const string Closed = "closed";

    public static bool IsKnown(string? value) =>
        value is Open or Answered or Closed;
}

namespace TishtryaCMS.Modules.Tickets.Domain;

public static class TicketCategories
{
    public const string News = "news";
    public const string Event = "event";
    public const string Support = "support";
    public const string HumanRights = "human_rights";
    public const string Other = "other";

    public static bool IsKnown(string? value) =>
        value is News or Event or Support or HumanRights or Other;
}

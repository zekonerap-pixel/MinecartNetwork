using System.Text;
using MinecartNetwork.Models;
using StardewModdingAPI;
using StardewValley;

namespace MinecartNetwork.Services;

public sealed class LocationRegionService
{
    private readonly IModHelper helper;

    public LocationRegionService(IModHelper helper)
    {
        this.helper = helper;
    }

    public string GetStationCategory(MinecartStation station)
    {
        if (!station.UseAutomaticCategory)
            return string.IsNullOrWhiteSpace(station.Category)
                ? this.helper.Translation.Get("region.other")
                : station.Category;

        return this.GetCategoryForLocation(station.LocationName);
    }

    public string GetCategoryForDestination(string destinationId, string targetLocation)
    {
        string id = Normalize(destinationId);

        if (id == "town")
            return this.helper.Translation.Get("region.town");
        if (id == "mines")
            return this.helper.Translation.Get("region.mines");
        if (id == "bus")
            return this.helper.Translation.Get("region.farm");
        if (id == "quarry")
            return this.helper.Translation.Get("region.mountain");

        return this.GetCategoryForLocation(targetLocation);
    }

    public string GetCategoryForLocation(string? locationName)
    {
        string normalized = Normalize(locationName);
        string typeName = "";

        if (!string.IsNullOrWhiteSpace(locationName) && Context.IsWorldReady)
        {
            GameLocation? location = Game1.getLocationFromName(locationName);
            typeName = Normalize(location?.GetType().Name);
        }

        string signal = $"{normalized} {typeName}";

        if (ContainsAny(signal,
                "undergroundmine", "mine", "mineshaft", "skullcave", "skullcavern", "dangerousmine"))
            return this.helper.Translation.Get("region.mines");

        if (ContainsAny(signal,
                "island", "ginger", "volcano", "caldera", "piratecove", "fieldoffice", "islandfarm"))
            return this.helper.Translation.Get("region.island");

        if (ContainsAny(signal,
                "desert", "calico", "oas", "sandy"))
            return this.helper.Translation.Get("region.desert");

        if (ContainsAny(signal,
                "forest", "cindersap", "woods", "secretwoods", "wizard", "witchswamp", "marnie", "leahhouse"))
            return this.helper.Translation.Get("region.forest");

        if (ContainsAny(signal,
                "beach", "fishshop", "elliotthouse", "tidalpool"))
            return this.helper.Translation.Get("region.beach");

        if (ContainsAny(signal,
                "mountain", "railroad", "bathhouse", "adventureguild", "quarry", "summit", "carpenter", "sciencehouse"))
            return this.helper.Translation.Get("region.mountain");

        if (ContainsAny(signal,
                "farmhouse", "farm", "greenhouse", "cellar", "farmcave", "shed", "slimehutch"))
            return this.helper.Translation.Get("region.farm");

        if (ContainsAny(signal,
                "town", "seedshop", "blacksmith", "communitycenter", "saloon", "hospital", "manorhouse", "trailer", "alexhouse", "haleyhouse", "samhouse", "jojamart", "movietheater", "museum"))
            return this.helper.Translation.Get("region.town");

        return this.helper.Translation.Get("region.other");
    }

    public string HumanizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return this.helper.Translation.Get("vanilla.minecart");

        string text = value.Trim();
        int separator = Math.Max(text.LastIndexOf('/'), Math.Max(text.LastIndexOf(':'), text.LastIndexOf('.')));
        if (separator >= 0 && separator < text.Length - 1)
            text = text[(separator + 1)..];

        var builder = new StringBuilder(text.Length + 8);
        char previous = '\0';

        foreach (char current in text)
        {
            if (current is '_' or '-' or '/')
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                    builder.Append(' ');
                previous = current;
                continue;
            }

            if (builder.Length > 0
                && char.IsUpper(current)
                && (char.IsLower(previous) || char.IsDigit(previous))
                && builder[^1] != ' ')
                builder.Append(' ');

            builder.Append(current);
            previous = current;
        }

        string result = string.Join(' ', builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.IsNullOrWhiteSpace(result) ? value : result;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }
}

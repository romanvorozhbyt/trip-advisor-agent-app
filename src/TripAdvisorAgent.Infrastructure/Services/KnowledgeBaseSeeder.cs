using System.Security.Cryptography;
using System.Text;
using TripAdvisorAgent.Core.Interfaces;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Seeds the knowledge base with initial travel data on application startup.
/// </summary>
public static class KnowledgeBaseSeeder
{
    // Stable DNS namespace GUID used as the UUID v5 namespace for deterministic IDs.
    private static readonly Guid DnsNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>
    /// Creates a deterministic UUID v5 from a namespace GUID and a UTF-8 string name.
    /// </summary>
    private static Guid DeterministicId(string content)
    {
        var namespaceBytes = DnsNamespace.ToByteArray();
        // UUID v5 requires big-endian namespace bytes
        SwapEndianness(namespaceBytes);
        var nameBytes = Encoding.UTF8.GetBytes(content);

        byte[] hash = SHA1.HashData([.. namespaceBytes, .. nameBytes]);

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant RFC 4122

        var guidBytes = hash[..16];
        SwapEndianness(guidBytes);
        return new Guid(guidBytes);
    }

    private static void SwapEndianness(byte[] b)
    {
        // Swap time_low (0-3), time_mid (4-5), time_hi_and_version (6-7)
        (b[0], b[3]) = (b[3], b[0]);
        (b[1], b[2]) = (b[2], b[1]);
        (b[4], b[5]) = (b[5], b[4]);
        (b[6], b[7]) = (b[7], b[6]);
    }

    private static readonly (string Content, string Category)[] SeedEntries =
    [
        (
            "Paris, France: Known as the City of Light, Paris offers iconic landmarks like the Eiffel Tower, Louvre Museum, and Notre-Dame Cathedral. Best visited in spring (April-June) or fall (September-November) for mild weather and fewer crowds. Budget tip: get a Paris Museum Pass for skip-the-line access to 60+ museums.",
            "destination"
        ),
        (
            "Tokyo, Japan: A vibrant blend of ultra-modern and traditional culture. Visit Shibuya Crossing, Senso-ji Temple, and Tsukiji Outer Market. Cherry blossom season (late March to mid-April) is peak time. A 7-day Japan Rail Pass saves significantly on bullet train travel.",
            "destination"
        ),
        (
            "Barcelona, Spain: Famous for Gaudí's architecture including Sagrada Família and Park Güell. Mediterranean beaches, vibrant nightlife, and tapas culture. Best visited May-June or September-October. Book Sagrada Família tickets well in advance — they sell out weeks ahead.",
            "destination"
        ),
        (
            "Bali, Indonesia: Tropical paradise with rice terraces, temples, and surf beaches. Ubud for culture, Seminyak for nightlife, Uluwatu for cliffs and surf. Dry season (April-October) is ideal. Very budget-friendly — a quality meal costs around $3-5 USD.",
            "destination"
        ),
        (
            "New York City, USA: Iconic skyline, Broadway shows, Central Park, and world-class museums like the Met and MoMA. Fall (September-November) offers beautiful foliage and comfortable weather. Get a CityPASS for discounted entry to top attractions.",
            "destination"
        ),
        (
            "Travel packing tip: Roll clothes instead of folding to save space and reduce wrinkles. Always pack a portable charger, universal adapter, and a reusable water bottle. Keep important documents (passport copies, insurance) in both digital and physical form.",
            "travel-tips"
        ),
        (
            "Budget travel strategies: Book flights 6-8 weeks in advance for the best prices. Use fare comparison tools like Google Flights or Skyscanner. Consider shoulder season travel for lower prices and fewer crowds. Eat where locals eat — avoid tourist-trap restaurants near major attractions.",
            "budget"
        ),
        (
            "Travel safety essentials: Register with your embassy before international trips. Keep digital copies of all important documents in secure cloud storage. Use RFID-blocking wallets in crowded tourist areas. Research local scams at your destination beforehand. Always have travel insurance.",
            "safety"
        ),
    ];

    /// <summary>
    /// Initializes the knowledge base and seeds it with travel data if not already seeded.
    /// </summary>
    public static async Task SeedAsync(IKnowledgeBaseService knowledgeBase, CancellationToken cancellationToken = default)
    {
        await knowledgeBase.InitializeAsync(cancellationToken);

        // Check the first entry — if it exists, the collection is already seeded.
        var firstId = DeterministicId(SeedEntries[0].Content);
        if (await knowledgeBase.RecordExistsAsync(firstId, cancellationToken))
            return;

        foreach (var (content, category) in SeedEntries)
        {
            var id = DeterministicId(content);
            await knowledgeBase.IngestAsync(content, category, id, cancellationToken);
        }
    }
}

using TripAdvisorAgent.Core.Interfaces;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Seeds the knowledge base with initial travel data on application startup.
/// </summary>
public static class KnowledgeBaseSeeder
{
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
    /// Initializes the knowledge base and seeds it with travel data.
    /// </summary>
    public static async Task SeedAsync(IKnowledgeBaseService knowledgeBase, CancellationToken cancellationToken = default)
    {
        await knowledgeBase.InitializeAsync(cancellationToken);

        foreach (var (content, category) in SeedEntries)
        {
            await knowledgeBase.IngestAsync(content, category, cancellationToken);
        }
    }
}

namespace SEAITT_LMS.Services;

/// <summary>
/// The lists the student chooses from in the application: the suffixes of a name, the provinces of the Philippines, and the cities and municipalities of the provinces of the Bicol region (where the
/// school is), and the barangays most students of Legazpi City live in. Every list ends with <see cref="Others"/>: the student chooses it and types what is not in the list.
/// </summary>
public static class PhilippineAddresses
{
    /// <summary>The last choice of every list: the student types their own.</summary>
    public const string Others = "Others";

    public static readonly string[] Suffixes = ["Jr.", "Sr.", "I", "II", "III", "IV", "V"];

    public static readonly string[] CivilStatuses = ["Single", "Married", "Widowed", "Separated"];

    public static readonly string[] Nationalities = ["Filipino"];

    public static readonly string[] Religions =
    [
        "Roman Catholic", "Iglesia ni Cristo", "Islam", "Born Again Christian", "Protestant", "Aglipayan", "Seventh-day Adventist", "Jehovah's Witnesses", "Buddhism", "None",
    ];

    public static readonly string[] Relationships = ["Father", "Mother", "Grandfather", "Grandmother", "Aunt", "Uncle", "Brother", "Sister"];

    public static readonly string[] Provinces =
    [
        "Abra", "Agusan del Norte", "Agusan del Sur", "Aklan", "Albay", "Antique", "Apayao", "Aurora", "Basilan", "Bataan", "Batanes", "Batangas", "Benguet", "Biliran", "Bohol", "Bukidnon",
        "Bulacan", "Cagayan", "Camarines Norte", "Camarines Sur", "Camiguin", "Capiz", "Catanduanes", "Cavite", "Cebu", "Cotabato", "Davao de Oro", "Davao del Norte", "Davao del Sur",
        "Davao Occidental", "Davao Oriental", "Dinagat Islands", "Eastern Samar", "Guimaras", "Ifugao", "Ilocos Norte", "Ilocos Sur", "Iloilo", "Isabela", "Kalinga", "La Union", "Laguna",
        "Lanao del Norte", "Lanao del Sur", "Leyte", "Maguindanao del Norte", "Maguindanao del Sur", "Marinduque", "Masbate", "Metro Manila", "Misamis Occidental", "Misamis Oriental",
        "Mountain Province", "Negros Occidental", "Negros Oriental", "Northern Samar", "Nueva Ecija", "Nueva Vizcaya", "Occidental Mindoro", "Oriental Mindoro", "Palawan", "Pampanga",
        "Pangasinan", "Quezon", "Quirino", "Rizal", "Romblon", "Samar", "Sarangani", "Siquijor", "Sorsogon", "South Cotabato", "Southern Leyte", "Sultan Kudarat", "Sulu", "Surigao del Norte",
        "Surigao del Sur", "Tarlac", "Tawi-Tawi", "Zambales", "Zamboanga del Norte", "Zamboanga del Sur", "Zamboanga Sibugay",
    ];

    private static readonly Dictionary<string, string[]> Cities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Albay"] = ["Legazpi City", "Ligao City", "Tabaco City", "Bacacay", "Camalig", "Daraga", "Guinobatan", "Jovellar", "Libon", "Malilipot", "Malinao", "Manito", "Oas", "Pio Duran", "Polangui", "Rapu-Rapu", "Santo Domingo", "Tiwi"],
        ["Camarines Sur"] =
        [
            "Iriga City", "Naga City", "Baao", "Balatan", "Bato", "Bombon", "Buhi", "Bula", "Cabusao", "Calabanga", "Camaligan", "Canaman", "Caramoan", "Del Gallego", "Gainza", "Garchitorena",
            "Goa", "Lagonoy", "Libmanan", "Lupi", "Magarao", "Milaor", "Minalabac", "Nabua", "Ocampo", "Pamplona", "Pasacao", "Pili", "Presentacion", "Ragay", "Sagñay", "San Fernando", "San Jose",
            "Sipocot", "Siruma", "Tigaon", "Tinambac",
        ],
        ["Sorsogon"] = ["Sorsogon City", "Barcelona", "Bulan", "Bulusan", "Casiguran", "Castilla", "Donsol", "Gubat", "Irosin", "Juban", "Magallanes", "Matnog", "Pilar", "Prieto Diaz", "Santa Magdalena"],
        ["Masbate"] =
        [
            "Masbate City", "Aroroy", "Baleno", "Balud", "Batuan", "Cataingan", "Cawayan", "Claveria", "Dimasalang", "Esperanza", "Mandaon", "Milagros", "Mobo", "Monreal", "Palanas", "Pio V. Corpuz",
            "Placer", "San Fernando", "San Jacinto", "San Pascual", "Uson",
        ],
        ["Catanduanes"] = ["Virac", "Bagamanoc", "Baras", "Bato", "Caramoran", "Gigmoto", "Pandan", "Panganiban", "San Andres", "San Miguel", "Viga"],
        ["Camarines Norte"] = ["Daet", "Basud", "Capalonga", "Jose Panganiban", "Labo", "Mercedes", "Paracale", "San Lorenzo Ruiz", "San Vicente", "Santa Elena", "Talisay", "Vinzons"],
    };

    private static readonly Dictionary<string, string[]> Barangays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Legazpi City"] =
        [
            "Arimbay", "Bagacay", "Bagong Abre", "Banquerohan", "Bariis", "Bigaa", "Binanuahan", "Bitano", "Bogña", "Bonot", "Buenavista", "Buyuan", "Cabagñan", "Cabangan", "Cruzada", "Dap-dap",
            "Dinagaan", "Em's Barrio", "Gogon", "Homapon", "Lamba", "Mabinit", "Maoyod", "Oro Site", "Padang", "Pigcale", "Puro", "Rawis", "Sagpon", "San Francisco", "San Roque", "Tamaoyan",
            "Taysan", "Tuburan",
        ],
    };

    /// <summary>The provinces, then Others.</summary>
    public static IReadOnlyList<string> ProvinceChoices() => [.. Provinces, Others];

    /// <summary>The cities and municipalities of a province that is in the list, then Others (the other provinces have only Others).</summary>
    public static IReadOnlyList<string> CityChoices(string? province) =>
        province is not null && Cities.TryGetValue(province, out var cities) ? [.. cities, Others] : [Others];

    /// <summary>The barangays of a city that is in the list, then Others.</summary>
    public static IReadOnlyList<string> BarangayChoices(string? city) =>
        city is not null && Barangays.TryGetValue(city, out var barangays) ? [.. barangays, Others] : [Others];

    public static IReadOnlyList<string> SuffixChoices() => [.. Suffixes, Others];
}

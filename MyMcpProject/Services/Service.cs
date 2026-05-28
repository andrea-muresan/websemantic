using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyMcpProject.Services;

public class Service
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _server1Json = "http://localhost:4000";
    private readonly string _server2Graphql = "http://localhost:3000";
    private readonly string _server3Rdf = "http://localhost:8080/rdf4j-server/repositories/grafexamen";

    public Service(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["ApiSettings:GeminiApiKey"] ?? string.Empty;
    }

    public async Task<JsonNode?> CallGeminiAsync(object payload)
    {
        string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
        var response = await _httpClient.PostAsJsonAsync(geminiUrl, payload);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<JsonNode>();
    }

    public async Task<JsonNode?> S1GetReportsByTypeAsync(string crimeType)
    {
        var response = await _httpClient.GetAsync($"{_server1Json}/reports?crimeType={crimeType}&_embed=evidence");
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(content);
    }

    public async Task<JsonNode?> S1CreateEvidenceAsync(int reportId, string evidenceType, string description)
    {
        var payload = new { reportId, evidenceType, description };
        var response = await _httpClient.PostAsJsonAsync($"{_server1Json}/evidence", payload);
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(content);
    }

    public async Task<JsonNode?> S2GetOldShowsAsync(int yearLte)
    {
        var graphqlQuery = new
        {
            query = @"query($year: Int) {
              allShows(filter: { year_lte: $year }) {
                title
                creator
                year
                Characters {
                  name
                  species
                }
              }
            }",
            variables = new { year = yearLte }
        };
        var jsonPayload = JsonSerializer.Serialize(graphqlQuery);
        var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_server2Graphql, httpContent);
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(content);
    }

    public async Task<JsonNode?> S2CreateShowAsync(string title, string creator, int year)
    {
        var graphqlQuery = new
        {
            query = @"mutation($title: String!, $creator: String!, $year: Int!) {
                        createShow(title: $title, creator: $creator, year: $year) {
                            id
                            title
                        }
                    }",
            variables = new { title, creator, year }
        };

        var jsonPayload = JsonSerializer.Serialize(graphqlQuery);
        var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(_server2Graphql, httpContent);
        
        if (!response.IsSuccessStatusCode) return null;
        
        return JsonNode.Parse(await response.Content.ReadAsStringAsync());
    }

    public async Task<JsonNode?> S3GetBooksAfterYearAsync(int anMinim)    
    {
        string sparqlQuery = $@"
            PREFIX s: <https://schema.org/>
            PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
            SELECT ?numeCarte ?an ?pagini ?numeAutor
            WHERE {{
                ?carte a s:Book ;
                    s:name ?numeCarte ;
                    s:copyrightYear ?an ;
                    s:numberOfPages ?pagini ;
                    s:author ?autor .
                ?autor s:name ?numeAutor .
                FILTER (xsd:integer(str(?an)) > {anMinim})
            }}";

        var requestUrl = $"{_server3Rdf}?query={Uri.EscapeDataString(sparqlQuery)}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("Accept", "application/sparql-results+json");
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(content);
    }

    private string ToBookId(string title)
    {
        return title.Replace(" ", "_").Replace("'", "").Replace(".", "");
    }

    private string ToAuthorId(string authorName)
    {
        return authorName.Replace(" ", "").Replace(".", "");
    }

    public async Task<bool> S3InsertBookAsync(string title, int year, int pages, string authorName)
    {
        string bookId = ToBookId(title);
        string authorId = ToAuthorId(authorName);

        string sparqlUpdate = $@"
            PREFIX s: <https://schema.org/>
            PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
            PREFIX : <https://github.com/andrea-muresan#>

            INSERT DATA {{
                :{bookId} a s:Book ;
                    s:name ""{title}"" ;
                    s:copyrightYear ""{year}""^^xsd:gYear ;
                    s:numberOfPages {pages} ;
                    s:author :{authorId} .
                    
                :{authorId} a s:Person ;
                    s:name ""{authorName}"" .
            }}";

        var httpContent = new StringContent(sparqlUpdate, Encoding.UTF8, "application/sparql-update");
        var response = await _httpClient.PostAsync($"{_server3Rdf}/statements", httpContent);
        
        return response.IsSuccessStatusCode;
    }
}
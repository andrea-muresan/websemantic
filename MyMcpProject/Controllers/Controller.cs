using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using MyMcpProject.Models;
using MyMcpProject.Services;

namespace MyMcpProject.Controllers;

[ApiController]
[Route("api/")]
public class Controller : ControllerBase
{
    private readonly Service _service;

    public Controller(Service srv)
    {
        _service = srv;
    }

    [HttpPost]
    public async Task<IActionResult> HandleMcpRequest([FromBody] UserRequest req)
    {
        var tools = new object[]
        {
            new { 
                name = "s1_get_reports_by_type", 
                description = "Filters crime reports by crime type", 
                parameters = new { 
                    type = "OBJECT", 
                    properties = new Dictionary<string, object> { 
                        { "crime_type", new { type = "STRING" } } 
                    }, 
                    required = new[] { "crime_type" } 
                } 
            },
            new { 
                name = "s1_create_evidence", 
                description = "Creates an evidence for a crime report.", 
                parameters = new { 
                    type = "OBJECT", 
                    properties = new Dictionary<string, object> { 
                        { "reportId", new { type = "INTEGER" } }, 
                        { "evidenceType", new { type = "STRING" } }, 
                        { "description", new { type = "STRING" } } 
                    }, 
                    required = new[] { "reportId", "evidenceType", "description" } 
                } 
            },
            new { 
                name = "s2_get_old_shows", 
                description = "Retrieves TV shows filtered by a maximum release year. Mentions the characters in the show", 
                parameters = new { 
                    type = "OBJECT", 
                    properties = new Dictionary<string, object> { 
                        { "year_lte", new { type = "INTEGER" } } 
                    }, 
                    required = new[] { "year_lte" } 
                } 
            },
            new { 
                name = "s2_create_show", 
                description = "Adds a new TV show.", 
                parameters = new { 
                    type = "OBJECT", 
                    properties = new Dictionary<string, object> { 
                        { "title", new { type = "STRING" } }, 
                        { "creator", new { type = "STRING" } }, 
                        { "year", new { type = "INTEGER" } } 
                    }, 
                    required = new[] { "title", "creator", "year" } 
                } 
            },
            new { 
                name = "s3_get_books_after_year", 
                description = "Retrieves all books published after a given year.", 
                parameters = new { 
                    type = "OBJECT", 
                    properties = new Dictionary<string, object> { 
                        { "year", new { type = "INTEGER" } } 
                    }, 
                    required = new[] { "year" } 
                } 
            },
            new { 
                name = "s3_insert_book", 
                description = "Adds a new book.", 
                parameters = new { 
                    type = "OBJECT", 
                    properties = new Dictionary<string, object> { 
                        { "title", new { type = "STRING" } }, 
                        { "year", new { type = "INTEGER" } }, 
                        { "pages", new { type = "INTEGER" } }, 
                        { "authorId", new { type = "STRING" } } 
                    }, 
                    required = new[] { "title", "year", "pages", "authorId" } 
                } 
            }
        };

        var initialPayload = new
        {
            contents = new object[] { 
                new { role = "user", parts = new object[] { new { text = req.Text } } } 
            },
            tools = new object[] { 
                new { functionDeclarations = tools } 
            }
        };

        var initialResponse = await _service.CallGeminiAsync(initialPayload);
        if (initialResponse == null) return Problem("Gemini API Error");

        var functionCall = initialResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["functionCall"];

        if (functionCall != null)
        {
            string name = functionCall["name"]!.GetValue<string>();
            var args = functionCall["args"];
            JsonNode? result = null;

            if (name == "s1_get_reports_by_type") 
            {
                result = await _service.S1GetReportsByTypeAsync(args?["crime_type"]?.GetValue<string>() ?? "Murder");
            }
            else if (name == "s1_create_evidence") 
            {
                result = await _service.S1CreateEvidenceAsync(
                    args?["reportId"]!.GetValue<int>() ?? 1, 
                    args!["evidenceType"]!.GetValue<string>(), 
                    args!["description"]!.GetValue<string>()
                );
            }
            else if (name == "s2_get_old_shows") 
            {
                result = await _service.S2GetOldShowsAsync(args?["year_lte"]?.GetValue<int>() ?? 1980);
            }
            else if (name == "s2_create_show") 
            {
                result = await _service.S2CreateShowAsync(
                    args!["title"]!.GetValue<string>(), 
                    args!["creator"]!.GetValue<string>(), 
                    args!["year"]!.GetValue<int>()
                );
            }
            else if (name == "s3_get_books_after_year") 
            {
                result = await _service.S3GetBooksAfterYearAsync(args?["year"]!.GetValue<int>() ?? 2020);
            }
            else if (name == "s3_insert_book")
            {
                bool success = await _service.S3InsertBookAsync(
                    args!["title"]!.GetValue<string>(), 
                    args!["year"]!.GetValue<int>(), 
                    args!["pages"]!.GetValue<int>(), 
                    args!["authorId"]!.GetValue<string>()
                );
                result = JsonNode.Parse(success ? "{\"status\": \"Success\"}" : "{\"status\": \"Failed\"}");
            }

            var instruction = "You are a helpful assistant. Provide answers in natural, conversational English. " +
                             "Never output raw JSON or technical data structures. Do not mention the IDs added" +
                             "Present all findings into fluid, readable, short paragraphs. " +
                             "Do not use any markdown formatting like bold, italics, or bullet points. " +
                             "Write plain text only.";

            var followUpPayload = new
            {
                system_instruction = new { 
                    parts = new object[] { new { text = instruction } } 
                },
                contents = new object[]
                {
                    new { role = "user", parts = new object[] { new { text = req.Text } } },
                    new { role = "model", parts = new object[] { new { functionCall = new { name = name, args = args } } } },
                    new { role = "user", parts = new object[] { new { functionResponse = new { name = name, response = new { result = result } } } } }
                }
            };

            var finalResponse = await _service.CallGeminiAsync(followUpPayload);
            string reply = finalResponse?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? string.Empty;

            return Ok(new { reply = reply });
        }

        string textReply = initialResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? string.Empty;
        return Ok(new { reply = textReply });
    }
}
//using System;
//using System.Threading.Tasks;

//class Program
//{
//    static async Task Main()
//    {
//        await foreach (var chunk in GenerateResponseStreamAsync())
//        {
//            Console.Write(chunk); // Print each chunk as it arrives
//        }

//        Console.WriteLine(); // Done
//    }

//    static async IAsyncEnumerable<string> GenerateResponseStreamAsync()
//    {
//        string[] parts = new[]
//        {
//            "This ", "response ", "is ", "streamed ", "like ", "ChatGPT.\n"
//        };

//        foreach (var part in parts)
//        {
//            await Task.Delay(500); // Simulate processing time
//            yield return part;
//        }
//    }
//}










using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static string apiKey;
    static string model; // or "gpt-3.5-turbo"

    private static readonly HttpClient httpClient = new HttpClient();

    // Keeps chat history
    private static readonly List<Dictionary<string, string>> messages = new List<Dictionary<string, string>>();
    static IConfiguration Configuration;

    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", optional: false);

        Configuration = builder.Build();

        apiKey = Configuration["OpenAI:ApiKey"];
        model = Configuration["OpenAI:Model"];

        Console.WriteLine($"Model: {model}");

        Console.WriteLine("ChatGPT (C# Console) - Type 'exit' to quit\n");

        while (true)
        {
            Console.Write("You: ");
            string userInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (userInput.ToLower() == "exit") break;

            messages.Add(new Dictionary<string, string>
            {
                { "role", "user" },
                { "content", userInput }
            });

            Console.Write("Assistant: ");
            await StreamAssistantResponse();

            Console.WriteLine(); // for formatting
        }
    }

    private static async Task StreamAssistantResponse()
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model = model,
            messages = messages,
            stream = true
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = content
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"\n[Error: {response.StatusCode}]");
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var assistantMessage = new StringBuilder();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var jsonData = line.Substring("data: ".Length);

            if (jsonData == "[DONE]") break;

            try
            {
                var parsed = JObject.Parse(jsonData);
                var contentPiece = parsed["choices"]?[0]?["delta"]?["content"]?.ToString();

                if (!string.IsNullOrEmpty(contentPiece))
                {
                    Console.Write(contentPiece);
                    assistantMessage.Append(contentPiece);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error parsing chunk] {ex.Message}");
            }
        }

        // Add assistant reply to the message history
        messages.Add(new Dictionary<string, string>
        {
            { "role", "assistant" },
            { "content", assistantMessage.ToString() }
        });
    }
}

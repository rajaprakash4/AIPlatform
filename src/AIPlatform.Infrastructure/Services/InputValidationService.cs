using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using System.Text.Json;

namespace AIPlatform.Infrastructure.Services;

public class InputValidationService
{
    private readonly IAIServiceFactory _aiFactory;

    public InputValidationService(IAIServiceFactory aiFactory)
    {
        _aiFactory = aiFactory;
    }

    public async Task<ValidationResult> ValidateInputAsync(string toolId, string inputKey, string inputValue, string fieldDescription)
    {
        if (toolId.Contains("RAG", StringComparison.OrdinalIgnoreCase) ||
        inputKey.Equals("Query", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(inputValue))
            {
                return new ValidationResult { IsValid = false, Error = "Search query cannot be empty." };
            }
            return new ValidationResult { IsValid = true };
        }

        if (inputKey.Equals("StoreName", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult { IsValid = true };
        }
        // We ask Gemini to act as a "Data Quality Judge"
        var prompt = $@"
You are a Data Validation Engine.
Context:
- Tool ID: {toolId}
- Field Name: {inputKey}
- Field Description: {fieldDescription}
- User Input: ""{inputValue}""

Task:
Determine if the User Input is valid and appropriate for this field.
1. If valid, return {{ ""isValid"": true }}.
2. If invalid (wrong format, nonsense, or ambiguous), return {{ ""isValid"": false, ""error"": ""Friendly error message explaining why"" }}.

Examples:
- Field: Email, Input: ""bob"" -> isValid: false, error: ""Please enter a valid email address.""
- Field: EmployeeId, Input: ""105"" -> isValid: true

Return ONLY JSON.";

        var aiService = _aiFactory.GetDefaultService();
        var response = await aiService.GenerateResponseAsync(new StandardChatRequest { UserMessage = prompt });

        try
        {
            var cleanJson = response.Content.Replace("```json", "").Replace("```", "").Trim();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<ValidationResult>(cleanJson, options);
        }
        catch
        {
            // Fallback: If AI fails, assume valid to prevent blocking, or strict fail.
            // Let's assume valid but log warning.
            return new ValidationResult { IsValid = true };
        }
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
}
using System.Text.Json;
using Kamada.Modules.Assistant.Domain;
using Microsoft.Extensions.AI;

namespace Kamada.Modules.Assistant.Infrastructure.Ollama;

/// <summary>
/// Instructions, output schema and few-shot examples for the extraction model. The prompt is in English (small
/// models follow English instructions best); the examples show both Polish and English input.
/// </summary>
internal static class ExtractionPrompt
{
    public const string System = """
        You extract a delivery order from a note dictated or typed by an employee of a Polish online shop.
        The note is in Polish or English and may come from speech recognition (numbers as words, no punctuation).
        Answer with JSON that matches the schema. Rules:
        - Extract only what is said. Never guess or invent anything (no e-mail, phone, postal code or product that was
          not said). Use null for anything not said.
        - client.name and client.surname: the customer's first name and surname in the nominative (base) form,
          e.g. "dla Jana Kowalskiego" -> "Jan", "Kowalski"; "pani Annie Nowak" -> "Anna", "Nowak".
        - client.phone: digits only, e.g. "600 100 200" or "sześćset sto dwieście" -> "600100200".
        - client.email: as said, lower case; spoken "małpa" or "at" means "@", "kropka" or "dot" means ".".
        - address.street: the street name without "ul.", "ulica" or "street", in the nominative form
          ("na Grunwaldzkiej" -> "Grunwaldzka"). Some street names start with a number ("10 Lutego", "3 Maja",
          "11 Listopada"): "10 Lutego 20" -> street "10 Lutego", buildingNumber "20".
        - address.buildingNumber and address.apartmentNumber: digits (and a letter if said): "piętnaście a" -> "15A";
          "mieszkania 4", "m. 4", "lokal 4", "/4", "flat 4", "apartment 4" -> apartmentNumber "4".
        - address.city: nominative form ("w Gdańsku" -> "Gdańsk"). address.postalCode: only when said, as "00-000".
        - items: one entry per product. product = the product name in Polish, singular, nominative, as a shop would
          list it, keeping size or variant words that were said ("sześć wód niegazowanych półtora litra" ->
          "woda niegazowana 1,5 l"; English "three apple juices" -> "sok jabłkowy"). quantity = how many, as an
          integer; 1 when no quantity is said ("dozen" = 12, "pół tuzina" = 6).
        - notes: remarks for the courier or about the delivery ("proszę dzwonić domofonem", "after 5 pm"), otherwise
          null. Never put client, address or product data in notes.
        """;

    /// <summary>Structured output schema (Ollama <c>format</c>): the model can only produce this shape.</summary>
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "client": {
              "type": "object",
              "properties": {
                "name": { "type": ["string", "null"] },
                "surname": { "type": ["string", "null"] },
                "phone": { "type": ["string", "null"] },
                "email": { "type": ["string", "null"] }
              },
              "required": ["name", "surname", "phone", "email"]
            },
            "address": {
              "type": "object",
              "properties": {
                "street": { "type": ["string", "null"] },
                "buildingNumber": { "type": ["string", "null"] },
                "apartmentNumber": { "type": ["string", "null"] },
                "city": { "type": ["string", "null"] },
                "postalCode": { "type": ["string", "null"] }
              },
              "required": ["street", "buildingNumber", "apartmentNumber", "city", "postalCode"]
            },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "product": { "type": "string" },
                  "quantity": { "type": "integer" }
                },
                "required": ["product", "quantity"]
              }
            },
            "notes": { "type": ["string", "null"] }
          },
          "required": ["client", "address", "items", "notes"]
        }
        """;

    private static readonly (string Input, string Output)[] Examples =
    [
        (
            "Zamówienie dla pani Katarzyny Wiśniewskiej, ulica Długa siedem mieszkania dwanaście, Sopot, kod 81-701, telefon 512 345 678. Trzy soki pomarańczowe i jedna kawa ziarnista. Proszę dzwonić przed przyjazdem.",
            """{"client":{"name":"Katarzyna","surname":"Wiśniewska","phone":"512345678","email":null},"address":{"street":"Długa","buildingNumber":"7","apartmentNumber":"12","city":"Sopot","postalCode":"81-701"},"items":[{"product":"sok pomarańczowy","quantity":3},{"product":"kawa ziarnista","quantity":1}],"notes":"Proszę dzwonić przed przyjazdem."}"""
        ),
        (
            "dla Marka Zielińskiego mail marek.zielinski małpa example kropka com dziesięć mat do ćwiczeń",
            """{"client":{"name":"Marek","surname":"Zieliński","phone":null,"email":"marek.zielinski@example.com"},"address":{"street":null,"buildingNumber":null,"apartmentNumber":null,"city":null,"postalCode":null},"items":[{"product":"mata do ćwiczeń","quantity":10}],"notes":null}"""
        ),
        (
            "Order for Tom Baker, 3 Main Street flat 2, Gdynia, phone 501 222 333: two packs of ground coffee and a garden hose, leave it at the door",
            """{"client":{"name":"Tom","surname":"Baker","phone":"501222333","email":null},"address":{"street":"Main Street","buildingNumber":"3","apartmentNumber":"2","city":"Gdynia","postalCode":null},"items":[{"product":"kawa mielona","quantity":2},{"product":"wąż ogrodowy","quantity":1}],"notes":"Leave it at the door."}"""
        ),
    ];

    public static ChatResponseFormat ResponseFormat { get; } =
        ChatResponseFormat.ForJsonSchema(JsonDocument.Parse(JsonSchema).RootElement.Clone(), "order", "Order mentioned in the note");

    public static IReadOnlyList<ChatMessage> Messages(string text, AssistantLanguage language)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, System) };
        foreach (var (input, output) in Examples)
        {
            messages.Add(new ChatMessage(ChatRole.User, input));
            messages.Add(new ChatMessage(ChatRole.Assistant, output));
        }

        messages.Add(new ChatMessage(ChatRole.User, language switch
        {
            AssistantLanguage.Polish => "(Polish note)\n" + text,
            AssistantLanguage.English => "(English note)\n" + text,
            _ => text,
        }));
        return messages;
    }
}

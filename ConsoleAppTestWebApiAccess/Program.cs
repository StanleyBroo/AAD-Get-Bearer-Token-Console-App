using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TextCopy;
namespace ConsoleAppTestWebApiAccess
{
    internal class Program
    {
        [STAThread] 
        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("secrets.json", optional: true, reloadOnChange: true);


            var configuration = builder.Build();
            var token_tenant_endpoint = configuration["GetTokenTenantIdEndpoint"] ?? "";
            var token_clientId = configuration["GetTokenClientId"] ?? "";
            var token_clientSecret = configuration["GetTokenClientSecret"] ?? "";
            var token_resource = configuration["GetTokenResource"] ?? "";

            var token = "";

            var apiBaseAddress = configuration["CallApiBaseAddress"] ?? "";
            var apiControllerName = configuration["CallApiControllerName"] ?? "";

            Console.WriteLine($"GetTokenTenantID_endpoint: {token_tenant_endpoint}");
            Console.WriteLine($"GetTokenClientId: {token_clientId}");
            Console.WriteLine($"GetTokenClientSecret: {token_clientSecret}");
            Console.WriteLine($"GetTokenResource: {token_resource}");

            Console.WriteLine("");
            Console.WriteLine($"CallApiBaseAddress: {apiBaseAddress}");
            Console.WriteLine($"CallApiControllerName: {apiControllerName}");
            Console.WriteLine("");
            Console.WriteLine("Applikationen startad. Använd följande kommandon:");
            Console.WriteLine("'T' - Hämta access_token");
            Console.WriteLine("'C' - Kopiera access_token till urklipp");
            Console.WriteLine("'L' - Kontrollera tid kvar på token");
            Console.WriteLine($"'G' - Testa Get() {apiControllerName} fråga mot API");
            Console.WriteLine("'E' - Avsluta applikationen");
           


            while (true) // En oändlig loop som fortsätter tills användaren väljer att avsluta
            {
                Console.WriteLine("");
                Console.Write("Ange kommando: ");
               
                var line = Console.ReadLine();
                Console.WriteLine(""); // För att göra en ny rad efter användarens input
                Console.WriteLine("");
                var key = line?.FirstOrDefault() ?? ' ';
                if (string.IsNullOrEmpty(line) || line.Length > 1)
                {
                    Console.WriteLine("Ogiltigt kommando. Försök igen med endast ett tecken.");
                }
                else
                {
                    switch (key)
                    {
                        case 'T':
                            // Hämta token-logik
                            token = await GetAccessTokenAsync(token_tenant_endpoint, token_clientId, token_clientSecret, token_resource);
                            Console.WriteLine($"Bearer: {token}");
                            break;
                        case 'C':
                            // Kopiera token till urklipp
                            // Se till att ClipboardService.SetTextAsync finns implementerad korrekt
                            if (!string.IsNullOrEmpty(token))
                            {
                                await ClipboardService.SetTextAsync(token);
                                Console.WriteLine("Token kopierad till urklipp.");
                            }
                            else
                            {
                                Console.WriteLine("Ingen token att kopiera.");
                            }
                            break;
                        case 'L':
                            // Kontrollera tid kvar på token
                            if (!string.IsNullOrEmpty(token))
                            {
                                CheckTimeLeftOnToken(token);
                            }
                            else
                            {
                                Console.WriteLine("Ingen token att kontrollera.");
                            }
                            break;
                        case 'G':
                            // Testa fråga mot API
                            if (!string.IsNullOrEmpty(token))
                            {
                                await MakeApiCallWithToken(token, apiBaseAddress, apiControllerName);
                            }
                            else
                            {
                                Console.WriteLine("Ingen token att testa.");
                            }
                            break;
                        case 'E':
                            // Avsluta applikationen
                            Console.WriteLine("Avslutar applikationen...");
                            return; // Avslutar Main och därmed programmet
                        default:
                            // Hanterar ogiltiga kommandon
                            Console.WriteLine("Ogiltigt kommando.");
                            break;
                    }
                }
            }
        }

        private static void CheckTimeLeftOnToken(string token)
        {
            var minutesLeft = TokenExpiresInMinutes(token);
            if (minutesLeft <= 0)
            {
                Console.WriteLine("Token har gått ut.");
            }
            else
            {
                Console.WriteLine($"Token är fortfarande giltig i {minutesLeft} minuter");
            }
        }

        private static async Task<string> GetAccessTokenAsync(string endpoint, string clientId, string clientSecret, string resource)
        {
            var tokenTenantID_Endpoint = endpoint;
            var client = new HttpClient();

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = resource

            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenTenantID_Endpoint)
            {
                Content = new FormUrlEncodedContent(requestBody)
            };

            HttpResponseMessage response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
               
                if (json != null && json.Contains("access_token"))
                {
                    var jObject = JObject.Parse(json);

                    // Plocka ut 'access_token'
                    var accessToken = jObject["access_token"]?.ToString() ?? "n/a";

                    return accessToken;

                }
            }

            throw new ApplicationException("Unable to retrieve access token.");
        }

        static async Task MakeApiCallWithToken(string token,string apibaseaddress,string apicontrollername)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(apibaseaddress);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                // Skicka GET-förfrågan till API-endpunkten
                var response = await client.GetAsync(apicontrollername);
                response.EnsureSuccessStatusCode(); // Kasta ett undantag om inte ett framgångsrikt svar

                // Läs svaret som en sträng
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException!");
                Console.WriteLine($"Request exception: {e.Message}");
            }
        }



        public static DateTime? GetTokenExpiryDate(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken == null) return null;

            var expClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "exp");
            if (expClaim == null) return null;

            var expiryDateUnix = long.Parse(expClaim.Value);
            var expiryDate = DateTimeOffset.FromUnixTimeSeconds(expiryDateUnix).DateTime;

            return expiryDate;
        }

        public static int TokenExpiresInMinutes(string token)
        {
            var expiryDate = GetTokenExpiryDate(token);
            if (!expiryDate.HasValue) 
                return 0; // Anta att token är ogiltig om vi inte kan få ett utgångsdatum

            return expiryDate.Value.Subtract( DateTime.UtcNow).Minutes;
        }

       
    }
}


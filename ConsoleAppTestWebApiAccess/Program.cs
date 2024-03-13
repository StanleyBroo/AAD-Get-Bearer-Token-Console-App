using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
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
             .AddJsonFile("secrets.json", optional: true, reloadOnChange: true)
             .AddUserSecrets<Program>();


            var configuration = builder.Build();

            var tenantId = configuration["TenantId"];
            var clientId = configuration["ClientId"];
            var clientSecret = configuration["ClientSecret"];
            var scope = "https://graph.microsoft.com/.default";
            var token = "";

            Console.WriteLine($"TenantId: {tenantId}");
            Console.WriteLine($"ClientId: {clientId}");
            Console.WriteLine($"ClientSecret: {clientSecret}");
            Console.WriteLine($"Scope: {scope}");
            Console.WriteLine("");

            Console.WriteLine("Tryck F för att hämta token...");
            if (Console.ReadKey().Key == ConsoleKey.F)
            {
                Console.WriteLine(" ");
                Console.WriteLine(" ");
                token = await GetAccessTokenAsync(tenantId, clientId, clientSecret, scope);
                Console.WriteLine($"Access Token: {token}");
                Console.WriteLine(" ");
                Console.WriteLine(" ");
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
            Console.WriteLine(" ");
            Console.WriteLine(" ");
            Console.WriteLine("Tryck 'C' för att kopiera access_token till urklipp.");
            if (Console.ReadKey().Key == ConsoleKey.C)
            {
                // Windows Forms Clipboard.SetDataObject används här som ett exempel
                await ClipboardService.SetTextAsync(token);
                Console.WriteLine("\nToken kopierad till urklipp.");
            }
            Console.WriteLine(" ");
            Console.WriteLine(" ");
            Console.WriteLine("Tryck 'ESC' för att avsluta");
            if (Console.ReadKey().Key == ConsoleKey.Escape)
            {
                Environment.Exit(0);
            }
        }

        private static async Task<string> GetAccessTokenAsync(string tenantId, string clientId, string clientSecret, string scope)
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
            var client = new HttpClient();

            var requestBody = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = scope,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
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


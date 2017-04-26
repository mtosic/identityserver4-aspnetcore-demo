using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Globalization;

namespace Client.MVCHybrid.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult About()
        {
            ViewData["Message"] = "About secure page - hybrid flow";

            return View();
        }

        [Authorize]
        public async Task<IActionResult> CallApi()
        {
            var accessToken = await HttpContext.Authentication.GetTokenAsync("access_token");

            var client = new HttpClient();
            client.SetBearerToken(accessToken);
            var content = await client.GetStringAsync("http://localhost:5001/identity");

            ViewBag.Json = JArray.Parse(content).ToString();
            return View();
        }

        public async Task<IActionResult> RenewTokens()
        {
            var disco = await DiscoveryClient.GetAsync("http://localhost:5000");
            if (disco.IsError) throw new Exception(disco.Error);

            var tokenClient = new TokenClient(disco.TokenEndpoint, "mvc.hybrid", "secret");
            var rt = await HttpContext.Authentication.GetTokenAsync("refresh_token");
            var tokenResult = await tokenClient.RequestRefreshTokenAsync(rt);

            if (!tokenResult.IsError)
            {
                var old_id_token = await HttpContext.Authentication.GetTokenAsync("id_token");
                var new_access_token = tokenResult.AccessToken;
                var new_refresh_token = tokenResult.RefreshToken;

                var tokens = new List<AuthenticationToken>();
                tokens.Add(new AuthenticationToken { Name = OpenIdConnectParameterNames.IdToken, Value = old_id_token });
                tokens.Add(new AuthenticationToken { Name = OpenIdConnectParameterNames.AccessToken, Value = new_access_token });
                tokens.Add(new AuthenticationToken { Name = OpenIdConnectParameterNames.RefreshToken, Value = new_refresh_token });

                var expiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(tokenResult.ExpiresIn);
                tokens.Add(new AuthenticationToken { Name = "expires_at", Value = expiresAt.ToString("o", CultureInfo.InvariantCulture) });

                var info = await HttpContext.Authentication.GetAuthenticateInfoAsync("Cookies");
                info.Properties.StoreTokens(tokens);
                await HttpContext.Authentication.SignInAsync("Cookies", info.Principal, info.Properties);

                return Redirect("~/Home/About");
            }

            ViewData["Error"] = tokenResult.Error;
            return View("Error");
        }


        public IActionResult Error()
        {
            return View();
        }
    }
}

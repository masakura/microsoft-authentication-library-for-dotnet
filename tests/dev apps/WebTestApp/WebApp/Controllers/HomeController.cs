﻿//----------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using WebApp.Utils;

namespace WebApp.Controllers
{
    public class HomeController : Controller
    {
        private const string MsGraphMeQuery = "https://graph.microsoft.com/v1.0/me";
        private const string MsGraphUsersQuery = "https://graph.microsoft.com/v1.0/users";

        private const string MsGraphDefaultScope = "https://graph.microsoft.com/.default";
        private const string MsGraphUsersScope = "User.Read.All";


        private const string AdminConsentUrlFormat =
            "https://login.microsoftonline.com/{0}/adminconsent?client_id={1}&redirect_uri={2}";

        private const string WebApiUserProfileQuery = "https://localhost:44351/api/UserProfile";

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Error(string message)
        {
            ViewBag.Message = message;
            return View("~/Views/Shared/Error.cshtml");
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CallGraphMeQuery()
        {
            var userName = User.FindFirst("preferred_username")?.Value;

            string result;
            try
            {
                var authenticationResult = await ConfidentialClientUtils.AcquireTokenSilentAsync(Startup.Scopes,
                    userName,
                    HttpContext.Session, ConfidentialClientUtils.CreateSecretClientCredential(), GetCurrentUserId());

                result = await CallApi(MsGraphMeQuery, authenticationResult.AccessToken);
            }
            catch (MsalException ex)
            {
                result = "WebApp failed to call GraphMeQuery, MsalException - " + ex.Message;
            }
            catch (Exception ex)
            {
                result = "WebApp failed to call GraphMeQuery, Exception - " + ex.Message;
            }

            return View("~/Views/Home/Index.cshtml", result);
        }

        [Authorize]
        [HttpGet]
        public ActionResult RequestApplicationPermissions()
        {
            return new RedirectResult(
                string.Format(AdminConsentUrlFormat,
                    Startup.Configuration["AzureAd:Tenant"],
                    Startup.Configuration["AzureAd:ClientId"],
                    Startup.Configuration["AzureAd:AdminConsentRedirectUri"]
                ));
        }

        [Authorize]
        [HttpGet]
        public ActionResult AdminConsent(string admin_consent, string tenant, string error, string error_description)
        {
            // If the admin successfully granted permissions, continue to showing the list of users
            if (admin_consent == "True")
            {
                return View("~/Views/Home/Index.cshtml", "admin successfully granted permissions");
            }
            else
            {
                return View("~/Views/Home/Index.cshtml",
                    "failed to grant permissions, error_description - " + error_description);
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CallGraphUsersQueryBySecretClientCredential()
        {
            return await CallGraphUsersQuery(ConfidentialClientUtils.CreateSecretClientCredential());
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CallGraphUsersQueryByCertClientCredential()
        {
            return await CallGraphUsersQuery(ConfidentialClientUtils.CreateClientCertificateCredential());
        }

        private async Task<IActionResult> CallGraphUsersQuery(ClientCredential clientCredential)
        {
            string result;
            try
            {
                var authenticationResult =
                    await ConfidentialClientUtils.AcquireTokenForClientAsync(new[] { MsGraphDefaultScope }, HttpContext.Session,
                        clientCredential,
                        GetCurrentUserId());

                result = await CallApi(MsGraphUsersQuery, authenticationResult.AccessToken);
            }
            catch (MsalException ex)
            {
                result = "WebApp failed to call GraphUsersQuery, MsalException - " + ex.Message;
            }
            catch (Exception ex)
            {
                result = "WebApp failed to call GraphUsersQuery, Exception - " + ex.Message;
            }

            return View("~/Views/Home/Index.cshtml", result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CallWebApiUserProfileQuery()
        {
            string result;
            try
            {
                var userName = User.FindFirst("preferred_username")?.Value;

                var authenticationResult = await ConfidentialClientUtils.AcquireTokenSilentAsync(
                    new[] { Startup.WebApiScope }, userName,
                    HttpContext.Session, ConfidentialClientUtils.CreateSecretClientCredential(), GetCurrentUserId());

                result = await CallApi(WebApiUserProfileQuery, authenticationResult.AccessToken);
            }
            catch (MsalException ex)
            {
                result = "WebApp failed to call WebApiUserProfileQuery, MsalException - " + ex.Message;
            }
            catch (Exception ex)
            {
                result = "WebApp failed to call WebApiUserProfileQuery, Exception - " + ex.Message;
            }

            return View("~/Views/Home/Index.cshtml", result);
        }

        private static async Task<string> CallApi(string apiUrl, string accessToken)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception(response.StatusCode.ToString());

            return await response.Content.ReadAsStringAsync();
        }
    }
}

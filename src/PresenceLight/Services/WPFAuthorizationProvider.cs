﻿using MSGraph = Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows;
using System.Net.Http.Headers;
using PresenceLight.Telemetry;

namespace PresenceLight.Core.Helpers
{
    public class WPFAuthorizationProvider : MSGraph.IAuthenticationProvider
    {
        public static IPublicClientApplication Application;
        private readonly List<string> _scopes;

        public WPFAuthorizationProvider(IPublicClientApplication application, List<string> scopes)
        {
            Application = application;
            _scopes = scopes;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            AuthenticationResult authResult = null;

            var accounts = await Application.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            try
            {
                authResult = await Application.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                .ExecuteAsync();

            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    await System.Windows.Application.Current.Dispatcher.Invoke<Task>(async () =>
                     {
                         authResult = await Application.AcquireTokenInteractive(_scopes)
                            .WithUseEmbeddedWebView(false)
                            .ExecuteAsync();
                     });
                }
                catch 
                {

                }
            }

            if (authResult != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", authResult.AccessToken);
            }
        }
    }
}

﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using LifxCloud.NET.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

using PresenceLight.Core;

namespace PresenceLight.Worker
{
    public class Worker : BackgroundService
    {
        private readonly BaseConfig Config;
        private readonly IHueService _hueService;
        private readonly AppState _appState;
        private readonly ILogger<Worker> _logger;
        private LIFXService _lifxService;
        private ICustomApiService _customApiService;
        private GraphServiceClient c;
        private IWorkingHoursService _workingHoursService;

        public Worker(IHueService hueService,
                      ILogger<Worker> logger,
                      IOptionsMonitor<BaseConfig> optionsAccessor,
                      AppState appState,
                      LIFXService lifxService,
                      IWorkingHoursService workingHoursService,
                      ICustomApiService customApiService)
        {
            Config = optionsAccessor.CurrentValue;
            _workingHoursService = workingHoursService;
            _hueService = hueService;
            _lifxService = lifxService;
            _customApiService = customApiService;
            _logger = logger;
            _appState = appState;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_appState.IsUserAuthenticated)
                {
                    c = _appState.GraphServiceClient;
                    _logger.LogInformation("User is Authenticated, starting worker");
                    try
                    {
                        await GetData(stoppingToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Exception occured restarting worker");
                    }
                }
                else
                {
                    _logger.LogInformation("User is Not Authenticated, restarting worker");
                }
                await Task.Delay(1000, stoppingToken);
            }
        }


        private async Task GetData(CancellationToken cancellationToken)
        {

            try
            {

                var user = await GetUserInformation(cancellationToken);

                var photo = await GetPhotoAsBase64Async(cancellationToken);

                var presence = await GetPresence(cancellationToken);

                //Attach properties to all logging within this context..
                using (Serilog.Context.LogContext.PushProperty("Availability", presence.Availability))
                using (Serilog.Context.LogContext.PushProperty("Activity", presence.Activity))
                {
                    _appState.SetUserInfo(user, photo, presence);

                    if (!string.IsNullOrEmpty(Config.LightSettings.Hue.HueApiKey) && !string.IsNullOrEmpty(Config.LightSettings.Hue.HueIpAddress) && !string.IsNullOrEmpty(Config.LightSettings.Hue.SelectedHueLightId))
                    {
                        await _hueService.SetColor(presence.Availability, Config.LightSettings.Hue.SelectedHueLightId);
                    }

                    if (Config.LightSettings.LIFX.IsLIFXEnabled && !string.IsNullOrEmpty(Config.LightSettings.LIFX.LIFXApiKey))
                    {
                        await _lifxService.SetColor(presence.Availability, Config.LightSettings.LIFX.SelectedLIFXItemId);


                        _logger.LogInformation($"Setting LIFX Light: { Config.LightSettings.Hue.SelectedHueLightId}, Graph Presence: {presence.Availability}");
                    }

                    while (_appState.IsUserAuthenticated)
                    {
                        if (_appState.LightMode == "Graph")
                        {
                            presence = await GetPresence(cancellationToken);

                            _appState.SetPresence(presence);

                            _logger.LogInformation($"Presence is {presence.Availability}");

                            if (!string.IsNullOrEmpty(Config.LightSettings.Hue.HueApiKey) && !string.IsNullOrEmpty(Config.LightSettings.Hue.HueIpAddress) && !string.IsNullOrEmpty(Config.LightSettings.Hue.SelectedHueLightId))
                            {
                                await _hueService.SetColor(presence.Availability, Config.LightSettings.Hue.SelectedHueLightId);
                            }

                            if (Config.LightSettings.LIFX.IsLIFXEnabled && !string.IsNullOrEmpty(Config.LightSettings.LIFX.LIFXApiKey))
                            {
                                await _lifxService.SetColor(presence.Availability, Config.LightSettings.LIFX.SelectedLIFXItemId);
                                _logger.LogInformation($"Setting LIFX Light: { Config.LightSettings.LIFX.SelectedLIFXItemId}, Graph Presence: {presence.Availability}");
                            }
                            if (Config.LightSettings.Custom.IsCustomApiEnabled)
                            {
                                // passing the data on only when it changed is handled within the custom api service
                                await _customApiService.SetColor(presence.Availability, presence.Activity, cancellationToken);
                            }
                        }
                        Thread.Sleep(Convert.ToInt32(Config.LightSettings.PollingInterval * 1000));
                    }

                    _logger.LogInformation("User logged out, no longer polling for presence.");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception occured in running worker");
                throw;
            }

        }

        public async Task<User> GetUserInformation(CancellationToken cancellationToken)
        {
            try
            {
                var me = await c.Me.Request().GetAsync(cancellationToken);
                _logger.LogInformation($"User is {me.DisplayName}");
                return me;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting me");
                throw;
            }
        }

        public async Task<string> GetPhotoAsBase64Async(CancellationToken cancellationToken)
        {
            try
            {
                var photoStream = await c.Me.Photo.Content.Request().GetAsync(cancellationToken);
                var memoryStream = new MemoryStream();
                photoStream.CopyTo(memoryStream);

                var photoBytes = memoryStream.ToArray();
                var base64Photo = $"data:image/gif;base64,{Convert.ToBase64String(photoBytes)}";

                return base64Photo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting photo");
                throw;
            }
        }

        public async Task<Presence> GetPresence(CancellationToken cancellationToken)
        {
            try
            {
                var presence = await c.Me.Presence.Request().GetAsync(cancellationToken);

                var r = new Regex(@"
                (?<=[A-Z])(?=[A-Z][a-z]) |
                 (?<=[^A-Z])(?=[A-Z]) |
                 (?<=[A-Za-z])(?=[^A-Za-z])", RegexOptions.IgnorePatternWhitespace);

                _logger.LogInformation($"Presence is {presence.Availability}");
                return presence;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,"Exception getting presence");
                throw;
            }
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;

using LifxCloud.NET.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

using PresenceLight.Core;

namespace PresenceLight.Worker.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class AlexaController : ControllerBase
    {
        private readonly BaseConfig Config;
        private readonly IHueService _hueService;
        private LIFXService _lifxService;
        private ILogger _logger;
        private readonly AppState _appState;
        public AlexaController(IHueService hueService,
                      IOptionsMonitor<BaseConfig> optionsAccessor,
                      ILogger<AlexaController> logger,
                      AppState appState,
                      LIFXService lifxService)
        {
            Config = optionsAccessor.CurrentValue;
            _hueService = hueService;
            _lifxService = lifxService;
            _appState = appState;
            _logger = logger;
        }


        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult> ProcessAlexaRequest([FromBody] SkillRequest request)
        {
            using (Serilog.Context.LogContext.PushProperty("Request", JsonConvert.SerializeObject(request)))
            {
                var requestType = request.GetRequestType();

                _logger.LogDebug($"Beginning Alexa Request: {requestType.Name}");

                SkillResponse response = null;

                if (requestType == typeof(LaunchRequest))
                {
                    response = ResponseBuilder.Tell("Welcome to Presence Light!");
                    response.Response.ShouldEndSession = false;
                }
                else if (requestType == typeof(IntentRequest))
                {
                    var intentRequest = request.Request as IntentRequest;

                    if (intentRequest.Intent.Name == "Teams")
                    {
                        _appState.SetLightMode("Graph");
                        response = ResponseBuilder.Tell("Presence Light set to Teams!");
                    }
                    else if (intentRequest.Intent.Name == "Custom")
                    {
                        _appState.SetLightMode("Custom");
                        _appState.SetCustomColor("#FFFFFF");
                        response = ResponseBuilder.Tell("Presence Light set to custom!");
                    }

                if (_appState.LightMode == "Custom")
                {
                    if (!string.IsNullOrEmpty(Config.LightSettings.Hue.HueApiKey) && !string.IsNullOrEmpty(Config.LightSettings.Hue.HueIpAddress) && !string.IsNullOrEmpty(Config.LightSettings.Hue.SelectedItemId))
                    {
                        await _hueService.SetColor(_appState.CustomColor, "", Config.LightSettings.Hue.SelectedItemId);
                    }

                    if (Config.LightSettings.LIFX.IsEnabled && !string.IsNullOrEmpty(Config.LightSettings.LIFX.LIFXApiKey))
                    {
                        await _lifxService.SetColor(_appState.CustomColor, "", Config.LightSettings.LIFX.SelectedItemId);
                    }
                }
            }

                return new OkObjectResult(response);
            }
        }

    }
}

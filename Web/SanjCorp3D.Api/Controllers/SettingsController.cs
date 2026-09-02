using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanjCorp3D.Api.Contracts;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Services;

namespace SanjCorp3D.Api.Controllers;

[ApiController,Authorize,Route("api/settings")]
public sealed class SettingsController(BusinessSettingsService settings):ControllerBase
{
    [HttpGet]public async Task<IActionResult>Get(CancellationToken ct)=>Ok(await settings.GetAsync(ct));
    [Authorize(Roles=AppRoles.Administrator),HttpPut]public async Task<IActionResult>Update(BusinessSettingsDto input,CancellationToken ct){await settings.SaveAsync(input,ct);return Ok(await settings.GetAsync(ct));}
}

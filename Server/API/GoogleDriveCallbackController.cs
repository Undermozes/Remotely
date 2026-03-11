using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using System.Threading.Tasks;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class GoogleDriveCallbackController : ControllerBase
{
    private readonly IGoogleDriveService _googleDriveService;
    private readonly UserManager<RemotelyUser> _userManager;

    public GoogleDriveCallbackController(
        IGoogleDriveService googleDriveService,
        UserManager<RemotelyUser> userManager)
    {
        _googleDriveService = googleDriveService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("Authorization code is missing.");
        }

        if (string.IsNullOrEmpty(state))
        {
            return BadRequest("State parameter is missing.");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        // Validate the OAuth state token to prevent CSRF attacks.
        var validatedUserId = _googleDriveService.ValidateOAuthState(state);
        if (validatedUserId is null || validatedUserId != user.Id)
        {
            return BadRequest("Invalid or expired OAuth state token.");
        }

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/googledrivecallback";

        await _googleDriveService.ExchangeCodeForTokenAsync(user.Id, code, redirectUri);

        return LocalRedirect("/backup");
    }
}

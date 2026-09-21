using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletLedger.Application.Exceptions;
using WalletLedger.Application.Identity;

namespace WalletLedger.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(RegisterUserHandler registerUserHandler, LoginHandler loginHandler) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var userId = await registerUserHandler.HandleAsync(new RegisterUserCommand(request.Email, request.Password), ct);
            return Created(string.Empty, new { userId });
        }
        catch (EmailAlreadyRegisteredException)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await loginHandler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);
            return Ok(result);
        }
        catch (InvalidCredentialsException)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }
    }
}

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

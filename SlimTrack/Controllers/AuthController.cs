using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SlimTrack.Data.Database;
using SlimTrack.DTOs;
using SlimTrack.Models;

namespace SlimTrack.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AuthController(
        ILogger<AuthController> logger,
        AppDbContext dbContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>Registra um novo usuário no sistema.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email == request.Email.ToLowerInvariant());

        if (emailExists)
            return Conflict(new { message = "E-mail já cadastrado" });

        var hasher = new PasswordHasher<User>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.ToLowerInvariant().Trim(),
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Novo usuário registrado: {Email}", user.Email);

        return Created(string.Empty, new { id = user.Id, name = user.Name, email = user.Email, role = user.Role.ToString() });
    }

    /// <summary>Autentica o usuário e retorna um token JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        if (user == null)
            return Unauthorized(new { message = "Credenciais inválidas" });

        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Credenciais inválidas" });

        var token = GenerateJwtToken(user);

        _logger.LogInformation("Usuário {Email} autenticado com sucesso", user.Email);

        return Ok(token);
    }

    /// <summary>Retorna os dados do usuário autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        var user = await _dbContext.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
            return NotFound();

        return Ok(new { id = user.Id, name = user.Name, email = user.Email, role = user.Role.ToString() });
    }

    private LoginResponse GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key não configurada");
        var issuer = _configuration["Jwt:Issuer"] ?? "SlimTrack";
        var audience = _configuration["Jwt:Audience"] ?? "SlimTrack";
        var expirationHours = int.TryParse(_configuration["Jwt:ExpirationHours"], out var hours) ? hours : 8;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddHours(expirationHours);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials
        );

        return new LoginResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            ExpiresAt = expiration
        };
    }
}

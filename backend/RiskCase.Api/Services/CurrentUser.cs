using System.Security.Claims;

using RiskCase.Application.Common.Interfaces;

namespace RiskCase.Api.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
    public List<string>? Roles => _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();

}

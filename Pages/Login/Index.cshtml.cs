using System.Security.Claims;
using CaioFinance.Services.Acesso;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CaioFinance.Pages.Login;

public class IndexModel(ServicoAcesso acesso) : PageModel
{
    [BindProperty]
    public string? Usuario { get; set; }

    [BindProperty]
    public string? Senha { get; set; }

    public string? Erro { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Dashboard/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var origem = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "local";
        if (!acesso.TentarEntrar(Usuario, Senha, origem, out var erro))
        {
            Erro = erro;
            return Page();
        }

        var identidade = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, (Usuario ?? "").Trim())],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidade),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToPage("/Dashboard/Index");
    }
}

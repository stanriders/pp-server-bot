using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PpServerBot.Services;

namespace PpServerBot.Pages
{
    public class CompleteModel : PageModel
    {
        private readonly ILogger<CompleteModel> _logger;
        private readonly VerificationService _verificationService;

        public CompleteModel(ILogger<CompleteModel> logger, VerificationService verificationService)
        {
            _logger = logger;
            _verificationService = verificationService;
        }

        public async Task<IActionResult> OnGet(Guid? id = null)
        {
            if (id == null || id == Guid.Empty)
            {
                _logger.LogWarning("Failed to log in user because of the missing id!");

                return RedirectToPage("Error", new { errorType = "missing-id" });
            }

            var authResult = await HttpContext.AuthenticateAsync("ExternalCookies");
            if (!authResult.Succeeded)
            {
                _logger.LogWarning("Failed to log in user {Id}!", id);

                return RedirectToPage("Error", new {errorType = "failed-login"});
            }

            var accessToken = await HttpContext.GetTokenAsync("ExternalCookies", "access_token");
            if (accessToken == null)
            {
                _logger.LogWarning("Failed to log in user {Id} - null access token!", id);

                return RedirectToPage("Error", new { errorType = "missing-token" });
            }

            if (!await _verificationService.Finish(id.Value, accessToken))
            {
                return RedirectToPage("Error", new { errorType = "failed-verification" });
            }

            return Page();
        }
    }
}

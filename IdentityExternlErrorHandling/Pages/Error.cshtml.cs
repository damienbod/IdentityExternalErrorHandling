using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace IdentityExternalErrorHandling.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet(string? remoteError)
    {
        if (remoteError != null)
        {
            Error = "Remote authentication error";
            ErrorDescription = remoteError;
        }

        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}

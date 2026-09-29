using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaundryHub2._0.Controllers;

[Authorize(Roles = "Admin,Manager,Staff")]
public class StaffController : Controller
{
    public IActionResult Index(string? tab = null)
    {
        // Admin, Manager, and Staff share the same comprehensive 10-module operational shell
        return RedirectToAction("Index", "Admin", new { tab });
    }
}

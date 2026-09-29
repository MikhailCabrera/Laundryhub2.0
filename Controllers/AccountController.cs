using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToDashboard();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth-policy")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        ApplicationUser? user = null;
        try
        {
            user = await _userManager.FindByEmailAsync(model.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error during login lookup for email {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "We couldn't complete the login right now. Please try again later.");
            return View(model);
        }

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        if (user.IsArchived)
        {
            ModelState.AddModelError(string.Empty, "This account has been archived. Please contact support.");
            return View(model);
        }

        if (user.IsSuspended)
        {
            var reason = string.IsNullOrWhiteSpace(user.SuspendNote) ? "" : $" Reason: {user.SuspendNote}";
            ModelState.AddModelError(string.Empty, $"This account has been suspended.{reason}");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName ?? user.Email!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return await RedirectToDashboardAsync(user);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out for email {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "This account is temporarily locked due to multiple failed login attempts. Please try again in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToDashboard();
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth-policy")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Clean & normalize Philippine phone number format
        var normalizedPhone = model.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");
        if (normalizedPhone.StartsWith("09"))
        {
            normalizedPhone = "+63" + normalizedPhone.Substring(1);
        }

        try
        {
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName.Trim(),
                PhoneNumber = normalizedPhone,
                District = model.District,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Default role is always Customer
                await _userManager.AddToRoleAsync(user, "Customer");

                TempData["SuccessMessage"] = "Account created successfully! Please log in to access your dashboard.";
                return RedirectToAction("Login", "Account");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, "Unable to connect to MySQL database. Please ensure Apache and MySQL are started in the XAMPP Control Panel on port 3306.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToDashboard()
    {
        if (User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Staff"))
        {
            return RedirectToAction("Index", "Admin");
        }
        if (User.IsInRole("Rider"))
        {
            return RedirectToAction("Index", "Rider");
        }
        return RedirectToAction("Index", "Customer");
    }

    private async Task<IActionResult> RedirectToDashboardAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin") || roles.Contains("Manager") || roles.Contains("Staff"))
        {
            return RedirectToAction("Index", "Admin");
        }
        if (roles.Contains("Rider"))
        {
            return RedirectToAction("Index", "Rider");
        }
        return RedirectToAction("Index", "Customer");
    }
}

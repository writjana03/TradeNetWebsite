using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using TradeNetAPI.Models;

namespace TradeNetPortal.Controllers
{
    public class AccountController : Controller
    {
        private readonly HttpClient _httpClient;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("TradeNetAPI");
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "Email and password are required.";
                return View();
            }

            try
            {
                // Call API to get all users and find the one matching email
                var usersResponse = await _httpClient.GetAsync("/api/user");
                if (!usersResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Unable to connect to authentication service.";
                    return View();
                }

                var usersContent = await usersResponse.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<User>>(usersContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                var user = users.FirstOrDefault(u => u.Email == email);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not registered. Please register first.";
                    return View();
                }

                // TODO: Implement proper password validation
                // For now, we're assuming email match is sufficient (development only)

                // Set session
                HttpContext.Session.SetInt32("UserId", user.UserID);
                HttpContext.Session.SetString("UserName", user.Name);
                HttpContext.Session.SetString("UserEmail", user.Email);

                TempData["SuccessMessage"] = $"Welcome back, {user.Name}!";

                // Redirect to Business Portal
                return RedirectToAction("Profile", "BusinessPortal");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error during login: " + ex.Message;
                return View();
            }
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string fullName, string businessName, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "All fields are required.";
                return View();
            }

            if (password != confirmPassword)
            {
                TempData["ErrorMessage"] = "Passwords do not match.";
                return View();
            }

            try
            {
                // Call API to get all users and check if email already exists
                var usersResponse = await _httpClient.GetAsync("/api/user");
                if (!usersResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Unable to connect to registration service.";
                    return View();
                }

                var usersContent = await usersResponse.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<User>>(usersContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                var existingUser = users.FirstOrDefault(u => u.Email == email);
                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = "User with this email already exists.";
                    return View();
                }

                // Create new user
                var newUser = new User
                {
                    Name = fullName,
                    Email = email,
                    Phone = "",
                    Role = "Business",
                    Status = "Active"
                };

                // Post user to API
                var userJson = JsonSerializer.Serialize(newUser);
                var userContent = new StringContent(userJson, Encoding.UTF8, "application/json");
                var createUserResponse = await _httpClient.PostAsync("/api/user", userContent);

                if (!createUserResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Failed to create user account.";
                    return View();
                }

                var userResponseContent = await createUserResponse.Content.ReadAsStringAsync();
                var createdUser = JsonSerializer.Deserialize<User>(userResponseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (createdUser == null)
                {
                    TempData["ErrorMessage"] = "Failed to create user account.";
                    return View();
                }

                // Create business profile if business name provided
                if (!string.IsNullOrEmpty(businessName))
                {
                    var business = new Business
                    {
                        UserID = createdUser.UserID,
                        Name = businessName,
                        Type = "Trader",
                        Address = "",
                        ContactInfo = email,
                        Status = "Pending",
                        RegistrationDate = DateTime.Now,
                        ComplianceStatus = "Compliant"
                    };

                    var businessJson = JsonSerializer.Serialize(business);
                    var businessContent = new StringContent(businessJson, Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync("/api/business", businessContent);
                }

                // Auto-login after registration
                HttpContext.Session.SetInt32("UserId", createdUser.UserID);
                HttpContext.Session.SetString("UserName", createdUser.Name);
                HttpContext.Session.SetString("UserEmail", createdUser.Email);

                TempData["SuccessMessage"] = $"Registration successful! Welcome, {createdUser.Name}!";

                // Redirecting to Business Portal Profile page
                return RedirectToAction("Profile", "BusinessPortal");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error during registration: " + ex.Message;
                return View();
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }
    }
}

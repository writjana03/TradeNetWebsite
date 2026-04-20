using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using TradeNetAPI.Models;
using TradeNetAPI.Models.ViewModels;

namespace TradeNetPortal.Controllers
{
    public class BusinessPortalController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _environment;

        public BusinessPortalController(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
        {
            _httpClient = httpClientFactory.CreateClient("TradeNetAPI");
            _environment = environment;
        }

        public async Task<IActionResult> Profile()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "Please login to access the business portal.";
                return RedirectToAction("Login", "Account");
            }

            int userId = GetCurrentUserId();
            try
            {
                var userResponse = await _httpClient.GetAsync($"/api/user/{userId}");
                var businessResponse = await _httpClient.GetAsync($"/api/business/user/{userId}");

                User? user = null;
                Business? business = null;
                List<BusinessDocument> documents = new();

                if (userResponse.IsSuccessStatusCode)
                {
                    var userContent = await userResponse.Content.ReadAsStringAsync();
                    user = JsonSerializer.Deserialize<User>(userContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                if (businessResponse.IsSuccessStatusCode)
                {
                    var businessContent = await businessResponse.Content.ReadAsStringAsync();
                    business = JsonSerializer.Deserialize<Business>(businessContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (business != null)
                    {
                        var docsResponse = await _httpClient.GetAsync($"/api/business/{business.BusinessID}/documents");
                        if (docsResponse.IsSuccessStatusCode)
                        {
                            var docsContent = await docsResponse.Content.ReadAsStringAsync();
                            var docsArray = JsonSerializer.Deserialize<List<BusinessDocument>>(docsContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            documents = docsArray ?? new();
                        }
                    }
                }

                var complianceStatus = business?.ComplianceStatus ?? "Compliant";
                var complianceMessage = complianceStatus == "Non-Compliant"
                    ? "Your business is non-compliant. Please update required documents."
                    : null;

                var viewModel = new ProfileViewModel
                {
                    User = user ?? new User(),
                    Business = business,
                    Documents = documents,
                    ComplianceStatus = complianceStatus,
                    ComplianceMessage = complianceMessage
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading profile: " + ex.Message;
                return View(new ProfileViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(BusinessUpdateViewModel model)
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "Please login to access the business portal.";
                return RedirectToAction("Login", "Account");
            }

            int userId = GetCurrentUserId();

            try
            {
                var businessResponse = await _httpClient.GetAsync($"/api/business/user/{userId}");
                Business? business = null;

                if (businessResponse.IsSuccessStatusCode)
                {
                    var content = await businessResponse.Content.ReadAsStringAsync();
                    business = JsonSerializer.Deserialize<Business>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                else if (model.BusinessID == 0)
                {
                    business = new Business
                    {
                        UserID = userId,
                        Name = model.Name,
                        Type = model.Type,
                        Address = model.Address,
                        ContactInfo = model.ContactInfo,
                        RegistrationNumber = model.RegistrationNumber,
                        RegistrationDate = DateTime.Now,
                        Status = "Pending",
                        ComplianceStatus = "Compliant"
                    };

                    var json = JsonSerializer.Serialize(business);
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                    var createResponse = await _httpClient.PostAsync("/api/business", httpContent);

                    if (createResponse.IsSuccessStatusCode)
                    {
                        var responseContent = await createResponse.Content.ReadAsStringAsync();
                        business = JsonSerializer.Deserialize<Business>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                }

                if (business != null)
                {
                    business.Name = model.Name;
                    business.Type = model.Type;
                    business.Address = model.Address;
                    business.ContactInfo = model.ContactInfo;
                    business.RegistrationNumber = model.RegistrationNumber;

                    var json = JsonSerializer.Serialize(business);
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                    await _httpClient.PutAsync($"/api/business/{business.BusinessID}", httpContent);

                    if (model.Documents != null)
                    {
                        foreach (var doc in model.Documents)
                        {
                            var fileName = await SaveFile(doc, "documents");
                            var document = new BusinessDocument
                            {
                                BusinessID = business.BusinessID,
                                DocType = "IDProof",
                                FileURI = fileName,
                                UploadedDate = DateTime.Now,
                                VerificationStatus = "Pending"
                            };
                            var docJson = JsonSerializer.Serialize(document);
                            var docContent = new StringContent(docJson, Encoding.UTF8, "application/json");
                            await _httpClient.PostAsync("/api/business/documents", docContent);
                        }
                    }
                }

                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating profile: " + ex.Message;
            }

            return RedirectToAction(nameof(Profile));
        }

        public async Task<IActionResult> LicenseList()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "Please login to access the business portal.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var licensesResponse = await _httpClient.GetAsync("/api/license");
                var programsResponse = await _httpClient.GetAsync("/api/program");

                var availableLicenses = new List<TradeLicense>();
                var activePrograms = new List<TradeProgram>();

                if (licensesResponse.IsSuccessStatusCode)
                {
                    var content = await licensesResponse.Content.ReadAsStringAsync();
                    var allLicenses = JsonSerializer.Deserialize<List<TradeLicense>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    availableLicenses = allLicenses.Where(l => l.Status == "Available").ToList();
                }

                if (programsResponse.IsSuccessStatusCode)
                {
                    var content = await programsResponse.Content.ReadAsStringAsync();
                    var allPrograms = JsonSerializer.Deserialize<List<TradeProgram>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    activePrograms = allPrograms.Where(p => p.Status == "Active").ToList();
                }

                var viewModel = new LicenseListViewModel
                {
                    AvailableLicenses = availableLicenses,
                    ActivePrograms = activePrograms
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading licenses: " + ex.Message;
                return View(new LicenseListViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApplyLicense(LicenseApplicationViewModel model)
        {
            if (!IsUserLoggedIn())
            {
                return Json(new { success = false, message = "Please login first." });
            }

            int userId = GetCurrentUserId();

            try
            {
                var businessResponse = await _httpClient.GetAsync($"/api/business/user/{userId}");
                if (!businessResponse.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Please complete your business profile first." });
                }

                var businessContent = await businessResponse.Content.ReadAsStringAsync();
                var business = JsonSerializer.Deserialize<Business>(businessContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (business == null)
                {
                    return Json(new { success = false, message = "Please complete your business profile first." });
                }

                var license = new TradeLicense
                {
                    BusinessID = business.BusinessID,
                    Type = model.LicenseType,
                    Title = model.Title,
                    Description = model.Description,
                    Status = "Pending",
                    ApplicationStatus = "PendingDocumentVerification",
                    ApplicationDate = DateTime.Now
                };

                var json = JsonSerializer.Serialize(license);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var licenseResponse = await _httpClient.PostAsync("/api/license", content);

                if (!licenseResponse.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Failed to create license application." });
                }

                var licenseContent = await licenseResponse.Content.ReadAsStringAsync();
                var createdLicense = JsonSerializer.Deserialize<TradeLicense>(licenseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (createdLicense != null && model.Documents != null)
                {
                    foreach (var doc in model.Documents)
                    {
                        var fileName = await SaveFile(doc, "licenses");
                        var licenseDoc = new LicenseDocument
                        {
                            LicenseID = createdLicense.LicenseID,
                            DocType = "Application",
                            FileURI = fileName,
                            UploadedDate = DateTime.Now,
                            VerificationStatus = "Pending"
                        };
                        var docJson = JsonSerializer.Serialize(licenseDoc);
                        var docContent = new StringContent(docJson, Encoding.UTF8, "application/json");
                        await _httpClient.PostAsync("/api/license/documents", docContent);
                    }
                }

                return Json(new { success = true, message = "License application submitted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public async Task<IActionResult> Dashboard()
        {
            if (!IsUserLoggedIn())
            {
                TempData["ErrorMessage"] = "Please login to access the business portal.";
                return RedirectToAction("Login", "Account");
            }

            int userId = GetCurrentUserId();

            try
            {
                var businessResponse = await _httpClient.GetAsync($"/api/business/user/{userId}");

                if (!businessResponse.IsSuccessStatusCode)
                {
                    return View(new DashboardViewModel());
                }

                var businessContent = await businessResponse.Content.ReadAsStringAsync();
                var business = JsonSerializer.Deserialize<Business>(businessContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (business == null)
                {
                    return View(new DashboardViewModel());
                }

                var licensesResponse = await _httpClient.GetAsync($"/api/license/business/{business.BusinessID}");
                var transactionsResponse = await _httpClient.GetAsync($"/api/transaction/business/{business.BusinessID}");
                var programsResponse = await _httpClient.GetAsync("/api/program");

                var appliedLicenses = new List<TradeLicense>();
                var transactions = new List<Transaction>();
                var availableSubsidies = new List<TradeProgram>();

                if (licensesResponse.IsSuccessStatusCode)
                {
                    var content = await licensesResponse.Content.ReadAsStringAsync();
                    appliedLicenses = JsonSerializer.Deserialize<List<TradeLicense>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }

                if (transactionsResponse.IsSuccessStatusCode)
                {
                    var content = await transactionsResponse.Content.ReadAsStringAsync();
                    transactions = JsonSerializer.Deserialize<List<Transaction>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    transactions = transactions.OrderByDescending(t => t.Date).ToList();
                }

                if (programsResponse.IsSuccessStatusCode)
                {
                    var content = await programsResponse.Content.ReadAsStringAsync();
                    var allPrograms = JsonSerializer.Deserialize<List<TradeProgram>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    availableSubsidies = allPrograms.Where(p => p.Status == "Active").ToList();
                }

                var viewModel = new DashboardViewModel
                {
                    AppliedLicenses = appliedLicenses,
                    Transactions = transactions,
                    AvailableSubsidies = availableSubsidies,
                    PendingLicenses = appliedLicenses.Count(l => l.ApplicationStatus?.Contains("Pending") == true),
                    ApprovedLicenses = appliedLicenses.Count(l => l.ApplicationStatus == "Approved"),
                    PendingTransactions = transactions.Count(t => t.Status == "Pending"),
                    TotalTransactionAmount = transactions.Where(t => t.Status == "Completed").Sum(t => t.Amount)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading dashboard: " + ex.Message;
                return View(new DashboardViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction(TransactionCreateViewModel model)
        {
            if (!IsUserLoggedIn())
            {
                return Json(new { success = false, message = "Please login first." });
            }

            int userId = GetCurrentUserId();

            try
            {
                var businessResponse = await _httpClient.GetAsync($"/api/business/user/{userId}");
                if (!businessResponse.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Business profile not found." });
                }

                var businessContent = await businessResponse.Content.ReadAsStringAsync();
                var business = JsonSerializer.Deserialize<Business>(businessContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (business == null)
                {
                    return Json(new { success = false, message = "Business profile not found." });
                }

                var transaction = new Transaction
                {
                    BusinessID = business.BusinessID,
                    Type = model.Type,
                    Amount = model.Amount,
                    Date = DateTime.Now,
                    Status = "Pending",
                    Description = model.Description,
                    InvoiceNumber = model.InvoiceNumber,
                    Counterparty = model.Counterparty
                };

                var json = JsonSerializer.Serialize(transaction);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var transactionResponse = await _httpClient.PostAsync("/api/transaction", content);

                if (!transactionResponse.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Failed to create transaction." });
                }

                return Json(new { success = true, message = "Transaction created successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            if (!IsUserLoggedIn())
            {
                return Json(new List<Notification>());
            }

            int userId = GetCurrentUserId();
            var notifications = new List<Notification>();

            try
            {
                return Json(notifications);
            }
            catch
            {
                return Json(notifications);
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(int notificationId)
        {
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetLicenseDetails(int licenseId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/license/{licenseId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var license = JsonSerializer.Deserialize<TradeLicense>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return Json(license);
                }
            }
            catch { }

            return Json(null);
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactionDetails(int transactionId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/transaction/{transactionId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var transaction = JsonSerializer.Deserialize<Transaction>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return Json(transaction);
                }
            }
            catch { }

            return Json(null);
        }

        [HttpGet]
        public async Task<IActionResult> GetProgramDetails(int programId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/program/{programId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var program = JsonSerializer.Deserialize<TradeProgram>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return Json(program);
                }
            }
            catch { }

            return Json(null);
        }

        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string? GetCurrentUserName()
        {
            return HttpContext.Session.GetString("UserName");
        }

        private bool IsUserLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId").HasValue;
        }

        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{folder}/{uniqueFileName}";
        }
    }
}

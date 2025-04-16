using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Fertilizer360.Models;
using System.Diagnostics;
using Fertilizer360.Data;
using Microsoft.EntityFrameworkCore;
using Fertilizer360.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Net.Mail;
using System.Net;

namespace Fertilizer360.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;


        public HomeController(ILogger<HomeController> logger, AppDbContext context, UserManager<Users> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;

        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Shop(string state, string district, string villageOrTaluka)
        {
            var shops = _context.Shops.AsQueryable();

            if (!string.IsNullOrEmpty(state))
            {
                shops = shops.Where(s => s.State == state);
            }

            if (!string.IsNullOrEmpty(district))
            {
                shops = shops.Where(s => s.District == district);
            }

            if (!string.IsNullOrEmpty(villageOrTaluka))
            {
                shops = shops.Where(s => s.VillageOrTaluka == villageOrTaluka);
            }

            return View("Shop", shops.ToList());
        }

        // GET: Shop/ShopDetails/5
        public async Task<IActionResult> ShopDetails(int? id, string searchTerm = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shop = await _context.Shops
                .FirstOrDefaultAsync(m => m.Id == id);

            if (shop == null)
            {
                return NotFound();
            }

            // Get fertilizers for this shop with optional search
            var fertilizers = _context.Fertilizers.Where(f => f.ShopId == id);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                fertilizers = fertilizers.Where(f => f.Name.Contains(searchTerm));
            }

            ViewBag.Fertilizers = await fertilizers.ToListAsync();
            ViewBag.SearchTerm = searchTerm;

            return View(shop);
        }

        // GET: Fertilizer/Details/5
        public async Task<IActionResult> FertilizerDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fertilizer = await _context.Fertilizers
                .Include(f => f.Shop)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (fertilizer == null)
            {
                return NotFound();
            }

            return View(fertilizer);
        }
        [HttpPost]
        public IActionResult AdvanceBooking(int FertilizerId, string FertilizerName, decimal Price, int Quantity)
        {
            // Calculate total
            decimal subtotal = Price * Quantity;

            // Create order viewmodel
            var orderViewModel = new OrderViewModel
            {
                FertilizerId = FertilizerId,
                FertilizerName = FertilizerName,
                Price = Price,
                Quantity = Quantity,
                Subtotal = subtotal
            };

            return View(orderViewModel);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CompleteBooking(int FertilizerId, int Quantity, decimal Subtotal)
        {
            var user = await _userManager.GetUserAsync(User);
            var fertilizer = await _context.Fertilizers.FindAsync(FertilizerId);

            if (fertilizer == null || fertilizer.Stocks < Quantity)
            {
                return RedirectToAction("Error");
            }

            // Fetch the shop ID from the fertilizer (ensure it's valid)
            var shop = await _context.Shops.FindAsync(fertilizer.ShopId);
            if (shop == null)
            {
                return RedirectToAction("Error"); // This avoids foreign key constraint issues
            }

            // Create order with valid foreign key (ShopId)
            var order = new Order
            {
                FertilizerId = FertilizerId,
                ShopId = fertilizer.ShopId, // ✅ Must be present to avoid FK constraint error
                UserName = user.FullName,
                Quantity = Quantity,
                Subtotal = Subtotal,
                OrderDate = DateTime.Now
            };

            _context.Orders.Add(order);

            // Reduce fertilizer stock
            fertilizer.Stocks -= Quantity;
            _context.Fertilizers.Update(fertilizer);

            await _context.SaveChangesAsync();

            // Redirect to OrderConfirmation view with the created order's ID
            return RedirectToAction("OrderConfirmation", new { id = order.Id });
        }


        public async Task<IActionResult> OrderConfirmation(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Fertilizer)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }


        public IActionResult About()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SubmitContactForm(string Name, string Email, string Subject, string Message)
        {
            try
            {
                // Create a new mail message
                var mailMessage = new MailMessage
                {
                    From = new MailAddress("your-email@gmail.com"), // Replace with your email
                    Subject = "New Contact Us Form Submission",
                    Body = $"Name: {Name}\nEmail: {Email}\nSubject: {Subject}\nMessage: {Message}",
                    IsBodyHtml = false
                };

                // Add recipient email (your Gmail)
                mailMessage.To.Add("shubhamgajera122@gmail.com"); // Replace with your Gmail address

                // Setup SMTP client to send email
                using (var smtpClient = new SmtpClient("smtp.gmail.com"))
                {
                    smtpClient.Port = 587;
                    smtpClient.Credentials = new NetworkCredential("shubhamgajera122@gmail.com", "ujjo xwmb uyia arfc"); // Replace with your Gmail credentials
                    smtpClient.EnableSsl = true;

                    // Send email
                    smtpClient.Send(mailMessage);
                }

                // Return a success view or redirect to a confirmation page
                return RedirectToAction("ContactUs"); // Redirect to a confirmation page
            }
            catch (Exception ex)
            {
                // Handle error and return an error view
                return View("Error", new { error = ex.Message });
            }
        }

        // Contact Confirmation Page after submission
        public IActionResult ContactUs()
        {
            return View();
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
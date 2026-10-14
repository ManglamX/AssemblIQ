using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartTorque.Web.Areas.Worker.Controllers;

[Area("Worker")]
[Authorize(Roles = "Worker")]
public class JobBoardController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
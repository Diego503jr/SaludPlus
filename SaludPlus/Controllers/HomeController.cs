using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult AccessDenied()
        {
            Response.StatusCode = 403;

            Response.TrySkipIisCustomErrors = true;

            return View();
        }

        public ActionResult PageNotFound()
        {
            Response.StatusCode = 404;

            Response.TrySkipIisCustomErrors = true;

            return View();
        }
    }
}
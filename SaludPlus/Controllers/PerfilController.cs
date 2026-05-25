using SaludPlus.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    [CustomAuthorize(Roles = "Medico,Recepcionista")]
    public class PerfilController : Controller
    {
        // GET: Perfil
        public ActionResult Index()
        {
            return View();
        }
    }
}
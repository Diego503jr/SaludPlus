using SaludPlus.Helpers;
using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Policy;
using System.Web;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public JsonResult ValidarUsuario(Usuarios usuario)
        {
                string claveCifrada = SecurityHelper.GetSHA256(usuario.PasswordHash);

            var info = db.Usuarios
                         .Include(u => u.Roles)
                         .FirstOrDefault(u => u.Email == usuario.Email && u.PasswordHash == claveCifrada);

            if (info != null)
            {
                info.UltimoAcceso = DateTime.Now;
                db.Entry(info).State = EntityState.Modified;
                db.SaveChanges();

                Session["User"] = info;
                return Json(new { success = true, url = Url.Action("Index", "Home") });
            }

            return Json(new { success = false, message = "Datos incorrectos" });
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Login");
        }
    }
}
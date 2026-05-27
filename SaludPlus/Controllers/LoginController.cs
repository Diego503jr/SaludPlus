using SaludPlus.Helpers;
using SaludPlus.Models;
using System.Web.Mvc;
using System.Web.Security;

namespace SaludPlus.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public JsonResult ValidarUsuario(Usuarios usuario)
        {
            var service = new LoginService();
            var resultado = service.ProcesarLogin(usuario.Email, usuario.PasswordHash);
            return Json(resultado);
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Login");
        }
    }
}
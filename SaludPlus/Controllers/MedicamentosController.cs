using SaludPlus.Helpers;
using SaludPlus.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    [CustomAuthorize(Roles = "Administrador")]
    public class MedicamentosController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Medicamentos
        public ActionResult Index()
        {
            var comodin = db.Medicamentos.FirstOrDefault(m => m.Nombre == "MEDICAMENTO EXTERNO (Solo texto)");
            if (comodin == null)
            {
                db.Medicamentos.Add(new Medicamentos
                {
                    Nombre = "MEDICAMENTO EXTERNO (Solo texto)",
                    Laboratorio = "Externo",
                    Presentacion = "Tabletas",
                    ViaAdministracion = "Oral",
                    StockActual = 999999,
                    StockMinimo = 0,
                    Precio = 0,
                    Activo = true
                });
                db.SaveChanges();
            }

            return View();
        }

        // LISTAR PARA LA TABLA AJAX
        [HttpGet]
        public JsonResult Listar()
        {
            // Agregamos el filtro para mostrar ÚNICAMENTE los registros activos en el catálogo
            var lista = db.Medicamentos
                .Where(m => m.Activo == true)
                .Select(m => new
                {
                    m.MedicamentoID,
                    m.Nombre,
                    m.Laboratorio,
                    m.Presentacion,
                    m.ViaAdministracion,
                    m.StockActual,
                    m.StockMinimo,
                    m.Precio 
                })
                .ToList();

            return Json(lista, JsonRequestBehavior.AllowGet);
        }

        // OBTENER UNO SOLO (Para editar)
        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var med = db.Medicamentos.Find(id);
            if (med == null) return Json(new { success = false, mensaje = "No encontrado" }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                data = new
                {
                    med.MedicamentoID,
                    med.Nombre,
                    med.Laboratorio,
                    med.Presentacion,
                    med.ViaAdministracion,
                    med.StockActual,
                    med.StockMinimo,
                    med.Precio
                }
            }, JsonRequestBehavior.AllowGet);
        }

        // GUARDAR O EDITAR
        [HttpPost]
        public JsonResult Guardar(Medicamentos obj)
        {
            try
            {
                if (obj.MedicamentoID == 0)
                {
                    obj.Activo = true; // Por defecto entra activo
                    db.Medicamentos.Add(obj);
                }
                else
                {
                    var data = db.Medicamentos.Find(obj.MedicamentoID);
                    if (data == null) return Json(new { success = false, mensaje = "El registro no existe." });

                    if (data.Nombre != "MEDICAMENTO EXTERNO (Solo texto)")
                    {
                        data.Nombre = obj.Nombre;
                        data.Laboratorio = obj.Laboratorio;
                        data.Presentacion = obj.Presentacion;
                        data.ViaAdministracion = obj.ViaAdministracion;
                    }

                    data.StockActual = obj.StockActual;
                    data.StockMinimo = obj.StockMinimo;
                    data.Precio = obj.Precio;

                    db.Entry(data).State = System.Data.Entity.EntityState.Modified;
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        // ELIMINAR
        [HttpPost]
        public JsonResult Eliminar(int id)
        {
            try
            {
                var data = db.Medicamentos.Find(id);
                if (data != null)
                {
                    if (data.Nombre == "MEDICAMENTO EXTERNO (Solo texto)")
                    {
                        return Json(new { success = false, mensaje = "No se puede eliminar el medicamento comodín del sistema." });
                    }

                    data.Activo = false; 
                    db.Entry(data).State = System.Data.Entity.EntityState.Modified;

                    db.SaveChanges();
                    return Json(new { success = true });
                }
                return Json(new { success = false, mensaje = "No encontrado" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }
    }
}
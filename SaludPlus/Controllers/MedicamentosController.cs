using SaludPlus.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class MedicamentosController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Medicamentos
        public ActionResult Index()
        {
            // --- INICIALIZADOR DEL COMODÍN ---
            // Revisamos si ya existe nuestro medicamento para "Solo Texto"
            var comodin = db.Medicamentos.FirstOrDefault(m => m.Nombre == "MEDICAMENTO EXTERNO (Solo texto)");
            if (comodin == null)
            {
                // Si no existe, lo creamos usando valores válidos para evitar el CHECK Constraint de SQL
                db.Medicamentos.Add(new Medicamentos
                {
                    Nombre = "MEDICAMENTO EXTERNO (Solo texto)",
                    Laboratorio = "Externo",
                    Presentacion = "Tabletas",      // <- Cambiado de "N/A" a "Tabletas"
                    ViaAdministracion = "Oral",     // <- Cambiado de "N/A" a "Oral" para pasar la validación SQL
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
            var lista = db.Medicamentos
                .Where(m => m.Activo == true)
                .Select(m => new {
                    m.MedicamentoID,
                    m.Nombre,
                    m.Laboratorio,
                    m.Presentacion,
                    m.ViaAdministracion,
                    m.StockActual,
                    m.StockMinimo,
                    m.Precio
                }).ToList();

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

                    // Evitamos que editen el nombre del Comodín
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
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        // ELIMINAR (Soft Delete)
        [HttpPost]
        public JsonResult Eliminar(int id)
        {
            try
            {
                var data = db.Medicamentos.Find(id);
                if (data != null)
                {
                    // Protegemos el comodín para que no lo borren por error
                    if (data.Nombre == "MEDICAMENTO EXTERNO (Solo texto)")
                    {
                        return Json(new { success = false, mensaje = "No se puede eliminar el medicamento comodín del sistema." });
                    }

                    data.Activo = false; // Eliminación lógica
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
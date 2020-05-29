using System;
using System.IO;
using System.Linq;
using ImageServer.Helpers;
using ImageServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageServer.Controllers
{
    public class UploadController : Controller
    {
        public UploadController(IAppConfigService appConfigService)
        {
            this.appConfigService = appConfigService;
        }

        [HttpGet]
        public ActionResult Index()
        {
            return Content("allow get");
        }
        [HttpPost]
        public ActionResult Index(string apiKey, string category)
        {
            var file = this.Request.Form.Files.First();

            if (_apiKey != apiKey) return Error("Invalid apiKey");
            var filename = file.FileName;
            if (
                !_allowAllExtenssions &&
                !_extensions.Any(c => filename.EndsWith(c)))
            {
                return Error("Invalid file extension");
            }
            if (_allowLocalIpUploadOnly)
            {
                if (!Helper.IsLocalIp(HttpContext.Connection))
                {
                    return Error("Invalid IP , only allow local ip to upload");
                }
            }
            if (string.IsNullOrWhiteSpace(category) || _allowFolders.All(c => category != c))
            {
                return Error("Invalid category");
            }
            try
            {
                var extenssion = Path.GetExtension(filename);
                var path = new ResizingPath(category, extenssion);
                var  root  = Directory.GetCurrentDirectory();
                var physicalPath = Path.Combine(root, path.PhysicalPath);
                
                if (!Directory.Exists(physicalPath))
                {
                    Directory.CreateDirectory(physicalPath);
                }
                var physicalFilename = Path.Combine(root, path.PhysicalFilename);

                using (var stream = System.IO.File.OpenWrite(physicalFilename))
                {
                    file.CopyTo(stream);
                }

                return Json(new UploadResult
                {
                    IsSuccess = true,
                    FormatUrl = path.VirtualFormatFilename,
                    RawUrl = path.RawPath
                });
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }
        #region Override Method

        private ActionResult Error(string message)
        {
            return Json(new { success = false, message });
        }

        #endregion

        #region Configure var

        private string _apiKey => appConfigService.Config.ApiKey;
        private string[] _allowFolders => (appConfigService.Config.AllowFolders ?? "").Split(',');
        private string[] _extensions = { ".jpg", ".png" };
        private bool _allowAllExtenssions => (appConfigService.Config.AllowAllExtensions == "true");
        private bool _allowLocalIpUploadOnly => (appConfigService.Config.AllowLocalIpUploadOnly == "true");
        private readonly IAppConfigService appConfigService;

        #endregion
    }
}
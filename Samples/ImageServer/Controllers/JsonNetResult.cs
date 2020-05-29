using System;
using Microsoft.AspNetCore.Mvc;

namespace ImageServer.Controllers
{
    // public class JsonNetResult : JsonResult
    // {
    //     public override void ExecuteResult(ControllerContext context)
    //     {
    //         if (context == null)
    //             throw new ArgumentNullException("context");

    //         var response = context.HttpContext.Response;

    //         response.ContentType = !String.IsNullOrEmpty(ContentType)
    //             ? ContentType
    //             : "application/json";

    //         // If you need special handling, you can call another form of SerializeObject below
    //         var serializedObject = JsonConvert.SerializeObject(Data, Formatting.None);
    //         response.Write(serializedObject);
    //     }
    // }
}
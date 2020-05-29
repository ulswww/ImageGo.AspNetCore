using System;
using System.Collections.Generic;
using System.Text;

namespace ImageGo.AspNetCore.Models
{
    public class WatermarksModel
    {
        public IEnumerable<WatermarkTextModel> WatermarkTextList { get; set; }
        public IEnumerable<WatermarkImageModel> WatermarkImageList { get; set; }
    }




}

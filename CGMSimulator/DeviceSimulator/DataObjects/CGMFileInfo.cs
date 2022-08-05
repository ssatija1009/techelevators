using System;
namespace DeviceSimulator.DataObjects
{
    public class CGMFileInfo
    {
        public DateTime Time { get; set; }

        public string BG { get; set; }

        public string CGM { get; set; }

        public string CHO { get; set; }

        public string Insulin { get; set; }

        public string LBGI { get; set; }

        public string HBGI { get; set; }

        public string Risk { get; set; }

    }
}


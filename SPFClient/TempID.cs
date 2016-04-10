using System.Management;
using System.Windows.Forms;
using System;
using System.Linq;
namespace SPFClient
{
    public static class TempID
    {
        public static int GetUniqueMachineID()
        {
            try
            {
                string cpuInfo = string.Empty;
                ManagementClass mc = new ManagementClass("win32_processor");
                ManagementObjectCollection moc = mc.GetInstances();

                foreach (ManagementObject mo in moc)
                {
                    cpuInfo = mo.Properties["processorID"].Value.ToString();
                    break;
                }

                string drive = "C";
                ManagementObject dsk = new ManagementObject(
                    @"win32_logicaldisk.deviceid=""" + drive + @":""");
                dsk.Get();
                string volumeSerial = dsk["VolumeSerialNumber"].ToString();

                int uniqueId = int.Parse(volumeSerial, System.Globalization.NumberStyles.HexNumber);

                return uniqueId;
            }
            catch (ManagementException e)
            {
                MessageBox.Show("An error occurred while querying for WMI data: " + e.Message);
                return 0;
            }
        }
    }
}

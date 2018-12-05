using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SystemMonitor
{
    public partial class SystemMonitor : Form
    {
        Int16 prevRAMRate;
        Int64 RAMCapacity;

        PerformanceCounter CPUCounter;
        PerformanceCounter RAMCounter;
        PerformanceCounter diskReadCounter;
        PerformanceCounter diskWriteCounter;
        String[] networkInstanceNames;
        NetworkInterface[] adapters;
        PerformanceCounter[] networkSentCounters;
        PerformanceCounter[] networkReceivedCounters;

        Bitmap bitmap;
        Graphics graphics;
        Font stringFont;
        SolidBrush stringSolidBrush;
        Rectangle stringRectangle;
        StringFormat stringFormat;

        public SystemMonitor()
        {
            InitializeComponent();
            InitializePerformanceCounter();
            InitializeNetworkPerformanceCounter();
            InitializeIcon();
            getRAMCapacity();
            //netStatus();
        }

        protected override void SetVisibleCore(bool e)
        {
            base.SetVisibleCore(false);
        }

        private void InitializePerformanceCounter()
        {
            CPUCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            RAMCounter = new PerformanceCounter("Memory", "Available MBytes", null);
            diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
        }

        private void InitializeNetworkPerformanceCounter()
        {
            networkInstanceNames = (new PerformanceCounterCategory("Network Interface")).GetInstanceNames();
            networkSentCounters = new PerformanceCounter[networkInstanceNames.Length];
            networkReceivedCounters = new PerformanceCounter[networkInstanceNames.Length];
            foreach (string name in networkInstanceNames)
            {
                networkSentCounters[networkInstanceNames.ToList().IndexOf(name)] = new PerformanceCounter("Network Interface", "Bytes Sent/sec", name);
                networkReceivedCounters[networkInstanceNames.ToList().IndexOf(name)] = new PerformanceCounter("Network Interface", "Bytes Received/sec", name);
            }
            adapters = NetworkInterface.GetAllNetworkInterfaces();
            getActiveAdapters();

        }

        private List<Int32> getActiveAdapters()
        {
            String adapterNames;
            List<Int32> activeAdapterList = new List<Int32>();
            foreach (NetworkInterface adapter in adapters)
            {
                adapterNames = adapter.Description.Replace('(', '[').Replace(')', ']');
                if (adapter.OperationalStatus == OperationalStatus.Up && networkInstanceNames.Contains(adapterNames) && adapter.GetIPProperties().GatewayAddresses.Count > 0)
                {
                    activeAdapterList.Add(networkInstanceNames.ToList().IndexOf(adapterNames));
                }
            }
            return activeAdapterList;
        }

        private void InitializeIcon()
        {
            Size size = new Size(16, 16);
            bitmap = new Bitmap(size.Width, size.Height);
            graphics = Graphics.FromImage(bitmap);
            stringFont = new Font("Microsoft Yahei", 14, FontStyle.Regular, GraphicsUnit.Pixel);
            stringSolidBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
            stringRectangle = new Rectangle(-20, -20, size.Width + 40, size.Height + 40);
            stringFormat = new StringFormat() { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        private void setIcon(string symbol)
        {
            graphics.Clear(Color.FromArgb(0, 0, 0, 0));
            graphics.DrawString(symbol, stringFont, stringSolidBrush, stringRectangle, stringFormat);
            try
            {
                DestroyIcon(notify.Icon.Handle);
                notify.Icon = Icon.FromHandle(bitmap.GetHicon());
            }
            catch
            {
                notify.Icon = Icon;
            }
        }

        private String getCPURate()
        {
            return "CPU: " + (Int16)CPUCounter.NextValue() + "%";
        }

        private String getRAMRate()
        {
            Int16 RAMRate = (Int16)(100 * (RAMCapacity - RAMCounter.NextValue()) / RAMCapacity);
            if (prevRAMRate != RAMRate || notify.Icon == Icon)
            {
                if (RAMRate < 100 && RAMRate >= 0)
                    setIcon(RAMRate.ToString());
                else
                    setIcon("∞");
                prevRAMRate = RAMRate;
            }
            return "RAM: " + RAMRate + "%";
        }

        private String getDiskRate()
        {
            Double diskReadRate = diskReadCounter.NextValue() / 1024 / 1024;
            Double diskWriteRate = diskReadCounter.NextValue() / 1024 / 1024;
            Double diskRate = diskReadRate + diskWriteRate;
            if (diskReadRate < 10)
                diskReadRate = Math.Round(diskReadRate, 1);
            else
                diskReadRate = Math.Round(diskReadRate, 0);

            if (diskWriteRate < 10)
                diskWriteRate = Math.Round(diskWriteRate, 1);
            else
                diskWriteRate = Math.Round(diskWriteRate, 0);
            return "DISK: " + diskReadRate + "+" + diskWriteRate + " MB/S";
        }

        private void getRAMCapacity()
        {
            ManagementClass mc = new ManagementClass("Win32_PhysicalMemory");
            ManagementObjectCollection moc = mc.GetInstances();
            try
            {
                foreach (ManagementObject mo in moc)
                    RAMCapacity += Int64.Parse(mo.Properties["Capacity"].Value.ToString()) / 1024 / 1024;
            }
            catch
            {
                Application.Exit();
            }
            moc.Dispose();
            mc.Dispose();
        }

        private String getNetworkRate()
        {
            Double networkSentRate = 0;
            Double networkReceivedRate = 0;

            foreach (int index in getActiveAdapters())
            {
                networkSentRate += networkSentCounters[index].NextValue() / 1024;
                networkReceivedRate += networkReceivedCounters[index].NextValue() / 1024;
            }
            Console.WriteLine(networkSentRate);
            if(networkSentRate < 1024 && networkReceivedRate < 1024)
            {
                networkSentRate = (Int32)(networkSentRate);
                networkReceivedRate = (Int32)(networkReceivedRate);
                return "NET: " + networkSentRate + "+" + networkReceivedRate + " KB/S";
            }
            else
            {
                networkSentRate = networkSentRate / 1024;
                networkReceivedRate = networkReceivedRate / 1024;

                if (networkSentRate < 10)
                    networkSentRate = Math.Round(networkSentRate, 1);
                else
                    networkSentRate = Math.Round(networkSentRate, 0);

                if (networkReceivedRate < 10)
                    networkReceivedRate = Math.Round(networkReceivedRate, 1);
                else
                    networkReceivedRate = Math.Round(networkReceivedRate, 0);

                return "NET: " + networkSentRate + "+" + networkReceivedRate + " MB/S";
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            notify.Text = getCPURate() + "\n" + getRAMRate() + "\n" + getDiskRate() + "\n" + getNetworkRate();
        }

        private void taskManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Environment.GetEnvironmentVariable("windir") + @"\System32\taskmgr.exe");
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Created on Aug 31, 2017\nSystem Monitor 1.0.0.0\nCopyright © 2017 A.H. Zhang", "System Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void notify_DoubleClick(object sender, EventArgs e)
        {
            taskManagerToolStripMenuItem_Click(null, null);
        }
    }
}

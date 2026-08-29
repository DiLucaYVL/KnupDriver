using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using HidSharp;

class Program
{
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_SetOutputReport(SafeFileHandle HidDeviceObject, byte[] lpReportBuffer, int ReportBufferLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    const uint GENERIC_READ = 0x80000000;
    const uint GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 0x00000001;
    const uint FILE_SHARE_WRITE = 0x00000002;
    const uint OPEN_EXISTING = 3;

    static void Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  TESTE ESPECÍFICO: MOTOR ESQUERDO / DIREITO     ");
        Console.WriteLine("=================================================\n");

        var allHids = DeviceList.Local.GetHidDevices().ToList();
        var knupDev = allHids.FirstOrDefault(d => d.VendorID == 0x0810 && d.ProductID == 0x0001);

        if (knupDev == null)
        {
            Console.WriteLine("❌ Controle Knup não encontrado!");
            return;
        }

        var handle = CreateFile(
            knupDev.DevicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid) return;

        // Teste 1: Motor Esquerdo (Forte)
        Console.WriteLine("1. VIBRANDO MOTOR ESQUERDO (FORTE) por 2 segundos...");
        byte[] leftReport = new byte[5] { 0x01, 0xFF, 0x00, 0x00, 0xFF };
        DateTime end1 = DateTime.Now.AddSeconds(2);
        while (DateTime.Now < end1)
        {
            HidD_SetOutputReport(handle, leftReport, 5);
            Thread.Sleep(50);
        }
        HidD_SetOutputReport(handle, new byte[5] { 0x01, 0, 0, 0, 0 }, 5);
        Thread.Sleep(1200);

        // Teste 2: Motor Direito (Fraco)
        Console.WriteLine("2. VIBRANDO MOTOR DIREITO (FRACO) por 2 segundos...");
        byte[] rightReport = new byte[5] { 0x01, 0x00, 0xFF, 0x00, 0xFF };
        DateTime end2 = DateTime.Now.AddSeconds(2);
        while (DateTime.Now < end2)
        {
            HidD_SetOutputReport(handle, rightReport, 5);
            Thread.Sleep(50);
        }
        HidD_SetOutputReport(handle, new byte[5] { 0x01, 0, 0, 0, 0 }, 5);
        Thread.Sleep(1200);

        // Teste 3: Ambos os motores
        Console.WriteLine("3. VIBRANDO AMBOS OS MOTORES JUNTOS por 3 segundos...");
        byte[] bothReport = new byte[5] { 0x01, 0xFF, 0xFF, 0x00, 0xFF };
        DateTime end3 = DateTime.Now.AddSeconds(3);
        while (DateTime.Now < end3)
        {
            HidD_SetOutputReport(handle, bothReport, 5);
            Thread.Sleep(50);
        }
        HidD_SetOutputReport(handle, new byte[5] { 0x01, 0, 0, 0, 0 }, 5);

        handle.Dispose();
        Console.WriteLine("\n✔ Teste concluído com sucesso!");
    }
}

















